using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// Runs the Slang paint shader over a Game of Life state texture, into a texture
/// the canvas samples directly.
/// </summary>
/// <remarks>
/// Each state gets a uniform set made up front, so a generation swap only changes
/// which one is bound. Everything touching the device is queued on the render
/// thread, after the blit that wrote the state.
/// </remarks>
internal sealed class RdLifePainter
{
    private const string VertexPath = "res://Diagnostics/RdLife.vert.spv";
    private const string FragmentPath = "res://Diagnostics/RdLife.frag.spv";

    private readonly int _size;
    private readonly Rid[] _states;
    private readonly Rid[] _uniformSets = new Rid[2];
    private RenderingDevice? _device;
    private Rid _shader;
    private Rid _sampler;
    private Rid _vertexBuffer;
    private Rid _vertexArray;
    private Rid _target;
    private Rid _framebuffer;
    private Rid _pipeline;

    public RdLifePainter(int size, Rid stateA, Rid stateB)
    {
        _size = size;
        _states = new[] { stateA, stateB };
        RenderingServer.CallOnRenderThread(Callable.From(Create));
    }

    public Texture2Drd Texture { get; } = new();

    public void Draw(int state) =>
        RenderingServer.CallOnRenderThread(Callable.From(() => DrawOnRenderThread(state)));

    public void Free()
    {
        Texture.TextureRdRid = new Rid();
        RenderingServer.CallOnRenderThread(Callable.From(FreeOnRenderThread));
    }

    private void Create()
    {
        RenderingDevice? device = RenderingServer.GetRenderingDevice();
        if (device is null)
        {
            GD.PushError("RdLife: no RenderingDevice.");
            return;
        }

        byte[] vertex = FileAccess.GetFileAsBytes(VertexPath);
        byte[] fragment = FileAccess.GetFileAsBytes(FragmentPath);
        if (vertex.Length == 0 || fragment.Length == 0)
        {
            GD.PushError("RdLife: compiled shaders are missing; see RdLife.slang for the slangc commands.");
            return;
        }

        var spirv = new RDShaderSpirV { BytecodeVertex = vertex, BytecodeFragment = fragment };
        _shader = device.ShaderCreateFromSpirV(spirv, "rd_life_paint");

        long vertexFormat = device.VertexFormatCreate(
            new Godot.Collections.Array<RDVertexAttribute>
            {
                new()
                {
                    Location = 0,
                    Format = RenderingDevice.DataFormat.R32G32Sfloat,
                    Offset = 0,
                    Stride = 8,
                },
            });

        float[] corners = { -1f, -1f, 1f, -1f, 1f, 1f, -1f, -1f, 1f, 1f, -1f, 1f };
        var bytes = new byte[corners.Length * sizeof(float)];
        System.Buffer.BlockCopy(corners, 0, bytes, 0, bytes.Length);

        _vertexBuffer = device.VertexBufferCreate((uint)bytes.Length, bytes);
        _vertexArray = device.VertexArrayCreate(
            6, vertexFormat, new Godot.Collections.Array<Rid> { _vertexBuffer });

        var textureFormat = new RDTextureFormat
        {
            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
            Width = (uint)_size,
            Height = (uint)_size,
            UsageBits = RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                        RenderingDevice.TextureUsageBits.SamplingBit,
        };
        _target = device.TextureCreate(textureFormat, new RDTextureView());

        var attachment = new RDAttachmentFormat
        {
            Format = textureFormat.Format,
            Samples = RenderingDevice.TextureSamples.Samples1,
            UsageFlags = (uint)textureFormat.UsageBits,
        };
        long framebufferFormat = device.FramebufferFormatCreate(
            new Godot.Collections.Array<RDAttachmentFormat> { attachment });
        _framebuffer = device.FramebufferCreate(new Godot.Collections.Array<Rid> { _target });

        // Nearest, so a cell reads as one solid value across its whole square.
        _sampler = device.SamplerCreate(new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Nearest,
            MagFilter = RenderingDevice.SamplerFilter.Nearest,
        });

        for (int i = 0; i < _states.Length; i++)
        {
            var uniform = new RDUniform
            {
                UniformType = RenderingDevice.UniformType.SamplerWithTexture,
                Binding = 0,
            };
            uniform.AddId(_sampler);
            uniform.AddId(RenderingServer.TextureGetRdTexture(_states[i]));
            _uniformSets[i] = device.UniformSetCreate(
                new Godot.Collections.Array<RDUniform> { uniform }, _shader, 0);
        }

        var colorBlend = new RDPipelineColorBlendState
        {
            Attachments = new Godot.Collections.Array<RDPipelineColorBlendStateAttachment> { new() },
        };
        _pipeline = device.RenderPipelineCreate(
            _shader,
            framebufferFormat,
            vertexFormat,
            RenderingDevice.RenderPrimitive.Triangles,
            new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled },
            new RDPipelineMultisampleState(),
            new RDPipelineDepthStencilState(),
            colorBlend);

        _device = device;
        Texture.TextureRdRid = _target;
    }

    private void DrawOnRenderThread(int state)
    {
        if (_device is null)
            return;

        long drawList = _device.DrawListBegin(
            _framebuffer,
            RenderingDevice.DrawFlags.ClearColor0,
            new[] { Colors.Black });
        _device.DrawListBindRenderPipeline(drawList, _pipeline);
        _device.DrawListBindUniformSet(drawList, _uniformSets[state], 0);
        _device.DrawListBindVertexArray(drawList, _vertexArray);
        _device.DrawListDraw(drawList, false, 1);
        _device.DrawListEnd();
    }

    private void FreeOnRenderThread()
    {
        RenderingDevice? device = RenderingServer.GetRenderingDevice();
        if (device is null)
            return;

        _device = null;
        foreach (Rid uniformSet in _uniformSets)
        {
            if (uniformSet.IsValid && device.UniformSetIsValid(uniformSet))
                device.FreeRid(uniformSet);
        }

        FreeIfValid(device, _framebuffer);
        FreeIfValid(device, _target);
        FreeIfValid(device, _sampler);
        FreeIfValid(device, _pipeline);
        FreeIfValid(device, _vertexArray);
        FreeIfValid(device, _vertexBuffer);
        FreeIfValid(device, _shader);
    }

    private static void FreeIfValid(RenderingDevice device, Rid rid)
    {
        if (rid.IsValid)
            device.FreeRid(rid);
    }
}
