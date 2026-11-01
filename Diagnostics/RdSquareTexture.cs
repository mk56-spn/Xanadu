using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// Draws the orange square on the main <see cref="RenderingDevice"/> into a
/// texture the canvas samples directly, so a canvas item can composite it
/// between two RenderingServer quads.
/// </summary>
/// <remarks>
/// The main device is render-thread only, so everything that touches it is
/// queued with <see cref="RenderingServer.CallOnRenderThread"/>. Those calls run
/// before the frame's canvas pass, and <see cref="Texture2Drd"/> shares the
/// texture with the canvas without going through the CPU.
/// The shader is RdSquare.slang's precompiled SPIR-V when it has been built, and
/// a plain orange GLSL fallback otherwise.
/// </remarks>
internal sealed class RdSquareTexture
{
    private const string SlangVertexPath = "res://Diagnostics/RdSquare.vert.spv";
    private const string SlangFragmentPath = "res://Diagnostics/RdSquare.frag.spv";
    private const int PushConstantSize = 28;

    private readonly int _size;
    private bool _animated;
    private RenderingDevice? _device;
    private Rid _shader;
    private Rid _vertexBuffer;
    private Rid _vertexArray;
    private Rid _target;
    private Rid _framebuffer;
    private Rid _pipeline;

    public RdSquareTexture(int size)
    {
        _size = size;
        RenderingServer.CallOnRenderThread(Callable.From(Create));
    }

    public Texture2Drd Texture { get; } = new();

    public void Draw() => RenderingServer.CallOnRenderThread(Callable.From(DrawOnRenderThread));

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
            GD.PushError("RdSquare: no RenderingDevice.");
            return;
        }

        RDShaderSpirV? slang = LoadSlang();
        _animated = slang is not null;
        RDShaderSpirV spirv = slang ?? CompileGlsl(device);
        if (!CheckStage(spirv, RenderingDevice.ShaderStage.Vertex) ||
            !CheckStage(spirv, RenderingDevice.ShaderStage.Fragment))
        {
            return;
        }

        _shader = device.ShaderCreateFromSpirV(spirv, "rd_square_texture");

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

        // Unorm, not Srgb: the canvas samples these texels as they are, and an
        // Srgb target would gamma-encode the colour on write and wash it out.
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
        _framebuffer = device.FramebufferCreate(
            new Godot.Collections.Array<Rid> { _target });
        if (!_framebuffer.IsValid)
        {
            GD.PushError("RdSquare: framebuffer is invalid.");
            return;
        }

        var rasterization = new RDPipelineRasterizationState
        {
            CullMode = RenderingDevice.PolygonCullMode.Disabled,
        };
        var depthStencil = new RDPipelineDepthStencilState
        {
            EnableDepthTest = false,
            EnableDepthWrite = false,
        };
        var colorBlend = new RDPipelineColorBlendState
        {
            Attachments = new Godot.Collections.Array<RDPipelineColorBlendStateAttachment> { new() },
        };

        _pipeline = device.RenderPipelineCreate(
            _shader,
            framebufferFormat,
            vertexFormat,
            RenderingDevice.RenderPrimitive.Triangles,
            rasterization,
            new RDPipelineMultisampleState(),
            depthStencil,
            colorBlend);

        _device = device;
        Texture.TextureRdRid = _target;
    }

    private void DrawOnRenderThread()
    {
        if (_device is null)
            return;

        long drawList = _device.DrawListBegin(
            _framebuffer,
            RenderingDevice.DrawFlags.ClearColor0,
            new[] { new Color(0f, 0f, 0f, 0f) });
        _device.DrawListBindRenderPipeline(drawList, _pipeline);
        _device.DrawListBindVertexArray(drawList, _vertexArray);
        if (_animated)
        {
            var pushConstant = new byte[PushConstantSize];
            System.BitConverter.TryWriteBytes(pushConstant, Time.GetTicksMsec() / 1000f);
            _device.DrawListSetPushConstant(drawList, pushConstant, PushConstantSize);
        }

        _device.DrawListDraw(drawList, false, 1);
        _device.DrawListEnd();
    }

    private void FreeOnRenderThread()
    {
        RenderingDevice? device = RenderingServer.GetRenderingDevice();
        if (device is null)
            return;

        _device = null;
        FreeIfValid(device, _framebuffer);
        FreeIfValid(device, _target);
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

    /// <summary>The compiled RdSquare.slang, or null if it has not been built.</summary>
    private static RDShaderSpirV? LoadSlang()
    {
        byte[] vertex = FileAccess.GetFileAsBytes(SlangVertexPath);
        byte[] fragment = FileAccess.GetFileAsBytes(SlangFragmentPath);
        if (vertex.Length == 0 || fragment.Length == 0)
            return null;

        return new RDShaderSpirV { BytecodeVertex = vertex, BytecodeFragment = fragment };
    }

    private static RDShaderSpirV CompileGlsl(RenderingDevice device)
    {
        var source = new RDShaderSource
        {
            Language = RenderingDevice.ShaderLanguage.Glsl,
            SourceVertex = """
                #version 450

                layout(location = 0) in vec2 corner;

                void main() {
                	gl_Position = vec4(corner, 0.0, 1.0);
                }
                """,
            SourceFragment = """
                #version 450

                layout(location = 0) out vec4 frag_color;

                void main() {
                	frag_color = vec4(1.0, 0.5, 0.1, 1.0);
                }
                """,
        };

        return device.ShaderCompileSpirVFromSource(source);
    }

    private static bool CheckStage(RDShaderSpirV spirv, RenderingDevice.ShaderStage stage)
    {
        string error = spirv.GetStageCompileError(stage);
        if (error.Length == 0)
            return true;

        GD.PushError($"RdSquare: shader stage {stage} failed: {error}");
        return false;
    }
}
