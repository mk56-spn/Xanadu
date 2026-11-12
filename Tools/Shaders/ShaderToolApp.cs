using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Host;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Ui;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Shaders;

// Reads the pointer outside UI (BannedSymbols.txt): as the tool shell does, the app hands the editor its frame.
#pragma warning disable RS0030

/// <summary>
/// The shader tool: every shipped effect in a list on the left, each with a
/// live thumbnail; the picked one's code in the shared Slang editor
/// (<see cref="ShaderCodeEditor"/>) with its preview and problems on the right.
/// Edits stay in memory, kept per shader while others are picked, until saved
/// (Ctrl+S or SAVE) or put back (REVERT). It is all containers: the code, its
/// status bar, the divider, the preview and the column under it are slots the
/// editor reads its boxes from; dragging the divider only resizes a container.
/// </summary>
internal sealed class ShaderToolApp : HostApp
{
    private const float Margin = UiMetrics.Space.Lg;
    private const float Gap = UiMetrics.Space.Md;
    private const float ListWidth = 300;
    private const float ThumbWidth = 112, ThumbHeight = 63;
    private const float StatusHeight = 32;

    /// <summary>The preview column's width at first, and the least the code and the column can be dragged to.</summary>
    private const float SideWidth = 680, MinSide = 280, MinCode = 360;

    /// <summary>Room the column keeps under the preview for problems, sliders and help.</summary>
    private const float MinUnderPreview = 260;

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    private readonly ShaderFile[] _files;
    private readonly Entity[] _rows;
    private readonly Entity[] _thumbs;

    /// <summary>Edited text not yet saved, by file; a file without an entry is as on disk.</summary>
    private readonly Dictionary<ShaderFile, string> _unsaved = new();

    private Entity _title, _save, _revert, _note;
    private Entity _body, _side, _preview;
    private Entity _codeSlot, _barSlot, _sideSlot, _dividerSlot;
    private readonly HashSet<long> _held = new();

    private int _picked = -1;
    private int _wanted;
    private ShaderCodeEditor? _code;
    private Handle _codeItem;
    private ShaderEditorBoxes _boxes;

    /// <summary>The text as last saved or read, for telling whether the code has changes.</summary>
    private string _disk = "";
    private int _seenVersion = -1;

    private float _sideWidth = SideWidth;
    private bool _dragging;
    private float _grabbedAt;
    private bool _wasHeld;

    public ShaderToolApp(HostWindowOptions window, string? open) : base("Xanadu Shaders", window)
    {
        _files = ShaderFiles.All();
        _rows = new Entity[_files.Length];
        _thumbs = new Entity[_files.Length];
        _wanted = Math.Max(0, Array.FindIndex(_files, file => file.Name == open || file.Path == open));
        Build();
    }

    private void Build()
    {
        UiNode ui = UiNode.Layer(Entities, default, UiLayers.Base, null, UiStage.Full).Theme(UiThemes.Tools);
        UiNode page = ui.Page().Pad(Margin).Gap(Gap).Cross(StackCrossAlignment.Stretch);
        UiNode body = page.Row().Gap(Gap).Cross(StackCrossAlignment.Stretch).Grow();
        _body = body;

        UiNode list = body.Panel().Width(ListWidth).Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Stretch);
        list.Text("SHADERS", UiType.Section).AccentText();
        for (int i = 0; i < _files.Length; i++)
        {
            UiNode row = list.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
            // The effect as shipped, drawn live with its own defaults; saving makes it compile again.
            _thumbs[i] = row.Rect(Colors.White, size: new Vector2(ThumbWidth, ThumbHeight))
                .With(new UiMaterial(new CanvasMaterial(_files[i].Path)))
                .Input()
                .Tooltip(_files[i].Path);
            _rows[i] = row.Button(_files[i].Name, UiType.Small).Quiet().Grow().Height(ThumbHeight);
        }

        // The editor: code over its status bar, the divider, then the preview over its column.
        UiNode editor = body.Row().Cross(StackCrossAlignment.Stretch).Grow();
        UiNode code = editor.Column().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Stretch).Grow().MinWidth(MinCode);
        UiNode bar = code.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        _title = bar.Text("", UiType.Label).AccentText().Grow();
        // Sized for its longest words: a text box is measured from what it holds when made.
        _note = bar.Text("unsaved changes", UiType.Small, align: TextAlign.Right).DimText();
        _revert = bar.Button("REVERT", UiType.Small).Secondary().Tooltip("Put the code back as the file has it");
        _save = bar.Button("SAVE", UiType.Small).Primary().Tooltip("Write the code to the file (Ctrl+S)");
        _codeSlot = code.Column().Grow();
        _barSlot = code.Column().Height(StatusHeight);

        _dividerSlot = editor.Column().Width(Gap);
        UiNode side = editor.Column().Gap(Gap).Cross(StackCrossAlignment.Stretch).Width(_sideWidth);
        _side = side;
        _preview = side.Column().Height(PreviewHeight(_sideWidth));
        _sideSlot = side.Column().Grow();
    }

    private static float PreviewHeight(float width) => MathF.Round(width * 9f / 16f);

    protected override void Tick(float delta, float time)
    {
        if (!UiLayout.Settled(_codeSlot) || !UiLayout.Settled(_sideSlot))
            return;

        for (int i = 0; i < _files.Length; i++)
        {
            if (Clicked(_rows[i]) || Clicked(_thumbs[i]))
                _wanted = i;
        }

        if (_wanted != _picked)
            Pick(_wanted);
        if (_code is null)
            return;

        if (Clicked(_save))
            Save();
        if (Clicked(_revert))
            Revert();

        var frame = new ToolFrame(delta, time, Pointer.Position, Pointer.LeftDown, Inputs, Pointer.TakeWheel());
        Drag(frame);
        Follow();
        _code.Update(frame, pointerFree: !_dragging);
        ShowChanges();
    }

    /// <summary>Opens a shader: its unsaved text if it has some, else the file's.</summary>
    private void Pick(int index)
    {
        if (_picked >= 0 && _code is not null)
            Keep();
        _code?.Dispose();
        if (!_codeItem.Equals(default(Handle)))
            CanvasServer.Current.Free(_codeItem);

        _picked = index;
        ShaderFile file = _files[index];
        _disk = ShaderFiles.Read(file);
        string text = _unsaved.GetValueOrDefault(file, _disk);

        _codeItem = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_codeItem, Canvas.RootRid);
        CanvasServer.Current.SetZIndex(_codeItem, StageZ);
        _boxes = Boxes();
        string label = file.Name + ".slang";
        _code = new ShaderCodeEditor(Entities, _codeItem, _boxes, text,
            item => new WholeEffectPreview(Renderer.Pipelines, item, label));
        _code.SaveRequested += Save;
        _seenVersion = -1;

        UiText.Set(_title, file.Path);
        for (int i = 0; i < _rows.Length; i++)
        {
            // Bright blue only for the one open; the rest at rest.
            UiNode row = UiNode.Wrap(_rows[i]);
            if (i == index)
                row.Primary();
            else
                row.Quiet();
            _rows[i].AddTag<Dirty>();
        }
    }

    /// <summary>Remembers the open shader's text while another is picked, if it differs from the file.</summary>
    private void Keep()
    {
        ShaderFile file = _files[_picked];
        string text = _code!.Text;
        if (text == _disk)
            _unsaved.Remove(file);
        else
            _unsaved[file] = text;
    }

    private void Save()
    {
        if (_code is null)
            return;

        ShaderFile file = _files[_picked];
        string text = _code.Text;
        if (!ShaderFiles.Write(file, text, out string? error))
        {
            UiText.Set(_note, "not saved: " + error);
            return;
        }

        _disk = text;
        _unsaved.Remove(file);
        _seenVersion = -1;
        // The thumbnail draws the file, so it compiles again from what was just written.
        Renderer.Pipelines.Reload(file.Path);
    }

    /// <summary>Puts the file's text back: a fresh editor on it.</summary>
    private void Revert()
    {
        _unsaved.Remove(_files[_picked]);
        int index = _picked;
        _picked = -1;
        _code!.Dispose();
        _code = null;
        Pick(index);
    }

    /// <summary>Marks the list and the bar when the code differs from the file; checked only when the text changes.</summary>
    private void ShowChanges()
    {
        if (_code!.Version == _seenVersion)
            return;

        _seenVersion = _code.Version;
        bool changed = _code.Text != _disk;
        if (changed)
            _unsaved[_files[_picked]] = _code.Text;
        else
            _unsaved.Remove(_files[_picked]);

        UiText.Set(_note, changed ? "unsaved changes" : "saved");
        UiLayout.SetDisabled(_save, !changed);
        UiLayout.SetDisabled(_revert, !changed);
        for (int i = 0; i < _files.Length; i++)
            UiText.Set(_rows[i], _unsaved.ContainsKey(_files[i]) ? _files[i].Name + " •" : _files[i].Name);
    }

    /// <summary>The divider: a press on it drags the preview column's edge; only the column's container width changes.</summary>
    private void Drag(in ToolFrame frame)
    {
        bool pressed = frame.Held && !_wasHeld;
        _wasHeld = frame.Held;
        if (pressed && _boxes.Divider.HasPoint(frame.Mouse))
        {
            _dragging = true;
            _grabbedAt = frame.Mouse.X - _boxes.Divider.GetCenter().X;
        }

        if (_dragging)
        {
            Rect2 side = UiLayout.Box(_side);
            float width = ClampSide(side.End.X - (frame.Mouse.X - _grabbedAt) - Gap / 2);
            if (width != _sideWidth)
            {
                _sideWidth = width;
                UiNode.Wrap(_side).Width(width);
                UiNode.Wrap(_preview).Height(PreviewHeight(width));
            }

            if (!frame.Held)
            {
                _dragging = false;
                // Pressing outside the code took its focus; dragging shouldn't cost the typing.
                _code!.Focus();
            }
        }

        _code!.DividerLit = _dragging || _boxes.Divider.HasPoint(frame.Mouse);
    }

    /// <summary>A column width that leaves the code its least, and room under the preview.</summary>
    private float ClampSide(float width)
    {
        Rect2 body = UiLayout.Box(_body);
        float most = MathF.Min(body.Size.X - ListWidth - Gap * 2 - MinCode, (body.Size.Y - Gap - MinUnderPreview) * 16f / 9f);
        return MathF.Round(Math.Clamp(width, MinSide, MathF.Max(MinSide, most)));
    }

    /// <summary>The editor's parts, from their slots.</summary>
    private ShaderEditorBoxes Boxes() => new(
        UiLayout.Box(_codeSlot), UiLayout.Box(_barSlot), UiLayout.Box(_preview), UiLayout.Box(_sideSlot), UiLayout.Box(_dividerSlot));

    /// <summary>Hands the editor its slots' boxes when the layout moves them.</summary>
    private void Follow()
    {
        ShaderEditorBoxes boxes = Boxes();
        if (boxes == _boxes)
            return;
        _boxes = boxes;
        _code!.Place(boxes);
    }

    /// <summary>True on the frame a button goes down.</summary>
    private bool Clicked(Entity button)
    {
        if (button.Tags.Has<Pressed>())
            return _held.Add(button.Id);

        _held.Remove(button.Id);
        return false;
    }

    protected override void RenderOffscreen(MoonWorks.Graphics.CommandBuffer commands) => _code?.Render(commands);

    protected override void Shutdown() => _code?.Dispose();
}
