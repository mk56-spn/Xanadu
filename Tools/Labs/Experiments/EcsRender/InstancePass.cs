using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Host.Text;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>The GPU's side: the instanced-quad pipeline, the instance buffer, and the pass that draws a <see cref="DrawList"/>.</summary>
internal sealed class InstancePass : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms
    {
        public float Width, Height, First, Pad;
    }

    private readonly Mw.GraphicsDevice _device;
    private readonly TextAtlas _atlas;
    private readonly Shader _vertex, _fragment;
    private readonly GraphicsPipeline _pipeline;
    private readonly Sampler _sampler;
    private Buffer? _buffer;
    private TransferBuffer? _transfer;
    private uint _capacity;

    public InstancePass(Mw.GraphicsDevice device, TextAtlas atlas, string module, Mw.TextureFormat format)
    {
        _device = device;
        _atlas = atlas;
        _vertex = Shader.Create(device, SlangShaders.Compile(module, "itemVertex", "vertex"), "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV,
            Stage = Mw.ShaderStage.Vertex,
            NumStorageBuffers = 1,
            NumUniformBuffers = 1,
        });
        _fragment = Shader.Create(device, SlangShaders.Compile(module, "itemShade", "fragment"), "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV,
            Stage = Mw.ShaderStage.Fragment,
            NumSamplers = 1,
        });
        _pipeline = GraphicsPipeline.Create(device, new Mw.GraphicsPipelineCreateInfo
        {
            VertexShader = _vertex,
            FragmentShader = _fragment,
            VertexInputState = Mw.VertexInputState.Empty,
            PrimitiveType = Mw.PrimitiveType.TriangleList,
            RasterizerState = Mw.RasterizerState.CCW_CullNone,
            MultisampleState = Mw.MultisampleState.None,
            DepthStencilState = Mw.DepthStencilState.Disable,
            TargetInfo = new Mw.GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = format, BlendState = Premultiplied }],
            },
        });
        _sampler = Sampler.Create(device, Mw.SamplerCreateInfo.PointClamp);
    }

    private static readonly Mw.ColorTargetBlendState Premultiplied = new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = Mw.BlendFactor.One,
        DstColorBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
        ColorBlendOp = Mw.BlendOp.Add,
        SrcAlphaBlendFactor = Mw.BlendFactor.One,
        DstAlphaBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
        AlphaBlendOp = Mw.BlendOp.Add,
        EnableColorWriteMask = true,
        ColorWriteMask = Mw.ColorComponentFlags.RGBA,
    };

    /// <summary>The upload buffer, with room for <paramref name="count"/> instances; <see cref="Unmap"/> when written.</summary>
    public Span<DrawInstance> Map(int count)
    {
        if (_buffer is null || count > _capacity)
        {
            _buffer?.Dispose();
            _transfer?.Dispose();
            _capacity = Math.Max((uint)count, Math.Max(_capacity * 2, 1u << 14));
            _buffer = Buffer.Create<DrawInstance>(_device, "ecs render instances", Mw.BufferUsageFlags.GraphicsStorageRead, _capacity);
            _transfer = TransferBuffer.Create<DrawInstance>(_device, "ecs render upload", Mw.TransferBufferUsage.Upload, _capacity);
        }

        return _transfer!.Map<DrawInstance>(cycle: true);
    }

    public void Unmap() => _transfer!.Unmap();

    public void Draw(Mw.CommandBuffer commands, Texture target, DrawList list)
    {
        // A draw of shapes alone still needs a texture bound, though it never reads it; with no atlas yet there is none to bind.
        bool drawn = list.Draws.Count > 0 && _atlas.PageCount > 0;
        if (drawn)
        {
            Mw.CopyPass copy = commands.BeginCopyPass();
            copy.UploadToBuffer<DrawInstance>(_transfer!, _buffer!, 0, 0, (uint)list.Count, cycle: true);
            commands.EndCopyPass(copy);
        }

        Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(target, new Mw.Color(0.02f, 0.025f, 0.045f, 1f)));
        if (drawn)
        {
            pass.BindGraphicsPipeline(_pipeline);
            pass.BindVertexStorageBuffers(_buffer!);
            foreach ((uint start, uint count, short page) in list.Draws)
            {
                pass.BindFragmentSamplers(new Mw.TextureSamplerBinding(_atlas.PageTexture(Math.Max((short)0, page)), _sampler));
                commands.PushVertexUniformData(new FrameUniforms { Width = list.Stage.X, Height = list.Stage.Y, First = start });
                pass.DrawPrimitives(6, count, 0, 0);
            }
        }

        commands.EndRenderPass(pass);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _vertex.Dispose();
        _fragment.Dispose();
        _sampler.Dispose();
        _buffer?.Dispose();
        _transfer?.Dispose();
    }
}
