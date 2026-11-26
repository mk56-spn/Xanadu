using System.Collections.Generic;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Rendering;

/// <summary>An effect's copied form ready to draw (<see cref="SlangCopies"/>): its two stages and what they bind.</summary>
internal sealed class CopiedEffect(Shader vertex, Shader fragment, CompiledCopies compiled, bool deforms) : IDisposable
{
    public Shader Vertex { get; } = vertex;
    public Shader Fragment { get; } = fragment;
    public CompiledCopies Compiled { get; } = compiled;

    /// <summary>Whether the stack moves its own corners, and so sets depths within its slice.</summary>
    public bool Deforms { get; } = deforms;

    public void Dispose()
    {
        Vertex.Dispose();
        Fragment.Dispose();
    }
}

/// <summary>
/// Effects' copied forms, compiled the first time something draws copies through them, and their pipelines: the
/// copied vertex stage reads the mesh as canvas vertices, depth tested as the effect itself is.
/// </summary>
internal sealed class CopiesPipelines(GraphicsDevice device, TextureFormat format, TextureFormat depthFormat) : IDisposable
{
    /// <summary>Vertex uniform buffers the copied stage binds: the view (0), none at 1, the frame (2).</summary>
    private const uint VertexUniforms = 3;

    private readonly Dictionary<CanvasEffect, CopiedEffect?> _effects = [];
    private readonly Dictionary<(CopiedEffect, BlendMode, CustomBlend?, SampleCount), GraphicsPipeline> _pipelines = [];

    /// <summary>The copied form of <paramref name="effect"/> (compiled as <paramref name="name"/>); null when it has none or won't build.</summary>
    public CopiedEffect? Of(CanvasEffect effect, string name)
    {
        if (_effects.TryGetValue(effect, out CopiedEffect? known))
            return known;

        CopiedEffect? copied = null;
        if (SlangCopies.Refusal(effect.Compiled) is { } why)
            Console.Error.WriteLine($"Shader {name} can't draw copies ({why}); they are left out.");
        else
        {
            try
            {
                CompiledCopies compiled = SlangCopies.Compile(name, effect.Compiled);
                copied = new CopiedEffect(Make(compiled.Vertex, ShaderStage.Vertex, VertexUniforms), Make(compiled.Fragment, ShaderStage.Fragment,
                    compiled.Fragment.UniformBuffers), compiled, effect.Vertex is not null);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Shader {name}'s copied form failed to compile; its copies are left out.\n{exception.Message}");
            }
        }

        return _effects[effect] = copied;
    }

    private Shader Make(CompiledShader stage, ShaderStage kind, uint uniforms) => Shader.Create(device, stage.Spirv, "main", new ShaderCreateInfo
    {
        Format = ShaderFormat.SPIRV,
        Stage = kind,
        NumSamplers = (uint)stage.Samplers.Length,
        NumStorageBuffers = (uint)stage.StorageBuffers,
        NumUniformBuffers = uniforms,
    });

    /// <summary>The pipeline drawing <paramref name="copied"/> with the material's blend into a target with <paramref name="samples"/>.</summary>
    public GraphicsPipeline Pipeline(CopiedEffect copied, CanvasMaterial? material, SampleCount samples)
    {
        BlendMode blend = material?.Blend ?? BlendMode.Mix;
        CustomBlend? custom = material?.Custom?.PipelineKey;
        if (_pipelines.TryGetValue((copied, blend, custom, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
        {
            VertexShader = copied.Vertex,
            FragmentShader = copied.Fragment,
            VertexInputState = VertexInputState.CreateSingleBinding<CanvasVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            // As the effect's own pipeline: a deform writes depth within its slice (unless [Translucent]), the rest only test it.
            DepthStencilState = new DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = copied.Deforms && !copied.Compiled.Vertex.Translucent,
                CompareOp = copied.Deforms ? CompareOp.LessOrEqual : CompareOp.Less,
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription { Format = format, BlendState = custom is { } state ? CanvasPipelines.Blend(state) : CanvasPipelines.Blend(blend) },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = depthFormat,
            },
        });
        _pipelines[(copied, blend, custom, samples)] = pipeline;
        return pipeline;
    }

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _pipelines.Values)
            pipeline.Dispose();
        foreach (CopiedEffect? effect in _effects.Values)
            effect?.Dispose();
    }
}
