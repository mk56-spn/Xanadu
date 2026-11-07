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
    private RdFullscreenPass? _pass;

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

        _pass = RdFullscreenPass.Create(device, _shader, _size, "RdLife");
        if (_pass is null)
            return;

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

        _device = device;
        Texture.TextureRdRid = _pass.Target;
    }

    private void DrawOnRenderThread(int state)
    {
        if (_device is null || _pass is null)
            return;

        long drawList = _pass.Begin(_device, Colors.Black);
        _device.DrawListBindUniformSet(drawList, _uniformSets[state], 0);
        RdFullscreenPass.End(_device, drawList);
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

        _pass?.Free(device);
        _pass = null;
        RdFullscreenPass.FreeIfValid(device, _sampler);
        RdFullscreenPass.FreeIfValid(device, _shader);
    }
}
