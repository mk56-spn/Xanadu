using System.Collections.Generic;
using MoonWorks.Graphics;
using Xanadu.Rendering.Compute;
using Xanadu.Slang;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Rendering;

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

    /// <summary>Its passes and what they share, in the order they run: what the renderer walks each frame.</summary>
    public StackGraph Graph { get; } = StackGraph.Of(compiled);

    private readonly Dictionary<(int Pass, TextureFormat Colour, bool Depth, TextureFormat Z), GraphicsPipeline> _drawPipelines = [];

    /// <summary>
    /// Draw <paramref name="pass"/>'s pipeline into a colour image of <paramref name="colour"/> (and an r32f depth image when
    /// <paramref name="depth"/>), tested against a depth of <paramref name="z"/>: made the first time, kept with the effect.
    /// </summary>
    public GraphicsPipeline DrawPipeline(GraphicsDevice device, int pass, TextureFormat colour, bool depth, TextureFormat z)
    {
        if (_drawPipelines.TryGetValue((pass, colour, depth, z), out GraphicsPipeline? made))
            return made;
        (Shader vertexStage, Shader fragmentStage) = Draws[pass]!.Value;
        ColorTargetDescription Target(TextureFormat format) => new() { Format = format, BlendState = ColorTargetBlendState.NoBlend };
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
                ColorTargetDescriptions = depth ? [Target(colour), Target(TextureFormat.R32Float)] : [Target(colour)],
                HasDepthStencilTarget = true,
                DepthStencilFormat = z,
            },
        });
        _drawPipelines[(pass, colour, depth, z)] = made;
        return made;
    }

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _drawPipelines.Values)
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
