using System.IO;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>
/// The particle lab on the GPU (Shaders/particle_lab.slang). Everything the
/// show is lives in buffers that stay between frames: the pool, the free
/// slots, each particle's trail ring, the field and the deaths waiting to
/// burst. <see cref="Step"/> moves it on one frame, <see cref="Draw"/> shows
/// it. Nothing is uploaded per particle: the CPU only sends the settings
/// block, and reads the counters back late for the panel.
/// </summary>
internal sealed class ParticleGpu : IDisposable
{
    public const uint Capacity = 1 << 18;
    public const uint TrailRing = 12;
    public const uint FieldWidth = 640, FieldHeight = 360;
    public const uint MostEvents = 16384;
    public const uint MostSpawnsPerFrame = 1 << 16;

    private const uint Group = 64;
    private const int Counters = 8;

    private readonly GraphicsDevice _device;
    private readonly Buffer _particles, _trails, _dead, _alive, _counters, _args, _deposit;
    private readonly Buffer[] _events = new Buffer[2];
    private readonly Buffer[] _fields = new Buffer[2];
    private readonly TransferBuffer _readback;
    private readonly ComputePipeline _init, _begin, _emitEvents, _emit, _simulate, _finish, _spread, _clear;
    private readonly Shader[] _shaders;
    private readonly GraphicsPipeline _field, _particlesAdd, _particlesBlend, _trailsAdd, _trailsBlend;

    /// <summary>Which of each ping-pong pair holds last frame's: events to burst, the field to read.</summary>
    private int _current;
    private bool _cleared;
    private Fence? _reading;

    /// <summary>The newest counts the GPU has handed back.</summary>
    public ParticleCounts Counts { get; private set; }

    public ParticleGpu(GraphicsDevice device, TextureFormat format)
    {
        _device = device;
        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "particle_lab.slang");
        const BufferUsageFlags rw = BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite;
        const BufferUsageFlags drawn = rw | BufferUsageFlags.GraphicsStorageRead;

        _particles = Buffer.Create<Vector4>(device, "particle pool", drawn, Capacity * 3);
        _trails = Buffer.Create<Vector2>(device, "particle trails", drawn, Capacity * TrailRing);
        _dead = Buffer.Create<uint>(device, "particle free slots", rw, Capacity);
        _alive = Buffer.Create<uint>(device, "particle draw lists", drawn, Capacity * 2);
        _counters = Buffer.Create<int>(device, "particle counters", rw, Counters);
        _args = Buffer.Create<uint>(device, "particle draws", rw | BufferUsageFlags.Indirect, 8);
        _deposit = Buffer.Create<uint>(device, "particle deposits", rw, FieldWidth * FieldHeight);
        for (int i = 0; i < 2; i++)
        {
            _events[i] = Buffer.Create<Vector4>(device, "particle deaths", rw, MostEvents * 2);
            _fields[i] = Buffer.Create<float>(device, "particle field", drawn, FieldWidth * FieldHeight);
        }
        _readback = TransferBuffer.Create<int>(device, "particle counters back", TransferBufferUsage.Download, Counters);

        _init = Compute(module, "initPool", Group);
        _begin = Compute(module, "beginFrame", 1);
        _emitEvents = Compute(module, "emitEvents", Group);
        _emit = Compute(module, "emit", Group);
        _simulate = Compute(module, "simulate", Group);
        _finish = Compute(module, "finishFrame", 1);
        _spread = Compute(module, "spreadField", Group);
        _clear = Compute(module, "clearField", Group);

        Shader particleVertex = Stage(module, "particleVertex", ShaderStage.Vertex, 3);
        Shader particleFragment = Stage(module, "particleFragment", ShaderStage.Fragment, 0);
        Shader trailVertex = Stage(module, "trailVertex", ShaderStage.Vertex, 3);
        Shader trailFragment = Stage(module, "trailFragment", ShaderStage.Fragment, 0);
        Shader fieldVertex = Stage(module, "fieldVertex", ShaderStage.Vertex, 0, uniforms: 0);
        Shader fieldFragment = Stage(module, "fieldFragment", ShaderStage.Fragment, 1);
        _shaders = [particleVertex, particleFragment, trailVertex, trailFragment, fieldVertex, fieldFragment];

        _field = Draw(fieldVertex, fieldFragment, format, null);
        _particlesAdd = Draw(particleVertex, particleFragment, format, Additive);
        _particlesBlend = Draw(particleVertex, particleFragment, format, Premultiplied);
        _trailsAdd = Draw(trailVertex, trailFragment, format, Additive);
        _trailsBlend = Draw(trailVertex, trailFragment, format, Premultiplied);
    }

    /// <summary>Empties the pool and the field on the next <see cref="Step"/>.</summary>
    public void Clear() => _cleared = false;

    /// <summary>
    /// One frame of the show: burst last frame's deaths, spawn what the
    /// emitters owe, move everything, count what is left to draw, and let the
    /// field fade and spread. <paramref name="moving"/> false only draws.
    /// </summary>
    public void Step(CommandBuffer commands, in SimGpu sim, bool moving)
    {
        uint fieldCells = FieldWidth * FieldHeight;
        if (!_cleared)
        {
            _cleared = true;
            Pass(commands, _init, sim, Capacity, SimWrites());
            for (int i = 0; i < 2; i++)
                Pass(commands, _clear, sim, fieldCells, [Write(_fields[i]), Write(_deposit)], [_events[0], _events[1]]);
        }

        if (!moving)
            return;

        Buffer[] reads = [_events[_current], _fields[_current]];
        StorageBufferReadWriteBinding[] writes = SimWrites();
        Pass(commands, _begin, sim, 1, writes, reads);
        Pass(commands, _emitEvents, sim, MostEvents, writes, reads);
        if (sim.Frame.W > 0)
            Pass(commands, _emit, sim, sim.Frame.W, writes, reads);
        Pass(commands, _simulate, sim, Capacity, writes, reads);
        Pass(commands, _finish, sim, 1, writes, reads);
        Pass(commands, _spread, sim, fieldCells, [Write(_fields[1 - _current]), Write(_deposit)], reads);
        _current = 1 - _current;
    }

    /// <summary>The field under it all, then the trails, then the particles, into <paramref name="target"/>.</summary>
    public void Draw(CommandBuffer commands, Texture target, in SimGpu sim, Xanadu.Core.Maths.Color background, bool additive)
    {
        RenderPass pass = commands.BeginRenderPass(new ColorTargetInfo(target, new Mw.Color(background.R, background.G, background.B, 1f)));
        commands.PushVertexUniformData(sim);
        commands.PushFragmentUniformData(sim);

        if (sim.Look.X > 0)
        {
            pass.BindGraphicsPipeline(_field);
            pass.BindFragmentStorageBuffers(_fields[_current]);
            pass.DrawPrimitives(3, 1, 0, 0);
        }

        pass.BindGraphicsPipeline(additive ? _trailsAdd : _trailsBlend);
        pass.BindVertexStorageBuffers(_particles, _alive, _trails);
        pass.DrawPrimitivesIndirect(_args, 16, 1);
        pass.BindGraphicsPipeline(additive ? _particlesAdd : _particlesBlend);
        pass.BindVertexStorageBuffers(_particles, _alive, _trails);
        pass.DrawPrimitivesIndirect(_args, 0, 1);
        commands.EndRenderPass(pass);
    }

    /// <summary>
    /// Hands the counters back without ever waiting: a copy goes up on its own
    /// command buffer, after the frame that filled them, and is read once its
    /// fence says it is done.
    /// </summary>
    public void PollCounts()
    {
        if (_reading is { } fence)
        {
            if (!_device.QueryFence(fence))
                return;
            _device.ReleaseFence(fence);
            _reading = null;
            Span<int> c = _readback.Map<int>(false);
            Counts = new ParticleCounts(c[0], c[1], c[2], c[4], c[5], c[6]);
            _readback.Unmap();
        }

        if (!_cleared)
            return;
        CommandBuffer commands = _device.AcquireCommandBuffer();
        CopyPass copy = commands.BeginCopyPass();
        copy.DownloadFromBuffer(new BufferRegion(_counters), new TransferBufferLocation(_readback));
        commands.EndCopyPass(copy);
        _reading = _device.SubmitAndAcquireFence(commands);
    }

    private StorageBufferReadWriteBinding[] SimWrites() =>
    [
        Write(_particles), Write(_trails), Write(_dead), Write(_alive),
        Write(_counters), Write(_events[1 - _current]), Write(_args), Write(_deposit),
    ];

    private static StorageBufferReadWriteBinding Write(Buffer buffer) => new(buffer, cycle: false);

    private void Pass(CommandBuffer commands, ComputePipeline pipeline, in SimGpu sim, uint threads,
        StorageBufferReadWriteBinding[] writes, Buffer[]? reads = null)
    {
        ComputePass pass = commands.BeginComputePass([], writes);
        pass.BindComputePipeline(pipeline);
        Buffer[] read = reads ?? [_events[_current], _fields[_current]];
        pass.BindStorageBuffers(read);
        commands.PushComputeUniformData(sim);
        pass.Dispatch((threads + Group - 1) / Group, 1, 1);
        commands.EndComputePass(pass);
    }

    private ComputePipeline Compute(string module, string entry, uint threads) =>
        ComputePipeline.Create(_device, SlangShaders.Compile(module, entry, "compute"), "main", new ComputePipelineCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            NumReadonlyStorageBuffers = 2,
            NumReadWriteStorageBuffers = entry is "spreadField" or "clearField" ? 2u : 8u,
            NumUniformBuffers = 1,
            ThreadCountX = threads,
            ThreadCountY = 1,
            ThreadCountZ = 1,
        });

    private Shader Stage(string module, string entry, ShaderStage stage, uint storage, uint uniforms = 1) =>
        Shader.Create(_device, SlangShaders.Compile(module, entry, stage == ShaderStage.Vertex ? "vertex" : "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = stage, NumStorageBuffers = storage, NumUniformBuffers = uniforms });

    private GraphicsPipeline Draw(Shader vertex, Shader fragment, TextureFormat format, ColorTargetBlendState? blend) =>
        GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = format, BlendState = blend ?? ColorTargetBlendState.NoBlend }],
            },
        });

    // Alpha is left alone: the view stays opaque for the canvas that shows it.
    private static readonly ColorTargetBlendState Additive = Blend(BlendFactor.One, BlendFactor.One);
    private static readonly ColorTargetBlendState Premultiplied = Blend(BlendFactor.One, BlendFactor.OneMinusSrcAlpha);

    private static ColorTargetBlendState Blend(BlendFactor source, BlendFactor destination) => new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = source,
        DstColorBlendFactor = destination,
        ColorBlendOp = BlendOp.Add,
        SrcAlphaBlendFactor = BlendFactor.Zero,
        DstAlphaBlendFactor = BlendFactor.One,
        AlphaBlendOp = BlendOp.Add,
        EnableColorWriteMask = true,
        ColorWriteMask = ColorComponentFlags.RGBA,
    };

    public void Dispose()
    {
        if (_reading is { } fence)
        {
            _device.WaitForFence(fence);
            _device.ReleaseFence(fence);
        }

        foreach (GraphicsPipeline p in new[] { _field, _particlesAdd, _particlesBlend, _trailsAdd, _trailsBlend })
            p.Dispose();
        foreach (ComputePipeline p in new[] { _init, _begin, _emitEvents, _emit, _simulate, _finish, _spread, _clear })
            p.Dispose();
        foreach (Shader s in _shaders)
            s.Dispose();
        foreach (Buffer b in new[] { _particles, _trails, _dead, _alive, _counters, _args, _deposit, _events[0], _events[1], _fields[0], _fields[1] })
            b.Dispose();
        _readback.Dispose();
    }
}
