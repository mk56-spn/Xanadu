using System.Collections.Generic;
using System.Linq;
using Xanadu.Rendering;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// A shipped shader stack on a 16:9 quad, compiled from the bricks being edited
/// (composed by the editor), not the folder, through the same wrapper as level
/// shaders (its <c>shade</c> takes <c>Canvas</c> and/or <c>ItemInput</c>; a
/// <c>deform</c> bends the quad, drawn as a grid so it has corners to move). A
/// stack with a pool draws the quad once per item, a plain rect each, placed by
/// its deform brick; the overlays are off for it. Textures it declares
/// past the item's own draw white until the tool can feed them. Behind it the
/// backdrop effect (<see cref="ShaderFiles.Backdrop"/>) fills the same quad, so
/// an effect that reads the screen shows what it does to something. Over it,
/// on request, the triangles and where uv is 0 and 1 (<see cref="ShowOverlays"/>),
/// both drawn through the effect's own vertex stage.
/// </summary>
internal sealed class EffectPreview : IEditorPreview
{
    private readonly CanvasRenderer _renderer;
    private readonly CanvasPipelines _pipelines;
    private readonly Handle _backdrop;
    private readonly Handle _item;
    private readonly string _label;
    private CanvasMaterial? _material;

    /// <summary>The overlays, drawn over the effect: the triangles' edges, and the uv lines.</summary>
    private readonly Overlay _triangles, _uv;

    /// <summary>The code last shown, which the overlays draw through.</summary>
    private string? _source;
    private Rect2 _area;
    private (int Columns, int Rows)? _grid;

    /// <summary>
    /// Whether the code shown has a graph: one plain rect (its nodes need the quad uncut), no overlays.
    /// </summary>
    private bool _graph;

    /// <summary>Whether the code shown has a graph, which the overlays don't draw yet.</summary>
    public bool HasGraph => _graph;

    /// <summary>One overlay: its item and, while on, its material (the code with the overlay for shade).</summary>
    private sealed class Overlay(MaterialOverlay kind, Handle item)
    {
        public readonly MaterialOverlay Kind = kind;
        public readonly Handle Item = item;
        public bool On;
        public CanvasMaterial? Material;
    }

    /// <param name="item">Draws the backdrop; the effect goes in a child of it, so it draws over.</param>
    public EffectPreview(CanvasRenderer renderer, Handle item, string label)
    {
        _renderer = renderer;
        _pipelines = renderer.Pipelines;
        _backdrop = item;
        _label = label;
        _item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_item, item);
        // Made after the effect's item, so they draw over it: triangles, then the uv lines on top.
        _triangles = new Overlay(MaterialOverlay.Triangles, CanvasServer.Current.CreateItem());
        _uv = new Overlay(MaterialOverlay.UvLines, CanvasServer.Current.CreateItem());
        CanvasServer.Current.SetParent(_triangles.Item, item);
        CanvasServer.Current.SetParent(_uv.Item, item);
        CanvasServer.Current.SetVisible(_triangles.Item, false);
        CanvasServer.Current.SetVisible(_uv.Item, false);
        CanvasServer.Current.SetMaterial(_backdrop, new CanvasMaterial(ShaderFiles.Backdrop));
    }

    public string Label => _label;

    public IEnumerable<(string Name, string Type, string? Note)> Provided => [];

    public string[] Help =>
    [
        "A stack is every .slang file in its folder, read as one: void graph() runs its passes and draws; shade alone draws the item.",
        "Entries: graph(), shade(ItemInput i, Canvas canvas), deform(v). GRAPH shows what runs.",
        "Ctrl+S saves every file to the folder. Picking another stack keeps these edits until the tool closes.",
    ];

    public PreviewBuild Compile(string source)
    {
        CompiledShader compiled = SlangShaders.CompileEffectBody(Label, source);
        PreviewBuild build = new(source, compiled.Params, compiled);
        // Off the frame, so the overlays' first draw finds them in Slang's cache. Graphs have none (yet).
        if (Graphs(source))
            return build;
        if (_triangles.On)
            SlangShaders.CompileEffectBody(Label, source, SlangShaders.TrianglesOverlay);
        if (_uv.On)
            SlangShaders.CompileEffectBody(Label, source, SlangShaders.UvOverlay);
        return build;
    }

    /// <summary>Turns the overlays on and off: the triangles' edges, and lines where uv is 0 and 1.</summary>
    public void ShowOverlays(bool triangles, bool uv)
    {
        Turn(_triangles, triangles);
        Turn(_uv, uv);
    }

    private void Turn(Overlay overlay, bool on)
    {
        if (overlay.On == on)
            return;
        overlay.On = on;
        Refresh(overlay);
    }

    /// <summary>Gives an overlay a material for the code now shown, or none while it's off.</summary>
    private void Refresh(Overlay overlay)
    {
        CanvasMaterial? previous = overlay.Material;
        overlay.Material = overlay.On && !_graph && _source is { } source
            ? CanvasMaterial.FromSource(Label, source, overlay: overlay.Kind)
            : null;
        CanvasServer.Current.SetVisible(overlay.Item, overlay.Material is not null);
        if (overlay.Material is not null)
            CanvasServer.Current.SetMaterial(overlay.Item, overlay.Material);
        if (previous is not null)
            _pipelines.Forget(previous);
    }

    public string? Problem => _material is { } material && _renderer.GraphProblem(material) is { } problem ? $"graph() can't run: {problem}" : null;

    public string Show(PreviewBuild build)
    {
        CanvasMaterial? previous = _material;
        _material = CanvasMaterial.FromSource(Label, (string)build.Result);
        CanvasServer.Current.SetMaterial(_item, _material);
        if (previous is not null)
            _pipelines.Forget(previous);
        _source = (string)build.Result;
        (int, int)? grid = DeformGrid.Of(_source);
        bool graph = Graphs(_source);
        if (grid != _grid || graph != _graph)
        {
            (_grid, _graph) = (grid, graph);
            Place(_area);
        }

        Refresh(_triangles);
        Refresh(_uv);
        return "";
    }

    /// <summary>Whether code has a graph.</summary>
    private static bool Graphs(string source) => ShaderStack.RolesOf(source).Contains(StackRole.Graph);

    /// <remarks>
    /// An effect with a <c>[Grid]</c> or a graph gets one plain rect, cut by the renderer as a level item's
    /// would be; without one, the preview's own fine grid, so any deform has corners to bend.
    /// </remarks>
    public void Place(Rect2 area)
    {
        _area = area;
        // Everything over the backdrop stays on the preview: a deform or a graph's draws may land past it.
        CanvasServer.Current.SetCustomRect(_backdrop, true, area);
        CanvasServer.Current.SetClip(_backdrop, true);
        CanvasServer.Current.Clear(_backdrop);
        CanvasServer.Current.AddRect(_backdrop, area, Colors.White);
        foreach (Handle item in (ReadOnlySpan<Handle>)[_item, _triangles.Item, _uv.Item])
        {
            CanvasServer.Current.Clear(item);
            if (_grid is not null || _graph)
                CanvasServer.Current.AddRect(item, area, Colors.White);
            else
                PreviewMesh.Add(item, area, Colors.White);
        }
    }

    public void Update(in ToolFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        if (_material is not null)
            tuning.Apply(_material);
        // The overlays' deform reads the same values, so they land where the effect does.
        if (_triangles.Material is not null)
            tuning.Apply(_triangles.Material);
        if (_uv.Material is not null)
            tuning.Apply(_uv.Material);
    }

    public void Dispose()
    {
        if (_material is not null)
            _pipelines.Forget(_material);
        foreach (Overlay overlay in (ReadOnlySpan<Overlay>)[_triangles, _uv])
        {
            if (overlay.Material is not null)
                _pipelines.Forget(overlay.Material);
            CanvasServer.Current.Free(overlay.Item);
        }

        CanvasServer.Current.Free(_item);
    }
}
