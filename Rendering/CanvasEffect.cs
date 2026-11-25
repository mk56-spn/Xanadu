using System.Collections.Generic;
using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Rendering.Compute;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Rendering;

/// <summary>An image a graph's items get, as the GPU makes it (<see cref="GraphPlan.Images"/>).</summary>
/// <param name="Source">For a history, the place of the image it is last frame's copy of; -1 otherwise.</param>
/// <param name="Material">The material's, one for all its items, rather than each item's.</param>
internal readonly record struct GraphImage(string Name, TextureFormat Format, float Clear, ImageLife Life, int Source, ImageSize Size, bool Material);

/// <summary>A fragment effect ready to draw: its shader object and what it binds.</summary>
internal sealed class CanvasEffect(
    Shader shader, CompiledShader compiled, ComputePipeline? update = null, Shader? vertex = null, ComputeOperation<Passes.PoolJob>? pool = null,
    ComputePipeline?[]? passes = null, ComputePipeline? trace = null, (Shader Vertex, Shader Fragment)?[]? draws = null)
    : IDisposable
{
    public Shader Shader { get; } = shader;
    public CompiledShader Compiled { get; } = compiled;

    /// <summary>Its own vertex stage, from its <c>deform</c>; null draws with the standard one.</summary>
    public Shader? Vertex { get; } = vertex;

    /// <summary>A kept-state effect's once-a-frame <c>update</c>; null when it keeps none.</summary>
    public ComputePipeline? Update { get; } = update;

    /// <summary>A pooled stack's step (its STEP brick): one job a material, run before the draws; null without a pool.</summary>
    public ComputeOperation<Passes.PoolJob>? Pool { get; } = pool;

    /// <summary>A graph's compute passes, in the order it declares them (a schedule's call names one by its place); null for a draw.</summary>
    public ComputePipeline?[] Passes { get; } = passes ?? [];

    /// <summary>A graph's draws' stages, by the same places as <see cref="Passes"/>; null for a compute pass.</summary>
    public (Shader Vertex, Shader Fragment)?[] Draws { get; } = draws ?? [];

    /// <summary>A TRACE brick's compute stage, a thread a pixel of the quad; null without one.</summary>
    public ComputePipeline? Trace { get; } = trace;

    /// <summary>Its graph lowered at the default values: what a stack with no schedule runs every frame.</summary>
    public GraphPlan Plan { get; } = GraphPlan.Of(compiled);

    /// <summary>The graph's images (<see cref="GraphPlan.Images"/>), as the GPU makes them.</summary>
    public GraphImage[] Images { get; } =
        [.. GraphPlan.Images(compiled).Select(image =>
            new GraphImage(image.Name, FormatOf(image.Format), image.Clear, image.Life, image.Source, image.Size, image.Material))];

    /// <summary>Whether an item's image outlives the frame (<c>[Kept]</c>, <c>[History]</c>), so each item holds some of its own.</summary>
    public bool HoldsImages { get; } = GraphPlan.Images(compiled).Any(image => image.Life != ImageLife.Frame && !image.Material);

    /// <summary>Whether any image is the material's, so the material holds a set for its items to share.</summary>
    public bool SharesImages { get; } = GraphPlan.Images(compiled).Any(image => image.Material);

    /// <summary>Whether its graph draws into its images, so each item needs a draw depth too.</summary>
    public bool DrawsImages { get; } = compiled.Build?.Passes.Any(pass => pass.Drawn is not null) == true;

    /// <summary>An image's texel format, from the name Slang gives it.</summary>
    private static TextureFormat FormatOf(string format) => format switch
    {
        "rgba16f" => TextureFormat.R16G16B16A16Float,
        "rgba8" => TextureFormat.R8G8B8A8Unorm,
        "rgba32f" => TextureFormat.R32G32B32A32Float,
        "rg16f" => TextureFormat.R16G16Float,
        "r16f" => TextureFormat.R16Float,
        "r32f" => TextureFormat.R32Float,
        _ => TextureFormat.R32Uint,
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
        var descriptions = new ColorTargetDescription[targets.Length];
        for (int k = 0; k < descriptions.Length; k++)
            descriptions[k] = new ColorTargetDescription { Format = targets[k], BlendState = ColorTargetBlendState.NoBlend };
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
    /// Draw <paramref name="pass"/>'s pipeline onto the frame (an exit): blended as <paramref name="material"/> says, depth
    /// tested and written as a deforming effect's is, at the job's sample count. Made the first time, kept with the effect.
    /// </summary>
    public GraphicsPipeline ExitPipeline(GraphicsDevice device, int pass, CanvasMaterial? material, SampleCount samples, TextureFormat format,
        TextureFormat z)
    {
        BlendMode blend = material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = material?.Custom?.PipelineKey;
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
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = true, CompareOp = CompareOp.LessOrEqual },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = format,
                        BlendState = custom is { } state ? CanvasPipelines.Blend(state) : CanvasPipelines.Blend(blend),
                    },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = z,
            },
        });
        _exitPipelines[(pass, blend, custom, samples)] = made;
        return made;
    }

    public void Dispose()
    {
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
        Trace?.Dispose();
        Update?.Dispose();
        Vertex?.Dispose();
        Pool?.Dispose();
    }
}
