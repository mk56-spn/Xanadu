using System.IO;
using Xanadu.Core.Storage;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A shader editor inside the lab for the spawner: code typed on the left,
/// compiled as you type and drawn on the right. Slang's problems mark their
/// lines; calls into the library show their parameter names as inlay hints. The text saves itself to a git-ignored file.
/// What the code compiles into and how it is drawn is the preview's
/// (<see cref="IEditorPreview"/>). Effects are edited in the shader tool.
/// </summary>
internal sealed class ShaderEditor(string name, string workingCopy, string template,
    Func<LabContext, Handle, IEditorPreview> preview) : ILabExperiment
{
    /// <summary>The spawner: <c>count</c> things from <c>spawn(i)</c>, culled and drawn indirectly on the GPU.</summary>
    public static ShaderEditor Spawner() => new("SPAWNER",
        "Tools/Labs/Playground/spawner.slang", "Tools/Labs/Playground/spawner.template.slang",
        (context, item) => new SpawnerPreview(context, item));

    public string Name => name;

    /// <summary>The editor lays out the whole body itself; its help sits under the problem list.</summary>
    public bool FullWidth => true;

    public string[] Notes => [];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(context, parent, area, WorkingFile(), preview);

    /// <summary>The lab's own working copy, made from the template the first time.</summary>
    private string WorkingFile()
    {
        string copy = GamePaths.Full(workingCopy);
        if (!File.Exists(copy))
            File.Copy(GamePaths.Full(template), copy);
        return copy;
    }

    /// <summary>
    /// The lab's layout round a <see cref="ShaderCodeEditor"/>: the code on the left, the preview
    /// (16:9) and its column on the right, a divider between that drags. The text saves to the
    /// working file before every compile.
    /// </summary>
    private sealed class Stage(LabContext context, Handle parent, Rect2 area, string workingFile,
        Func<LabContext, Handle, IEditorPreview> preview) : IToolStage
    {
        private const float Gap = 12;
        private const float StatusHeight = 32;

        /// <summary>The right-hand column's width at first: the preview at 16:9 on top, problems and help under it.</summary>
        private const float SideWidth = 720;

        /// <summary>The least the code and the column can be dragged down to; the column also keeps room under the preview.</summary>
        private const float MinCode = 360, MinSide = 280, MinUnderPreview = 260 + ClipTimeline.MinHeight;

        private ShaderCodeEditor? _code;
        private UiNode _side, _preview, _divider;
        private float _sideWidth;

        /// <summary>The divider is being dragged; where the pointer grabbed it from its middle.</summary>
        private bool _dragging;
        private float _grabbedAt;
        private bool _wasHeld;

        /// <summary>
        /// The code and its status bar on the left, the divider in the gap, the preview (16:9, as
        /// the game's playfield is, so a shader looks here as it will there) and the column under it
        /// on the right; the editor lives in these slots.
        /// </summary>
        public void Build(UiNode body)
        {
            _sideWidth = ClampSide(SideWidth);
            UiNode row = body.Row().Cross(StackCrossAlignment.Stretch).Grow();
            UiNode code = row.Column().Cross(StackCrossAlignment.Stretch).Grow().MinWidth(MinCode);
            UiNode codeSlot = code.Column().Grow();
            UiNode barSlot = code.Column().Height(StatusHeight);
            _divider = row.Column().Width(Gap);
            _side = row.Column().Gap(Gap).Cross(StackCrossAlignment.Stretch).Width(_sideWidth);
            _preview = _side.Column().Height(PreviewHeight(_sideWidth));
            UiNode sideSlot = _side.Column().Grow();

            _code = new ShaderCodeEditor(context.Entities, parent, new ShaderEditorSlots(codeSlot, barSlot, _preview, sideSlot, _divider),
                File.ReadAllText(workingFile), item => preview(context, item))
            {
                // Saving is a convenience; the compile goes ahead.
                Compiling = source =>
                {
                    try
                    {
                        File.WriteAllText(workingFile, source);
                    }
                    catch (IOException)
                    {
                    }
                },
            };
        }

        private static float PreviewHeight(float width) => MathF.Round(width * 9f / 16f);

        /// <summary>A column width that leaves the code, the column and the room under the preview their least.</summary>
        private float ClampSide(float width)
        {
            float most = MathF.Min(area.Size.X - Gap - MinCode, (area.Size.Y - Gap - MinUnderPreview) * 16f / 9f);
            return MathF.Round(Math.Clamp(width, MinSide, MathF.Max(MinSide, most)));
        }

        public void Render(MoonWorks.Graphics.CommandBuffer commands) => _code?.Render(commands);

        public void Update(in ToolFrame frame)
        {
            if (_code is null || !UiLayout.Settled(_side.Entity))
                return;

            bool pressed = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;
            Rect2 divider = UiLayout.Box(_divider.Entity);

            // The divider: a press on it drags the column's edge until let go; only the column's width changes.
            if (pressed && divider.HasPoint(frame.Mouse))
            {
                _dragging = true;
                _grabbedAt = frame.Mouse.X - divider.GetCenter().X;
            }

            if (_dragging)
            {
                Rect2 side = UiLayout.Box(_side.Entity);
                float width = ClampSide(side.End.X - (frame.Mouse.X - _grabbedAt) - Gap / 2);
                if (width != _sideWidth)
                {
                    _sideWidth = width;
                    _side.Width(width);
                    _preview.Height(PreviewHeight(width));
                }

                if (!frame.Held)
                {
                    _dragging = false;
                    // Pressing outside the code took its focus; dragging shouldn't cost the typing.
                    _code.Focus();
                }
            }

            _code.DividerLit = _dragging || divider.HasPoint(frame.Mouse);
            _code.Update(frame, pointerFree: !_dragging);
        }

        public void Dispose() => _code?.Dispose();
    }
}
