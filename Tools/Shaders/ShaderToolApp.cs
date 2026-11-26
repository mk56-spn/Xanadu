using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Core.Theme;
using Xanadu.Host;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Tools.Shaders;

// Reads the pointer outside UI (BannedSymbols.txt): as the tool shell does, the app hands the editor its frame.
#pragma warning disable RS0030

/// <summary>
/// The shader tool: the picked shader stack's files in the shared Slang editor
/// (<see cref="ShaderCodeEditor"/>), its pipeline over the code, with its preview
/// and problems on the right. Edits (files added, renamed and removed too) stay in memory,
/// kept per stack while others are picked, until saved (Ctrl+S or SAVE) or put
/// back (REVERT). The bar's SHADERS button opens every shipped stack as a grid of
/// live thumbnails (<see cref="ShaderGrid"/>) over the editor, so the editor keeps the
/// whole width; a card opens its stack. NEW makes a stack, a shade brick from its template
/// (<see cref="ShaderFiles.Create"/>), and opens it. It is all containers: the code, its
/// status bar, the divider, the preview and the column under it are slots the
/// editor reads its boxes from; dragging the divider only resizes a container.
/// </summary>
internal sealed class ShaderToolApp : HostApp
{
    // The game's shader globals, filled by the editor's stand-ins; declared before the shipped effects warm.
    static ShaderToolApp() => RhythmGlobals.Declare();

    private const float Margin = UiMetrics.Space.Lg;
    private const float Gap = UiMetrics.Space.Md;

    private const float StatusHeight = 32;

    /// <summary>Stages draw above the ECS layer they sit on.</summary>
    private const int StageZ = UiLayers.Base + 1;

    private readonly List<ShaderFile> _files;
    private readonly ShaderStills _stills;

    /// <summary>Stacks deleted this run, by file index: their places stay so every other index holds.</summary>
    private readonly HashSet<int> _gone = [];
    private ShaderGrid _grid = null!;

    /// <summary>Edited files not yet saved, by stack; a stack without an entry is as on disk.</summary>
    private readonly Dictionary<ShaderFile, List<StackFile>> _unsaved = [];

    private Entity _shaders, _title, _save, _revert, _note, _graphButton, _trianglesButton, _uvButton;

    /// <summary>The preview's overlays, kept across shaders: the triangles' edges, and where uv is 0 and 1.</summary>
    private bool _showGraph, _showTriangles, _showUv;
    private EffectPreview? _effect;
    private Entity _body, _side, _preview;
    private Entity _codeSlot, _barSlot, _sideSlot, _dividerSlot;
    private readonly HashSet<long> _held = [];

    private int _picked = -1;
    private int _wanted;
    private ShaderCodeEditor? _code;
    private Handle _codeItem;

    /// <summary>The files as last saved or read, for telling whether the code has changes.</summary>
    private List<StackFile> _disk = [];
    private int _seenVersion = -1;

    private ShaderDivider _divider = null!;

    private readonly SystemRoot _systems;
    private readonly ToastSystem _toasts;

    public ShaderToolApp(HostWindowOptions window, string? open, bool graph = false) : base("Xanadu Shaders", window)
    {
        _showGraph = graph;
        _toasts = new ToastSystem(Entities, new Vector2(Width, Height), HostPlatform.Visuals);
        _systems = new SystemRoot(Entities, "Shaders") { _toasts };
        UseSystems(_systems);
        _files = [.. ShaderFiles.All()];
        _stills = new ShaderStills(Renderer, Canvas, Width, Height);
        _wanted = Math.Max(0, _files.FindIndex(file => file.Name == open || file.Path == open));
        Build();
    }

    private void Build()
    {
        UiNode ui = UiNode.Layer(Entities, default, UiLayers.Base, null, UiStage.Full).Theme(UiThemes.Tools);
        UiNode page = ui.Page().Pad(Margin).Gap(Gap).Cross(StackCrossAlignment.Stretch);
        UiNode body = page.Row().Gap(Gap).Cross(StackCrossAlignment.Stretch).Grow();
        _body = body;

        // The editor: code over its status bar, the divider, then the preview over its column.
        UiNode editor = body.Row().Cross(StackCrossAlignment.Stretch).Grow();
        UiNode code = editor.Column().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Stretch).Grow().MinWidth(ShaderDivider.MinCode);
        UiNode bar = code.Row().Gap(UiMetrics.Space.Sm).Cross(StackCrossAlignment.Center);
        _shaders = bar.Button("SHADERS", UiType.Small).Secondary().Tooltip("Every shader as a grid of thumbnails: pick one to open it");
        _title = bar.Text("", UiType.Label).AccentText().Grow();
        // Sized for its longest words: a text box is measured from what it holds when made.
        _note = bar.Text("unsaved changes", UiType.Small, align: TextAlign.Right).DimText();
        _graphButton = bar.Button("GRAPH", UiType.Small).Secondary()
            .Tooltip("Show the stack's render graph where the code is: its passes in order, the data they share, and who reads and writes what");
        if (_showGraph)
            UiNode.Wrap(_graphButton).Primary();
        _trianglesButton = bar.Button("TRIANGLES", UiType.Small).Secondary()
            .Tooltip("Outline every triangle of the preview's mesh, where the vertex stage put it (not for pools yet)");
        _uvButton = bar.Button("UV", UiType.Small).Secondary()
            .Tooltip("Lines where uv is 0 (solid) and 1 (dashed): red for u, green for v, after the vertex stage (not for pools yet)");
        _revert = bar.Button("REVERT", UiType.Small).Secondary().Tooltip("Put the code back as the file has it");
        _save = bar.Button("SAVE", UiType.Small).Primary().Tooltip("Write the code to the file (Ctrl+S)");
        _codeSlot = code.Column().Grow();
        _barSlot = code.Column().Height(StatusHeight);

        _dividerSlot = editor.Column().Width(Gap);
        UiNode side = editor.Column().Gap(Gap).Cross(StackCrossAlignment.Stretch).Width(ShaderDivider.SideWidth);
        _side = side;
        _preview = side.Column().Height(ShaderDivider.PreviewHeight(ShaderDivider.SideWidth));
        _divider = new ShaderDivider(_body, _side, _preview, _dividerSlot, Gap);
        _sideSlot = side.Column().Grow();

        _grid = new ShaderGrid(UiNode.Layer(Entities, default, UiLayers.ShaderPicker, null, UiStage.Full).Theme(UiThemes.Tools));
        for (int i = 0; i < _files.Count; i++)
            AddCard(i);
    }

    /// <summary>A file's card in the grid; saving makes its thumbnail compile again.</summary>
    private void AddCard(int i) => _grid.Add(_files[i], SlangShaders.IsLabOnly(_files[i].Path) ? _stills.Make(i, _files[i].Path) : null);

    /// <summary>NEW: writes a fresh effect from the template, lists it and opens it.</summary>
    private void New()
    {
        if (!ShaderFiles.Create(out ShaderFile? file, out string? error))
        {
            UiText.Set(_note, "not made: " + error);
            return;
        }

        _files.Add(file!);
        AddCard(_files.Count - 1);
        _wanted = _files.Count - 1;
    }

    protected override void Tick(float delta, float time)
    {
        _stills.Settle();
        if (!UiLayout.Settled(_codeSlot) || !UiLayout.Settled(_sideSlot))
            return;

        PickFromGrid();

        if (_wanted != _picked)
            Pick(_wanted);
        if (_code is null)
            return;

        if (Clicked(_graphButton))
        {
            Toggle(ref _showGraph, _graphButton);
            _code.ShowsGraph = _showGraph;
        }
        if (Clicked(_trianglesButton))
            Toggle(ref _showTriangles, _trianglesButton);
        if (Clicked(_uvButton))
            Toggle(ref _showUv, _uvButton);
        if (Clicked(_save))
            Save();
        if (Clicked(_revert))
            Revert();

        // The overlays don't draw graphs yet: their buttons rest while one is shown.
        bool graph = _effect?.HasGraph == true;
        UiLayout.SetDisabled(_trianglesButton, graph);
        UiLayout.SetDisabled(_uvButton, graph);

        // Under the grid the code gets no press and no wheel: those are the grid's.
        bool free = !_grid.Open;
        var frame = new ToolFrame(delta, time, Pointer.Position, free && Pointer.LeftDown, Inputs, free ? Pointer.TakeWheel() : 0);
        _divider.Update(frame, _code);
        _code.Update(frame, pointerFree: free && !_divider.Dragging);
        ShowChanges();
    }

    /// <summary>Opens a stack: its unsaved files if it has some, else the folder's.</summary>
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
        List<StackFile> files = _unsaved.GetValueOrDefault(file, _disk);

        _codeItem = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_codeItem, Canvas.RootHandle);
        CanvasServer.Current.SetZIndex(_codeItem, StageZ);
        var slots = new ShaderEditorSlots(UiNode.Wrap(_codeSlot), UiNode.Wrap(_barSlot), UiNode.Wrap(_preview),
            UiNode.Wrap(_sideSlot), UiNode.Wrap(_dividerSlot));
        _effect = null;
        _code = new ShaderCodeEditor(Entities, _codeItem, slots, file.Path, files,
            item =>
            {
                _effect = new EffectPreview(Renderer, item, file.Path);
                _effect.ShowOverlays(_showTriangles, _showUv);
                return _effect;
            });
        _code.SaveRequested += Save;
        _code.Toasts = _toasts;
        _code.ShowsGraph = _showGraph;
        _seenVersion = -1;

        UiText.Set(_title, file.Path);
        _grid.Mark(index);
    }

    /// <summary>SHADERS opens the grid; a card picks and closes it, as do CLOSE, Escape and a press around the panel.</summary>
    private void PickFromGrid()
    {
        if (Clicked(_shaders) || (_wanted == _picked && ShaderGrid.TakeStartOpen()))
            _grid.Show(true);
        if (!_grid.Open)
            return;

        if (Clicked(_grid.NewButton))
            New();
        if (_grid.TakeMenuPick() is { } pick)
            OnCardMenu(pick.File, pick.Item);
        if (_grid.Renamed() is { } renamed)
            Rename(renamed.File, renamed.Typed);
        for (int i = 0; i < _files.Count; i++)
        {
            if (Clicked(_grid.Cards[i]) || Clicked(_grid.Thumbs[i]))
                _wanted = i;
        }

        if (_wanted != _picked || Clicked(_grid.CloseButton) || Clicked(_grid.Scrim) || Inputs.Keyboard.IsPressed(MoonWorks.Input.KeyCode.Escape))
        {
            _grid.Show(false);
            _code?.Focus();
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

    /// <summary>Remembers the open stack's files while another is picked, if they differ from the folder.</summary>
    private void Keep()
    {
        ShaderFile file = _files[_picked];
        List<StackFile> files = _code!.Files;
        if (files.SequenceEqual(_disk))
            _unsaved.Remove(file);
        else
            _unsaved[file] = files;
    }

    private void Save()
    {
        if (_code is null)
            return;

        ShaderFile file = _files[_picked];
        List<StackFile> files = _code.Files;
        if (!ShaderFiles.Write(file, files, out string? error))
        {
            UiText.Set(_note, "not saved: " + error);
            return;
        }

        _disk = files;
        _unsaved.Remove(file);
        _seenVersion = -1;
        // The thumbnail draws the file, so it compiles again from what was just written.
        Renderer.Pipelines.Reload(file.Path);
        _stills.Redraw(_picked, file.Path);
    }

    /// <summary>Puts the folder's files back: a fresh editor on them.</summary>
    private void Revert()
    {
        _unsaved.Remove(_files[_picked]);
        int index = _picked;
        _picked = -1;
        _code!.Dispose();
        _code = null;
        Pick(index);
    }

    /// <summary>Marks the list and the bar when the files differ from the folder; checked only when they change.</summary>
    private void ShowChanges()
    {
        if (_code!.Version == _seenVersion)
            return;

        _seenVersion = _code.Version;
        List<StackFile> files = _code.Files;
        bool changed = !files.SequenceEqual(_disk);
        if (changed)
            _unsaved[_files[_picked]] = files;
        else
            _unsaved.Remove(_files[_picked]);

        UiText.Set(_note, changed ? "unsaved changes" : "saved");
        UiLayout.SetDisabled(_save, !changed);
        UiLayout.SetDisabled(_revert, !changed);
        for (int i = 0; i < _files.Count; i++)
            _grid.Name(i, _unsaved.ContainsKey(_files[i]) ? _files[i].Name + " •" : _files[i].Name);
    }

    /// <summary>A card's right-click menu: RENAME puts a field on it; DELETE (asked first) deletes the stack's folder.</summary>
    private void OnCardMenu(int index, int item)
    {
        if (item == ShaderGrid.Rename)
            _grid.BeginRename(index, _files[index]);
        else if (!ShaderFiles.Delete(_files[index], out string? error))
            UiText.Set(_note, "not deleted: " + error);
        else
        {
            _unsaved.Remove(_files[index]);
            _gone.Add(index);
            _grid.Remove(index);
            // The open stack went: the first one left opens in its place.
            if (index == _picked)
                (_picked, _wanted) = (-1, Enumerable.Range(0, _files.Count).First(i => !_gone.Contains(i)));
        }
    }

    /// <summary>Renames a stack in its folder, keeping its unsaved edits; the open one opens again under its new path.</summary>
    private void Rename(int index, string typed)
    {
        ShaderFile old = _files[index];
        if (typed.Length == 0 || old.Name == typed || old.Name.EndsWith("/" + typed, StringComparison.Ordinal))
            return;
        if (index == _picked)
            Keep();
        if (!ShaderFiles.Rename(old, typed, out ShaderFile renamed, out string? error))
        {
            UiText.Set(_note, "not renamed: " + error);
            return;
        }

        _files[index] = renamed;
        if (_unsaved.Remove(old, out List<StackFile>? kept))
            _unsaved[renamed] = kept;
        _grid.Retarget(index, renamed, SlangShaders.IsLabOnly(renamed.Path));
        if (index == _picked)
            (_picked, _wanted) = (-1, index);
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
