using System;
using System.IO;
using Xanadu.Ecs;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Storage;
using Xanadu.Host.Tools;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// A shader editor inside the lab, the start of the one the game will get: a
/// level shader slot typed on the left, compiled as you type and drawn on the
/// right. Slang's problems mark their lines; calls into the library show their
/// parameter names as inlay hints. The text saves itself to a git-ignored file.
/// What the code compiles into and how it is drawn is the preview's
/// (<see cref="IEditorPreview"/>): a level shader slot, or the spawner.
/// </summary>
internal sealed class ShaderEditor(string name, string workingCopy, string template, string? file,
    Func<LabContext, Handle, IEditorPreview> preview) : ILabExperiment
{
    /// <summary>A level shader slot: <c>shade</c> on a quad.</summary>
    public static ShaderEditor Effects(string? file) => new("SHADER EDITOR",
        "res://Tools/Labs/Playground/editor.slang", "res://Tools/Labs/Playground/editor.template.slang", file,
        (context, item) => new MaterialPreview(context, item));

    /// <summary>The spawner: <c>count</c> things from <c>spawn(i)</c>, culled and drawn indirectly on the GPU.</summary>
    public static ShaderEditor Spawner() => new("SPAWNER",
        "res://Tools/Labs/Playground/spawner.slang", "res://Tools/Labs/Playground/spawner.template.slang", null,
        (context, item) => new SpawnerPreview(context, item));

    public string Name => name;

    /// <summary>The editor lays out the whole body itself; its help sits under the problem list.</summary>
    public bool FullWidth => true;

    public string[] Notes => [];

    public IToolStage CreateStage(LabContext context, Handle parent, Rect2 area) => new Stage(context, parent, area, WorkingFile(), preview);

    /// <summary><c>--editor-file</c>, or the lab's own working copy, made from the template the first time.</summary>
    private string WorkingFile()
    {
        if (file is not null)
            return file;

        string copy = GamePaths.Globalize(workingCopy);
        if (!File.Exists(copy))
            File.Copy(GamePaths.Globalize(template), copy);
        return copy;
    }

    /// <summary>
    /// The lab's layout round a <see cref="ShaderCodeEditor"/>: the code on the left, the preview
    /// (16:9) and its column on the right, a divider between that drags. The text saves to the
    /// working file before every compile.
    /// </summary>
    private sealed class Stage : IToolStage
    {
        private const float Gap = 12;
        private const float StatusHeight = 32;

        /// <summary>The right-hand column's width at first: the preview at 16:9 on top, problems and help under it.</summary>
        private const float SideWidth = 720;

        /// <summary>The least the code and the column can be dragged down to; the column also keeps room under the preview.</summary>
        private const float MinCode = 360, MinSide = 280, MinUnderPreview = 260 + ClipTimeline.Height;

        private readonly ShaderCodeEditor _code;
        private readonly Rect2 _area;
        private float _sideWidth;
        private Rect2 _divider;

        /// <summary>The divider is being dragged; where the pointer grabbed it from its middle.</summary>
        private bool _dragging;
        private float _grabbedAt;
        private bool _wasHeld;

        public Stage(LabContext context, Handle parent, Rect2 area, string workingFile, Func<LabContext, Handle, IEditorPreview> preview)
        {
            _area = area;
            _sideWidth = ClampSide(SideWidth);
            ShaderEditorBoxes boxes = Arrange(_sideWidth);
            _divider = boxes.Divider;
            _code = new ShaderCodeEditor(context.Entities, parent, boxes, File.ReadAllText(workingFile), item => preview(context, item))
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

        /// <summary>
        /// Where everything goes for a column this wide: the code and its status bar on the left, the
        /// divider in the gap, the preview (16:9, as the game's playfield is, so a shader looks here as
        /// it will there) and the column under it on the right.
        /// </summary>
        private ShaderEditorBoxes Arrange(float sideWidth)
        {
            float codeWidth = _area.Size.X - sideWidth - Gap;
            var code = new Rect2(_area.Position, new Vector2(codeWidth, _area.Size.Y - StatusHeight));
            var bar = new Rect2(_area.Position.X, code.End.Y, codeWidth, StatusHeight);
            float sideLeft = code.End.X + Gap;
            var preview = new Rect2(sideLeft, _area.Position.Y, sideWidth, MathF.Round(sideWidth * 9f / 16f));
            var side = new Rect2(sideLeft, preview.End.Y + Gap, sideWidth, _area.End.Y - preview.End.Y - Gap);
            var divider = new Rect2(code.End.X, _area.Position.Y, Gap, _area.Size.Y);
            return new ShaderEditorBoxes(code, bar, preview, side, divider);
        }

        /// <summary>A column width that leaves the code, the column and the room under the preview their least.</summary>
        private float ClampSide(float width)
        {
            float most = MathF.Min(_area.Size.X - Gap - MinCode, (_area.Size.Y - Gap - MinUnderPreview) * 16f / 9f);
            return MathF.Round(Math.Clamp(width, MinSide, MathF.Max(MinSide, most)));
        }

        public void Render(MoonWorks.Graphics.CommandBuffer commands) => _code.Render(commands);

        public void Update(in ToolFrame frame)
        {
            bool pressed = frame.Held && !_wasHeld;
            _wasHeld = frame.Held;

            // The divider: a press on it drags the column's edge until let go.
            if (pressed && _divider.HasPoint(frame.Mouse))
            {
                _dragging = true;
                _grabbedAt = frame.Mouse.X - _divider.GetCenter().X;
            }

            if (_dragging)
            {
                float width = ClampSide(_area.End.X - (frame.Mouse.X - _grabbedAt) - Gap / 2);
                if (width != _sideWidth)
                {
                    _sideWidth = width;
                    ShaderEditorBoxes boxes = Arrange(width);
                    _divider = boxes.Divider;
                    _code.Place(boxes);
                }

                if (!frame.Held)
                {
                    _dragging = false;
                    // Pressing outside the code took its focus; dragging shouldn't cost the typing.
                    _code.Focus();
                }
            }

            _code.DividerLit = _dragging || _divider.HasPoint(frame.Mouse);
            _code.Update(frame, pointerFree: !_dragging);
        }

        public void Dispose() => _code.Dispose();
    }
}
