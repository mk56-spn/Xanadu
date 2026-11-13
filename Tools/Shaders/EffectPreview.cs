using System.Collections.Generic;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Slang;
using Xanadu.Systems.Canvas;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// A whole shipped effect (its own <c>shade(ItemVaryings v)</c>) on a 16:9 quad,
/// compiled from the text being edited, not the file. Textures it declares
/// past the item's own draw white until the tool can feed them.
/// </summary>
internal sealed class WholeEffectPreview(CanvasPipelines pipelines, Handle item, string label) : IEditorPreview
{
    private CanvasMaterial? _material;

    public string Label => label;

    public IEnumerable<(string Name, string Type, string? Note)> Provided => [];

    public string[] Help =>
    [
        "A whole effect: [shader(\"fragment\")] float4 shade(ItemVaryings v) is its entry; import xanadu.* for the library.",
        "Ctrl+S saves to the file. Picking another shader keeps these edits until the tool closes.",
    ];

    public PreviewBuild Compile(string source) =>
        new(source, SlangShaders.CompileWholeEffect(Label, source).Params);

    public string Show(PreviewBuild build)
    {
        CanvasMaterial? previous = _material;
        _material = CanvasMaterial.FromEffect(Label, (string)build.Result);
        CanvasServer.Current.SetMaterial(item, _material);
        if (previous is not null)
            pipelines.Forget(previous);
        return "";
    }

    public void Place(Rect2 area)
    {
        CanvasServer.Current.Clear(item);
        CanvasServer.Current.AddRect(item, area, Colors.White);
    }

    public void Update(in ToolFrame frame, Vector2 pointer, EditorTuning tuning)
    {
        if (_material is not null)
            tuning.Apply(_material);
    }

    public void Dispose()
    {
        if (_material is not null)
            pipelines.Forget(_material);
    }
}
