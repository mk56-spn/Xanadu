using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Host.Rendering;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// A shipped effect on a 16:9 quad, compiled from the text being edited, not
/// the file, through the same wrapper as level shaders (its <c>shade</c> takes
/// <c>Canvas</c> and/or <c>ItemInput</c>; a <c>deform</c> bends the quad, drawn
/// as a grid so it has corners to move). Textures it declares
/// past the item's own draw white until the tool can feed them. Behind it the
/// backdrop effect (<see cref="ShaderFiles.Backdrop"/>) fills the same quad, so
/// an effect that reads the screen shows what it does to something. Over it,
/// on request, the triangles and where uv is 0 and 1 (<see cref="ShowOverlays"/>),
/// both drawn through the effect's own vertex stage.
/// </summary>
internal sealed class EffectPreview : IEditorPreview
{
    private readonly CanvasPipelines _pipelines;
    private readonly Handle _backdrop;
    private readonly Handle _item;
    private readonly string _label;
    private readonly bool _keepsState;
    private CanvasMaterial? _material;

    /// <summary>The overlays, drawn over the effect: the triangles' edges, and the uv lines.</summary>
    private readonly Overlay _triangles, _uv;

    /// <summary>The code last shown, which the overlays draw through.</summary>
    private string? _source;
    private Rect2 _area;
    private (int Columns, int Rows)? _grid;

    /// <summary>One overlay: its item and, while on, its material (the code with the overlay for shade).</summary>
    private sealed class Overlay(MaterialOverlay kind, Handle item)
    {
        public readonly MaterialOverlay Kind = kind;
        public readonly Handle Item = item;
        public bool On;
        public CanvasMaterial? Material;
    }

    /// <param name="item">Draws the backdrop; the effect goes in a child of it, so it draws over.</param>
    /// <param name="keepsState">A lab-only effect (<see cref="SlangShaders.IsLabOnly"/>): its update runs and shade reads state.</param>
    public EffectPreview(CanvasPipelines pipelines, Handle item, string label, bool keepsState)
    {
        _pipelines = pipelines;
        _keepsState = keepsState;
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
        "An effect: uniforms and float4 shade(Canvas canvas), as in a level shader; ItemInput i gives uv, colour and the rest.",
        "ItemVertex deform(ItemVertex v) moves the corners: v.position is target pixels.",
        "Ctrl+S saves to the file. Picking another shader keeps these edits until the tool closes.",
    ];

    public PreviewBuild Compile(string source)
    {
        PreviewBuild build = new(source, SlangShaders.CompileEffectBody(Label, source, _keepsState).Params);
        // Off the frame, so the overlays' first draw finds them in Slang's cache.
        if (_triangles.On)
            SlangShaders.CompileEffectBody(Label, source, _keepsState, SlangShaders.TrianglesOverlay);
        if (_uv.On)
            SlangShaders.CompileEffectBody(Label, source, _keepsState, SlangShaders.UvOverlay);
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
        overlay.Material = overlay.On && _source is { } source
            ? CanvasMaterial.FromSource(Label, source, keepsState: _keepsState, overlay: overlay.Kind)
            : null;
        CanvasServer.Current.SetVisible(overlay.Item, overlay.Material is not null);
        if (overlay.Material is not null)
            CanvasServer.Current.SetMaterial(overlay.Item, overlay.Material);
        if (previous is not null)
            _pipelines.Forget(previous);
    }

    public string Show(PreviewBuild build)
    {
        CanvasMaterial? previous = _material;
        _material = CanvasMaterial.FromSource(Label, (string)build.Result, keepsState: _keepsState);
        CanvasServer.Current.SetMaterial(_item, _material);
        if (previous is not null)
            _pipelines.Forget(previous);
        _source = (string)build.Result;
        (int, int)? grid = SlangShaders.GridOf(_source);
        if (grid != _grid)
        {
            _grid = grid;
            Place(_area);
        }

        Refresh(_triangles);
        Refresh(_uv);
        return "";
    }

    /// <remarks>
    /// An effect with a <c>[Grid]</c> gets one plain rect, cut by the renderer as a level item's
    /// would be; without one, the preview's own fine grid, so any deform has corners to bend.
    /// </remarks>
    public void Place(Rect2 area)
    {
        _area = area;
        CanvasServer.Current.Clear(_backdrop);
        CanvasServer.Current.AddRect(_backdrop, area, Colors.White);
        foreach (Handle item in (ReadOnlySpan<Handle>)[_item, _triangles.Item, _uv.Item])
        {
            CanvasServer.Current.Clear(item);
            if (_grid is not null)
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
