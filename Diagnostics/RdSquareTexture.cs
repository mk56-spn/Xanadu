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
    private RdFullscreenPass? _pass;

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
        _pass = RdFullscreenPass.Create(device, _shader, _size, "RdSquare");
        if (_pass is null)
            return;

        _device = device;
        Texture.TextureRdRid = _pass.Target;
    }

    private void DrawOnRenderThread()
    {
        if (_device is null || _pass is null)
            return;

        long drawList = _pass.Begin(_device, new Color(0f, 0f, 0f, 0f));
        if (_animated)
        {
            var pushConstant = new byte[PushConstantSize];
            System.BitConverter.TryWriteBytes(pushConstant, Time.GetTicksMsec() / 1000f);
            _device.DrawListSetPushConstant(drawList, pushConstant, PushConstantSize);
        }

        RdFullscreenPass.End(_device, drawList);
    }

    private void FreeOnRenderThread()
    {
        RenderingDevice? device = RenderingServer.GetRenderingDevice();
        if (device is null)
            return;

        _device = null;
        _pass?.Free(device);
        _pass = null;
        RdFullscreenPass.FreeIfValid(device, _shader);
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
