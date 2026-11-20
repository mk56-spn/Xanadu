using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Host;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Handle = Xanadu.Core.Maths.Handle;

namespace Xanadu.Tools.Shaders;

// Reads the pointer outside UI (BannedSymbols.txt): as the tool shell does, the app hands the editor its frame.
#pragma warning disable RS0030

/// <summary>
/// The shader tool: every shipped effect in a list on the left, each with a
/// live thumbnail; the picked one's code in the shared Slang editor
/// (<see cref="ShaderCodeEditor"/>) with its preview and problems on the right.
/// Edits stay in memory, kept per shader while others are picked, until saved
/// (Ctrl+S or SAVE) or put back (REVERT). NEW makes an effect from the
/// template (<see cref="ShaderFiles.Create"/>) and opens it. It is all containers: the code, its
/// status bar, the divider, the preview and the column under it are slots the
/// editor reads its boxes from; dragging the divider only resizes a container.
/// </summary>
internal sealed class ShaderToolApp : HostApp
{
    // The game's shader globals, filled by the editor's stand-ins; declared before the shipped effects warm.
    static ShaderToolApp() => RhythmGlobals.Declare();

    private const float Margin = UiMetrics.Space.Lg;
    private const float Gap = UiMetrics.Space.Md;
    private const float ListWidth = 300;
    private const float ThumbWidth = 112, ThumbHeight = 63;

    /// <summary>Frames a lab-only effect's still is drawn for (its update fills its state first), then it stops.</summary>
    private const int StillFrames = 4;

    /// <summary>
    /// Lab-only effects (raymarchers and the like) are too heavy to draw live in the list on
    /// every frame, so each gets a still: drawn into a texture for <see cref="StillFrames"/>
    /// frames when the tool opens or the file is saved, then left as it is. By file index:
    /// the texture the thumbnail shows, and the offscreen item drawing it while it's live.
    /// </summary>
    private readonly Dictionary<int, (Texture Target, Handle Root, int Left)> _stills = new();
    private const float StatusHeight = 32;

    /// <summary>The preview column's width at first, and the least the code and the column can be dragged to.</summary>
    private const float SideWidth = 680, MinSide = 280, MinCode = 360;

    /// <summary>Room the column keeps under the preview for problems, sliders and help.</summary>
    private const float MinUnderPreview = 260;

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    private readonly List<ShaderFile> _files;
    private readonly List<Entity> _rows = new();
    private readonly List<Entity> _thumbs = new();

    /// <summary>Edited text not yet saved, by file; a file without an entry is as on disk.</summary>
    private readonly Dictionary<ShaderFile, string> _unsaved = new();

    private Entity _title, _save, _revert, _note, _new, _trianglesButton, _uvButton;

    /// <summary>The preview's overlays, kept across shaders: the triangles' edges, and where uv is 0 and 1.</summary>
    private bool _showTriangles, _showUv;
    private EffectPreview? _effect;
    private UiNode _list;
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
        _files = ShaderFiles.All().ToList();
        _wanted = Math.Max(0, _files.FindIndex(file => file.Name == open || file.Path == open));
        Build();
    }

    private void Build()
    {
        UiNode ui = UiNode.Layer(Entities, default, UiLayers.Base, null, UiStage.Full).Theme(UiThemes.Tools);
        UiNode page = ui.Page().Pad(Margin).Gap(Gap).Cross(StackCrossAlignment.Stretch);
        UiNode body = page.Row().Gap(Gap).Cross(StackCrossAlignment.Stretch).Grow();
        _body = body;

        _list = body.Panel().Width(ListWidth).Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Stretch);
        UiNode head = _list.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        head.Text("SHADERS", UiType.Section).AccentText().Grow();
        _new = head.Button("NEW", UiType.Small).Secondary().Tooltip("Make a new effect from the template and open it");
        for (int i = 0; i < _files.Count; i++)
            AddRow(i);

        // The editor: code over its status bar, the divider, then the preview over its column.
        UiNode editor = body.Row().Cross(StackCrossAlignment.Stretch).Grow();
        UiNode code = editor.Column().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Stretch).Grow().MinWidth(MinCode);
        UiNode bar = code.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        _title = bar.Text("", UiType.Label).AccentText().Grow();
        // Sized for its longest words: a text box is measured from what it holds when made.
        _note = bar.Text("unsaved changes", UiType.Small, align: TextAlign.Right).DimText();
        _trianglesButton = bar.Button("TRIANGLES", UiType.Small).Secondary()
            .Tooltip("Outline every triangle of the preview's mesh, where the vertex stage put it");
        _uvButton = bar.Button("UV", UiType.Small).Secondary()
            .Tooltip("Lines where uv is 0 (solid) and 1 (dashed): red for u, green for v, after the vertex stage");
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

    /// <summary>A file's row in the list: its thumbnail and its name.</summary>
    private void AddRow(int i)
    {
        UiNode row = _list.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        // The effect as shipped, drawn live with its own defaults; saving makes it compile again.
        _thumbs.Add((SlangShaders.IsLabOnly(_files[i].Path)
                ? row.Image(Still(i), new Vector2(ThumbWidth, ThumbHeight))
                : row.Rect(Colors.White, size: new Vector2(ThumbWidth, ThumbHeight)).With(new UiMaterial(new CanvasMaterial(_files[i].Path))))
            .Input()
            .Tooltip(_files[i].Path));
        _rows.Add(row.Button(_files[i].Name, UiType.Small).Quiet().Grow().Height(ThumbHeight));
    }

    /// <summary>NEW: writes a fresh effect from the template, lists it and opens it.</summary>
    private void New()
    {
        if (!ShaderFiles.Create(out ShaderFile? file, out string? error))
        {
            UiText.Set(_note, "not made: " + error);
            return;
        }

        _files.Add(file!);
        AddRow(_files.Count - 1);
        _wanted = _files.Count - 1;
    }

    /// <summary>A lab-only effect's still: a frame-sized texture it's drawn into, offscreen, for a few frames.</summary>
    private Handle Still(int index)
    {
        Texture target = Renderer.CreateTarget("still " + _files[index].Name);
        Handle texture = Canvas.MintHandle();
        Renderer.Textures[texture.Id] = target;
        _stills[index] = (target, default, 0);
        Redraw(index);
        return texture;
    }

    /// <summary>Draws a still again from the file: the whole frame, so the effect keeps its own coordinates.</summary>
    private void Redraw(int index)
    {
        if (!_stills.TryGetValue(index, out var still))
            return;
        Handle root = still.Left > 0 ? still.Root : Canvas.CreateItem();
        CanvasServer.Current.Clear(root);
        CanvasServer.Current.AddRect(root, new Rect2(0, 0, Width, Height), Colors.White);
        CanvasServer.Current.SetMaterial(root, new CanvasMaterial(_files[index].Path));
        if (still.Left == 0)
            Renderer.AddOffscreen(Canvas.Item(root)!, still.Target);
        _stills[index] = (still.Target, root, StillFrames);
    }

    /// <summary>Counts the live stills down; each stops drawing when it reaches 0.</summary>
    private void SettleStills()
    {
        foreach ((int index, var still) in _stills.ToArray())
        {
            if (still.Left == 0)
                continue;
            if (still.Left > 1)
            {
                _stills[index] = still with { Left = still.Left - 1 };
                continue;
            }

            if (Canvas.Item(still.Root) is { } root)
                Renderer.RemoveOffscreen(root);
            Canvas.Free(still.Root);
            _stills[index] = (still.Target, default, 0);
        }
    }

    private static float PreviewHeight(float width) => MathF.Round(width * 9f / 16f);

    protected override void Tick(float delta, float time)
    {
        SettleStills();
        if (!UiLayout.Settled(_codeSlot) || !UiLayout.Settled(_sideSlot))
            return;

        if (Clicked(_new))
            New();
        for (int i = 0; i < _files.Count; i++)
        {
            if (Clicked(_rows[i]) || Clicked(_thumbs[i]))
                _wanted = i;
        }

        if (_wanted != _picked)
            Pick(_wanted);
        if (_code is null)
            return;

        if (Clicked(_trianglesButton))
            Toggle(ref _showTriangles, _trianglesButton);
        if (Clicked(_uvButton))
            Toggle(ref _showUv, _uvButton);
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
        CanvasServer.Current.SetParent(_codeItem, Canvas.RootHandle);
        CanvasServer.Current.SetZIndex(_codeItem, StageZ);
        _boxes = Boxes();
        string label = file.Name + ".slang";
        _code = new ShaderCodeEditor(Entities, _codeItem, _boxes, text,
            item =>
            {
                _effect = new EffectPreview(Renderer.Pipelines, item, label, SlangShaders.IsLabOnly(file.Path));
                _effect.ShowOverlays(_showTriangles, _showUv);
                return _effect;
            });
        _code.SaveRequested += Save;
        _seenVersion = -1;

        UiText.Set(_title, file.Path);
        for (int i = 0; i < _rows.Count; i++)
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

    /// <summary>Flips an overlay: bright blue while it shows (the buttons at rest are muted).</summary>
    private void Toggle(ref bool on, Entity button)
    {
        on = !on;
        UiNode node = UiNode.Wrap(button);
        if (on)
            node.Primary();
        else
            node.Secondary();
        button.AddTag<Dirty>();
        _effect?.ShowOverlays(_showTriangles, _showUv);
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
        Redraw(_picked);
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
        for (int i = 0; i < _files.Count; i++)
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
