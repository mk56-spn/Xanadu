using System.Collections.Generic;
using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Rendering;

/// <summary>An image a graph's items get, as the GPU makes it (<see cref="GraphPlan.Images"/>).</summary>
/// <param name="Source">For a history, the place of the image it is last frame's copy of; -1 otherwise.</param>
/// <param name="Material">The material's, one for all its items, rather than each item's.</param>
/// <param name="Shared">The frame's (<c>[Shared]</c>): one for every graph declaring it, the screen's size.</param>
/// <param name="View">The host's (<c>[View]</c>): the canvas subtree its material names, drawn before the graph runs.</param>
internal readonly record struct GraphImage(string Name, TextureFormat Format, Vector4 Clear, ImageLife Life, int Source, ImageSize Size, bool Material,
    bool Shared, bool View)
{
    /// <summary>Not an item's: shared or a view, the screen's size, taken with the material's set.</summary>
    public bool Screen => Shared || View;
}

/// <summary>A fragment effect ready to draw: its shader object and what it binds.</summary>
internal sealed class CanvasEffect(
    Shader shader, CompiledShader compiled, Shader? vertex = null, ComputePipeline?[]? passes = null, (Shader Vertex, Shader Fragment)?[]? draws = null)
    : IDisposable
{
    public Shader Shader { get; } = shader;
    public CompiledShader Compiled { get; } = compiled;

    /// <summary>Its own vertex stage, from its <c>deform</c>; null draws with the standard one.</summary>
    public Shader? Vertex { get; } = vertex;

    /// <summary>A graph's compute passes, in the order it declares them (a schedule's call names one by its place); null for a draw.</summary>
    public ComputePipeline?[] Passes { get; } = passes ?? [];

    /// <summary>A graph's draws' stages, by the same places as <see cref="Passes"/>; null for a compute pass.</summary>
    public (Shader Vertex, Shader Fragment)?[] Draws { get; } = draws ?? [];

    /// <summary>Its graph lowered at the default values: what a stack with no schedule runs every frame.</summary>
    public GraphPlan Plan { get; } = GraphPlan.Of(compiled);

    /// <summary>The graph's images (<see cref="GraphPlan.Images"/>), as the GPU makes them.</summary>
    public GraphImage[] Images { get; } =
        [.. GraphPlan.Images(compiled).Select(image =>
            new GraphImage(image.Name, FormatOf(image.Format), image.Clear, image.Life, image.Source, image.Size, image.Material, image.Shared, image.View))];

    /// <summary>Whether an item's image outlives the frame (<c>[Kept]</c>, <c>[History]</c>), so each item holds some of its own.</summary>
    public bool HoldsImages { get; } = GraphPlan.Images(compiled).Any(image => image.Life != ImageLife.Frame && !image.Material);

    /// <summary>
    /// Whether any image is the material's or the frame's (<c>[Shared]</c>, <c>[View]</c>), so the material takes a set of
    /// them a frame for its <c>[Pass]</c>es and items.
    /// </summary>
    public bool HasMaterialSet { get; } = GraphPlan.Images(compiled).Any(image => image.Material || image.Shared || image.View);

    /// <summary>The <c>[Shared]</c> lists and images its graph writes and reads, which order it against other materials.</summary>
    public SharedUse Shared => Compiled.Build?.Shared ?? SharedUse.None;

    /// <summary>Whether its graph draws into its images, so each item needs a draw depth too.</summary>
    public bool DrawsImages { get; } = compiled.Build?.Passes.Any(pass => pass.Drawn is not null) == true;

    /// <summary>An image's texel format, from the name Slang gives it.</summary>
    private static TextureFormat FormatOf(string format) => format switch
    {
        "rgba16f" => TextureFormat.R16G16B16A16Float,
        "rgba8" => TextureFormat.R8G8B8A8Unorm,
        "rgba32f" => TextureFormat.R32G32B32A32Float,
        "rg16f" => TextureFormat.R16G16Float,
        "rg32f" => TextureFormat.R32G32Float,
        "rg8" => TextureFormat.R8G8Unorm,
        "r16f" => TextureFormat.R16Float,
        "r32f" => TextureFormat.R32Float,
        "r32ui" => TextureFormat.R32Uint,
        "rg32ui" => TextureFormat.R32G32Uint,
        "rgba32ui" => TextureFormat.R32G32B32A32Uint,
        "r32i" => TextureFormat.R32Int,
        "rg32i" => TextureFormat.R32G32Int,
        "rgba32i" => TextureFormat.R32G32B32A32Int,
        _ => throw new ArgumentException($"no texture format for {format}"),
    };

    private readonly Dictionary<(int Pass, TextureFormat A, TextureFormat B, TextureFormat C, TextureFormat D, TextureFormat Z), GraphicsPipeline> _drawPipelines = [];

    /// <summary>
    /// Draw <paramref name="pass"/>'s pipeline into colour targets of <paramref name="targets"/> (one to three), tested
    /// against a depth of <paramref name="z"/>: made the first time, kept with the effect.
    /// </summary>
    public GraphicsPipeline DrawPipeline(GraphicsDevice device, int pass, ReadOnlySpan<TextureFormat> targets, TextureFormat z)
    {
        TextureFormat Format(ReadOnlySpan<TextureFormat> all, int k) => k < all.Length ? all[k] : TextureFormat.Invalid;
        var key = (pass, Format(targets, 0), Format(targets, 1), Format(targets, 2), Format(targets, 3), z);
        if (_drawPipelines.TryGetValue(key, out GraphicsPipeline? made))
            return made;
        BlendMode? blend = Compiled.Build!.Passes[pass].Drawn!.Blend;
        ColorTargetBlendState state = blend is { } mode ? CanvasPipelines.Blend(mode) : ColorTargetBlendState.NoBlend;
        var descriptions = new ColorTargetDescription[targets.Length];
        for (int k = 0; k < descriptions.Length; k++)
            descriptions[k] = new ColorTargetDescription { Format = targets[k], BlendState = state };
        (Shader vertexStage, Shader fragmentStage) = Draws[pass]!.Value;
        made = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertexStage,
            FragmentShader = fragmentStage,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = !Compiled.Build!.Passes[pass].Drawn!.Translucent, CompareOp = CompareOp.LessOrEqual },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = descriptions,
                HasDepthStencilTarget = true,
                DepthStencilFormat = z,
            },
        });
        _drawPipelines[key] = made;
        return made;
    }

    private readonly Dictionary<(int Pass, BlendMode Blend, CustomBlend? Custom, SampleCount Samples), GraphicsPipeline> _exitPipelines = [];

    /// <summary>
    /// Draw <paramref name="pass"/>'s pipeline onto the frame (an exit): blended as its <c>blend:</c> says, else as
    /// <paramref name="material"/> says, depth
    /// tested and written as a deforming effect's is, at the job's sample count. Made the first time, kept with the effect.
    /// </summary>
    public GraphicsPipeline ExitPipeline(GraphicsDevice device, int pass, CanvasMaterial? material, SampleCount samples, TextureFormat format,
        TextureFormat z)
    {
        CompiledDraw drawn = Compiled.Build!.Passes[pass].Drawn!;
        BlendMode? own = drawn.Blend;
        BlendMode blend = own ?? material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = own is null ? material?.Custom?.PipelineKey : null;
        if (_exitPipelines.TryGetValue((pass, blend, custom, samples), out GraphicsPipeline? made))
            return made;
        (Shader vertexStage, Shader fragmentStage) = Draws[pass]!.Value;
        made = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertexStage,
            FragmentShader = fragmentStage,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = !drawn.Translucent, CompareOp = CompareOp.LessOrEqual },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = format,
                        BlendState = drawn.Opaque ? ColorTargetBlendState.NoBlend
                            : custom is { } state ? CanvasPipelines.Blend(state) : CanvasPipelines.Blend(blend),
                    },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = z,
            },
        });
        _exitPipelines[(pass, blend, custom, samples)] = made;
        return made;
    }

    private readonly Dictionary<int, GraphicsPipeline> _depthPipelines = [];

    /// <summary>An <c>[Opaque]</c> draw <paramref name="pass"/> with no colour: its depth alone, single sampled, for the depth effects read.</summary>
    public GraphicsPipeline DepthPipeline(GraphicsDevice device, int pass, TextureFormat z)
    {
        if (_depthPipelines.TryGetValue(pass, out GraphicsPipeline? made))
            return made;
        if (!Compiled.Build!.Passes[pass].Drawn!.Opaque)
            throw new InvalidOperationException($"{Compiled.Build.Passes[pass].Name} isn't [Opaque], so it can't be drawn depth only.");
        (Shader vertexStage, Shader fragmentStage) = Draws[pass]!.Value;
        made = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertexStage,
            FragmentShader = fragmentStage,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = true, CompareOp = CompareOp.LessOrEqual },
            TargetInfo = new GraphicsPipelineTargetInfo { ColorTargetDescriptions = [], HasDepthStencilTarget = true, DepthStencilFormat = z },
        });
        _depthPipelines[pass] = made;
        return made;
    }

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _depthPipelines.Values)
            pipeline.Dispose();
        foreach (GraphicsPipeline pipeline in _drawPipelines.Values)
            pipeline.Dispose();
        foreach (GraphicsPipeline pipeline in _exitPipelines.Values)
            pipeline.Dispose();
        foreach (ComputePipeline? pass in Passes)
            pass?.Dispose();
        foreach ((Shader Vertex, Shader Fragment)? draw in Draws)
        {
            draw?.Vertex.Dispose();
            draw?.Fragment.Dispose();
        }

        Shader.Dispose();
        Vertex?.Dispose();
    }
}
