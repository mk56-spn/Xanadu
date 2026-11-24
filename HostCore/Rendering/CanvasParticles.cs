using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Particles;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Xanadu.Core.Maths.Color;
using Shader = MoonWorks.Graphics.Shader;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Rendering;

/// <summary>One particle as the GPU keeps it (<c>Particle</c> in <c>particles.slang</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GpuParticle
{
    public float X, Y, Vx, Vy;
    public float Age, Lifespan, Damping, Angle;
    public float R, G, B, A;
    public float Size, Effect, Cell, Cells;
}

/// <summary>
/// The particles one canvas item draws: a ring of slots in a storage buffer.
/// A burst takes the next slots, cutting off the oldest particles if the ring
/// is full, as Godot's pool of ten instances cut off the oldest burst.
/// </summary>
internal sealed class ParticleField : IDisposable
{
    public const uint Capacity = 8192;

    public readonly Buffer Buffer;
    private readonly TransferBuffer _upload;
    private readonly GpuParticle[] _staging = new GpuParticle[Capacity];
    private readonly List<(uint Start, uint Count)> _dirty = [];
    private uint _head;

    /// <summary>Seconds until every particle in the ring has died; the field neither steps nor draws past it.</summary>
    public float Remaining;

    /// <summary>The note strip the field's glyph particles show; 0 for none yet.</summary>
    public ulong Strip;

    public bool Live => Remaining > 0f;

    public ParticleField(GraphicsDevice device)
    {
        Buffer = Buffer.Create<GpuParticle>(device, "particles",
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
            Capacity);
        _upload = TransferBuffer.Create<GpuParticle>(device, "particle upload", TransferBufferUsage.Upload, Capacity);
        // Every slot starts dead (age 0, lifespan 0), and the GPU copy must say so too.
        _dirty.Add((0, Capacity));
    }

    public void Add(ReadOnlySpan<ParticleSeed> seeds, int effect)
    {
        int count = Math.Min(seeds.Length, (int)Capacity);
        uint start = _head;
        for (int i = 0; i < count; i++)
        {
            ref readonly ParticleSeed seed = ref seeds[i];
            _staging[_head] = new GpuParticle
            {
                X = seed.Position.X, Y = seed.Position.Y,
                Vx = seed.Velocity.X, Vy = seed.Velocity.Y,
                Age = -seed.Delay, Lifespan = seed.Lifespan, Damping = seed.Damping, Angle = seed.Angle,
                R = seed.Color.R, G = seed.Color.G, B = seed.Color.B, A = seed.Color.A,
                Size = seed.Size, Effect = effect, Cell = seed.Cell, Cells = StripCells,
            };
            Remaining = MathF.Max(Remaining, seed.Delay + seed.Lifespan);
            _head = (_head + 1) % Capacity;
        }

        // The written slots, split where they wrap round the end of the ring.
        uint first = (uint)Math.Min(count, (int)(Capacity - start));
        _dirty.Add((start, first));
        if (count > first)
            _dirty.Add((0, (uint)count - first));
    }

    /// <summary>How many cells the strip has, set by whoever emits glyphs into the field.</summary>
    public int StripCells = 1;

    public bool Disposed { get; private set; }

    /// <summary>Sends the slots written since the last frame; the rest of the buffer is left as the GPU moved it.</summary>
    public void Upload(CopyPass copy)
    {
        if (_dirty.Count == 0)
            return;

        Span<GpuParticle> mapped = _upload.Map<GpuParticle>(cycle: true);
        foreach ((uint start, uint count) in _dirty)
            _staging.AsSpan((int)start, (int)count).CopyTo(mapped.Slice((int)start, (int)count));
        _upload.Unmap();

        foreach ((uint start, uint count) in _dirty)
            copy.UploadToBuffer<GpuParticle>(_upload, Buffer, start, start, count, cycle: false);
        _dirty.Clear();
    }

    public bool Dirty => _dirty.Count > 0;

    public void Dispose()
    {
        if (Disposed)
            return;
        Disposed = true;
        Buffer.Dispose();
        _upload.Dispose();
    }
}

/// <summary>
/// The host's <see cref="IParticleServer"/> (Docs/host.md §5.6):
/// bursts land in the <see cref="ParticleField"/> of the canvas item they were
/// emitted into; once a frame every live field is uploaded and stepped by a
/// compute shader, and the <see cref="CanvasRenderer"/> draws each one as an
/// instanced quad per particle where its item sits in the tree.
/// </summary>
/// <remarks>
/// Each effect gets a row in one table (gravity, a sampled colour ramp and a
/// sampled scale curve) the first time it is emitted. Rows are dropped when the
/// particle library changes; particles still flying keep the row index they
/// were born with, so they finish with whatever effect took the row next.
/// </remarks>
internal sealed class CanvasParticles : IParticleServer, IDisposable
{
    private const int Samples = 32;
    private const int RowSize = 1 + Samples * 2;
    private const int MaxEffects = 128;
    private const uint ThreadsPerGroup = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct StepUniforms { public float Dt; public uint Count; public float Pad0, Pad1; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public float Width, Height, OriginX, OriginY;
        public float XX, XY, YX, YY;
        public float R, G, B, A;
        public float Depth, Pad0, Pad1, Pad2;
    }

    private readonly GraphicsDevice _device;
    private readonly CanvasTree _tree;
    private readonly ComputePipeline _step;
    private readonly Shader _vertex;
    private readonly Shader _fragment;
    private readonly TextureFormat _format;

    /// <summary>The draw pipeline per sample count, made the first time a target with that count draws particles.</summary>
    private readonly Dictionary<SampleCount, GraphicsPipeline> _draws = [];
    private readonly Buffer _effects;
    private readonly TransferBuffer _effectUpload;
    private readonly Vector4[] _rows = new Vector4[MaxEffects * RowSize];
    private readonly Dictionary<XpfParticle, (int Row, int Version)> _effectRows = new(ReferenceEqualityComparer.Instance);
    private readonly List<ParticleField> _fields = [];
    private bool _rowsDirty = true;

    public CanvasParticles(GraphicsDevice device, CanvasTree tree, TextureFormat format)
    {
        _device = device;
        _tree = tree;
        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "particles.slang");

        _step = ComputePipeline.Create(device, SlangShaders.Compile(module, "simulate", "compute"), "main",
            new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 1,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = ThreadsPerGroup, ThreadCountY = 1, ThreadCountZ = 1,
            });

        _format = format;
        _vertex = Shader.Create(device, SlangShaders.Compile(module, "quad", "vertex"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumStorageBuffers = 2, NumUniformBuffers = 1 });
        _fragment = Shader.Create(device, SlangShaders.Compile(module, "shadeParticle", "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 1 });

        _effects = Buffer.Create<Vector4>(device, "particle effects",
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.GraphicsStorageRead, (uint)_rows.Length);
        _effectUpload = TransferBuffer.Create<Vector4>(device, "particle effect upload", TransferBufferUsage.Upload, (uint)_rows.Length);

        ParticleLibrary.Changed += DropEffects;
    }

    /// <summary>Every effect's row (gravity, colour ramp, scale curve), which a particle-mesh draw reads too.</summary>
    public Buffer Effects => _effects;

    /// <summary>Fields stepped in the last frame, for the debug line.</summary>
    public int LastLiveFields { get; private set; }

    public void Emit(Handle item, XpfParticle effect, ReadOnlySpan<ParticleSeed> seeds, Handle strip = default, int cells = 1)
    {
        if (seeds.IsEmpty || _tree.Item(item) is not { } canvasItem)
            return;

        if (canvasItem.Particles is not { } field)
        {
            field = new ParticleField(_device);
            canvasItem.Particles = field;
            canvasItem.Touch();
        }

        if (!_fields.Contains(field))
            _fields.Add(field);
        if (strip.Id != 0)
        {
            field.Strip = strip.Id;
            field.StripCells = Math.Max(1, cells);
        }

        field.Add(seeds, Row(effect));
    }

    /// <summary>Uploads what was emitted and moves every live field on by <paramref name="dt"/> seconds.</summary>
    public void Step(CommandBuffer commands, float dt)
    {
        // Fields whose item was freed have been disposed by the tree.
        _fields.RemoveAll(static field => field.Disposed);
        LastLiveFields = 0;
        bool upload = _rowsDirty;
        foreach (ParticleField field in _fields)
            upload |= field.Dirty;

        if (upload)
        {
            CopyPass copy = commands.BeginCopyPass();
            if (_rowsDirty)
            {
                Span<Vector4> mapped = _effectUpload.Map<Vector4>(cycle: true);
                _rows.AsSpan().CopyTo(mapped);
                _effectUpload.Unmap();
                copy.UploadToBuffer<Vector4>(_effectUpload, _effects, 0, 0, (uint)_rows.Length, cycle: true);
                _rowsDirty = false;
            }

            foreach (ParticleField field in _fields)
                field.Upload(copy);
            commands.EndCopyPass(copy);
        }

        foreach (ParticleField field in _fields)
        {
            if (!field.Live)
                continue;

            LastLiveFields++;
            field.Remaining -= dt;
            commands.PushComputeUniformData(new StepUniforms { Dt = dt, Count = ParticleField.Capacity });
            ComputePass compute = commands.BeginComputePass(new StorageBufferReadWriteBinding(field.Buffer, cycle: false));
            compute.BindComputePipeline(_step);
            compute.BindStorageBuffers(_effects);
            compute.Dispatch((ParticleField.Capacity + ThreadsPerGroup - 1) / ThreadsPerGroup, 1, 1);
            commands.EndComputePass(compute);
        }
    }

    /// <summary>
    /// Draws a field inside an open render pass, moved by its item's
    /// <paramref name="transform"/> and faded by <paramref name="modulate"/>.
    /// Leaves the particle pipeline bound and vertex uniform slot 0 overwritten.
    /// </summary>
    public void Draw(CommandBuffer commands, RenderPass pass, ParticleField field, Transform2D transform, Color modulate,
        float width, float height, Texture strip, Sampler sampler, SampleCount samples = SampleCount.One, float depth = 0)
    {
        pass.BindGraphicsPipeline(DrawPipeline(samples));
        pass.BindVertexStorageBuffers(field.Buffer, _effects);
        pass.BindFragmentSamplers(new TextureSamplerBinding(strip, sampler));
        commands.PushVertexUniformData(new DrawUniforms
        {
            Width = width, Height = height,
            OriginX = transform.Origin.X, OriginY = transform.Origin.Y,
            XX = transform.X.X, XY = transform.X.Y, YX = transform.Y.X, YY = transform.Y.Y,
            R = modulate.R, G = modulate.G, B = modulate.B, A = modulate.A,
            Depth = depth,
        });
        pass.DrawPrimitives(6, ParticleField.Capacity, 0, 0);
    }

    /// <summary>The particle draw pipeline for a target with <paramref name="samples"/> samples a pixel.</summary>
    /// <summary>The renderer's depth format: particles draw inside its passes, which always carry a depth buffer.</summary>
    public TextureFormat DepthFormat { get; init; } = TextureFormat.D16Unorm;

    public GraphicsPipeline DrawPipeline(SampleCount samples)
    {
        if (_draws.TryGetValue(samples, out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _vertex,
            FragmentShader = _fragment,
            VertexInputState = VertexInputState.Empty,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = new MultisampleState { SampleCount = samples },
            // Tested, never written: an opaque rect in front hides them, and they don't hide anything.
            DepthStencilState = new DepthStencilState { EnableDepthTest = true, EnableDepthWrite = false, CompareOp = CompareOp.Less },
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new ColorTargetDescription { Format = _format, BlendState = CanvasPipelines.Blend(BlendMode.Mix) }],
                HasDepthStencilTarget = true,
                DepthStencilFormat = DepthFormat,
            },
        });
        _draws[samples] = pipeline;
        return pipeline;
    }

    /// <summary>The effect's row in the table, sampled the first time it is emitted and again after it was edited in place.</summary>
    private int Row(XpfParticle effect)
    {
        int row;
        if (_effectRows.TryGetValue(effect, out (int Row, int Version) known))
        {
            if (known.Version == effect.Version)
                return known.Row;
            row = known.Row;
        }
        else
        {
            if (_effectRows.Count == MaxEffects)
                _effectRows.Clear();
            row = _effectRows.Count;
        }
        _effectRows[effect] = (row, effect.Version);
        int start = row * RowSize;
        _rows[start] = new Vector4(effect.Gravity.X, effect.Gravity.Y, 0, 0);
        for (int i = 0; i < Samples; i++)
        {
            float t = i / (float)(Samples - 1);
            Color ramp = ParticleSpawn.Ramp(effect, t);
            _rows[start + 1 + i] = new Vector4(ramp.R, ramp.G, ramp.B, ramp.A);
            _rows[start + 1 + Samples + i] = new Vector4(ParticleSpawn.Scale(effect, t), 0, 0, 0);
        }

        _rowsDirty = true;
        return row;
    }

    private void DropEffects() => _effectRows.Clear();

    public void Dispose()
    {
        ParticleLibrary.Changed -= DropEffects;
        foreach (ParticleField field in _fields)
            field.Dispose();
        _step.Dispose();
        foreach (GraphicsPipeline pipeline in _draws.Values)
            pipeline.Dispose();
        _vertex.Dispose();
        _fragment.Dispose();
        _effects.Dispose();
        _effectUpload.Dispose();
    }
}
