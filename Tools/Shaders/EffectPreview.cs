using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Systems.Canvas;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// A shipped effect on a 16:9 quad, compiled from the text being edited, not
/// the file, through the same wrapper as level shaders (its <c>shade</c> takes
/// <c>Canvas</c> and/or <c>ItemInput</c>). Textures it declares
/// past the item's own draw white until the tool can feed them. Behind it the
/// backdrop effect (<see cref="ShaderFiles.Backdrop"/>) fills the same quad, so
/// an effect that reads the screen shows what it does to something.
/// </summary>
internal sealed class EffectPreview : IEditorPreview
{
    private readonly CanvasPipelines _pipelines;
    private readonly Handle _backdrop;
    private readonly Handle _item;
    private readonly string _label;
    private CanvasMaterial? _material;

    /// <param name="item">Draws the backdrop; the effect goes in a child of it, so it draws over.</param>
    public EffectPreview(CanvasPipelines pipelines, Handle item, string label)
    {
        _pipelines = pipelines;
        _backdrop = item;
        _label = label;
        _item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(_item, item);
        CanvasServer.Current.SetMaterial(_backdrop, new CanvasMaterial(ShaderFiles.Backdrop));
    }

    public string Label => _label;

    public IEnumerable<(string Name, string Type, string? Note)> Provided => [];

    public string[] Help =>
    [
        "An effect: uniforms and float4 shade(Canvas canvas), as in a level shader; ItemInput i gives uv, colour and the rest.",
        "Ctrl+S saves to the file. Picking another shader keeps these edits until the tool closes.",
    ];

    public PreviewBuild Compile(string source) =>
        new(source, SlangShaders.CompileEffectBody(Label, source).Params);

    public string Show(PreviewBuild build)
    {
        CanvasMaterial? previous = _material;
        _material = CanvasMaterial.FromSource(Label, (string)build.Result);
        CanvasServer.Current.SetMaterial(_item, _material);
        if (previous is not null)
            _pipelines.Forget(previous);
        return "";
    }

    public void Place(Rect2 area)
    {
        CanvasServer.Current.Clear(_backdrop);
        CanvasServer.Current.AddRect(_backdrop, area, Colors.White);
        CanvasServer.Current.Clear(_item);
        CanvasServer.Current.AddRect(_item, area, Colors.White);
    }

    public void Update(in ToolFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        if (_material is not null)
            tuning.Apply(_material);
    }

    public void Dispose()
    {
        if (_material is not null)
            _pipelines.Forget(_material);
        CanvasServer.Current.Free(_item);
    }
}
