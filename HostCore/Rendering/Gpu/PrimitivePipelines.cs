using System.Collections.Generic;
using System.IO;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Shader = MoonWorks.Graphics.Shader;

namespace Xanadu.Host.Rendering.Gpu;

/// <summary>
/// The pipelines plain primitives draw with (primitives.slang): shaded, opaque for the
/// pre-pass, and depth-only for the depth effects read; made on first use per sample count.
/// </summary>
internal sealed class PrimitivePipelines : IDisposable
{
    /// <summary>Textures one primitive draw can read; a run starts again when it needs a ninth.</summary>
    public const int Slots = 8;

    private readonly GraphicsDevice _device;
    private readonly TextureFormat _format, _depthFormat;
    private readonly Shader _vertex, _shade, _opaque;
    private readonly Dictionary<(bool Opaque, SampleCount Samples), GraphicsPipeline> _made = [];
    private GraphicsPipeline? _depthOnly;

    public PrimitivePipelines(GraphicsDevice device, TextureFormat format, TextureFormat depthFormat)
    {
        _device = device;
        _format = format;
        _depthFormat = depthFormat;
        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "primitives.slang");
        _vertex = Load(module, "primitiveVertex", ShaderStage.Vertex, new ShaderCreateInfo { NumStorageBuffers = 1, NumUniformBuffers = 1 });
        _shade = Load(module, "primitiveShade", ShaderStage.Fragment, new ShaderCreateInfo { NumSamplers = Slots + 1 });
        _opaque = Load(module, "primitiveOpaque", ShaderStage.Fragment, new ShaderCreateInfo());
    }

    private Shader Load(string module, string entry, ShaderStage stage, ShaderCreateInfo info)
    {
        info.Format = ShaderFormat.SPIRV;
        info.Stage = stage;
        return Shader.Create(_device, SlangShaders.Compile(module, entry, stage == ShaderStage.Vertex ? "vertex" : "fragment"), "main", info);
    }

    /// <summary>The pipeline for a run of primitives, or the opaque pre-pass, at <paramref name="samples"/>.</summary>
    public GraphicsPipeline Get(bool opaque, SampleCount samples)
    {
        if (_made.TryGetValue((opaque, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _vertex,
            FragmentShader = opaque ? _opaque : _shade,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            DepthStencilState = new DepthStencilState
            {
                EnableDepthTest = true,
                EnableDepthWrite = opaque,
                CompareOp = CompareOp.Less,
            },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription
                    {
                        Format = _format,
                        BlendState = opaque ? ColorTargetBlendState.NoBlend : CanvasPipelines.Blend(BlendMode.Mix),
                    },
                ],
                HasDepthStencilTarget = true,
                DepthStencilFormat = _depthFormat,
            },
        });
        _made[(opaque, samples)] = pipeline;
        return pipeline;
    }

    /// <summary>The opaque pre-pass again with no colour, for the single-sampled depth effects read.</summary>
    public GraphicsPipeline DepthOnly => _depthOnly ??= GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
    {
        VertexShader = _vertex,
        FragmentShader = _opaque,
        VertexInputState = VertexInputState.Empty,
        PrimitiveType = PrimitiveType.TriangleList,
        RasterizerState = RasterizerState.CCW_CullNone,
        MultisampleState = new MultisampleState { SampleCount = SampleCount.One },
        DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = true, CompareOp = CompareOp.Less },
        TargetInfo = new GraphicsPipelineTargetInfo
        {
            ColorTargetDescriptions = [],
            HasDepthStencilTarget = true,
            DepthStencilFormat = _depthFormat,
        },
    });

    public void Dispose()
    {
        foreach (GraphicsPipeline pipeline in _made.Values)
            pipeline.Dispose();
        _depthOnly?.Dispose();
        _vertex.Dispose();
        _shade.Dispose();
        _opaque.Dispose();
    }
}
