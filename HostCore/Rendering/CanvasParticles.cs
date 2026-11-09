using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Godot;
using MoonWorks.Graphics;
using Xanadu.Particles;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Godot.Color;
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
    private readonly List<(uint Start, uint Count)> _dirty = new();
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
/// The host's <see cref="IParticleServer"/> (docs/moonworks-port.md step 7):
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
    }

    private readonly GraphicsDevice _device;
    private readonly CanvasTree _tree;
    private readonly ComputePipeline _step;
    private readonly GraphicsPipeline _draw;
    private readonly Buffer _effects;
    private readonly TransferBuffer _effectUpload;
    private readonly Vector4[] _rows = new Vector4[MaxEffects * RowSize];
    private readonly Dictionary<XpfParticle, int> _effectRows = new(ReferenceEqualityComparer.Instance);
    private readonly List<ParticleField> _fields = new();
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

        Shader vertex = Shader.Create(device, SlangShaders.Compile(module, "quad", "vertex"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Vertex, NumStorageBuffers = 2, NumUniformBuffers = 1 });
        Shader fragment = Shader.Create(device, SlangShaders.Compile(module, "shadeParticle", "fragment"), "main",
            new ShaderCreateInfo { Format = ShaderFormat.SPIRV, Stage = ShaderStage.Fragment, NumSamplers = 1 });
        _draw = GraphicsPipeline.Create(device, new GraphicsPipelineCreateInfo
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
                ColorTargetDescriptions = [new ColorTargetDescription { Format = format, BlendState = CanvasPipelines.Blend(Xanadu.Rendering.BlendMode.Mix) }],
            },
        });
        vertex.Dispose();
        fragment.Dispose();

        _effects = Buffer.Create<Vector4>(device, "particle effects",
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.GraphicsStorageRead, (uint)_rows.Length);
        _effectUpload = TransferBuffer.Create<Vector4>(device, "particle effect upload", TransferBufferUsage.Upload, (uint)_rows.Length);

        ParticleLibrary.Changed += DropEffects;
    }

    /// <summary>Fields stepped in the last frame, for the debug line.</summary>
    public int LastLiveFields { get; private set; }

    public void Emit(Rid item, XpfParticle effect, ReadOnlySpan<ParticleSeed> seeds, Rid strip = default, int cells = 1)
    {
        if (seeds.IsEmpty || _tree.Item(item) is not { } canvasItem)
            return;

        if (canvasItem.Particles is not { } field)
        {
            field = new ParticleField(_device);
            canvasItem.Particles = field;
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
        float width, float height, Texture strip, Sampler sampler)
    {
        pass.BindGraphicsPipeline(_draw);
        pass.BindVertexStorageBuffers(field.Buffer, _effects);
        pass.BindFragmentSamplers(new TextureSamplerBinding(strip, sampler));
        commands.PushVertexUniformData(new DrawUniforms
        {
            Width = width, Height = height,
            OriginX = transform.Origin.X, OriginY = transform.Origin.Y,
            XX = transform.X.X, XY = transform.X.Y, YX = transform.Y.X, YY = transform.Y.Y,
            R = modulate.R, G = modulate.G, B = modulate.B, A = modulate.A,
        });
        pass.DrawPrimitives(6, ParticleField.Capacity, 0, 0);
    }

    /// <summary>The effect's row in the table, sampled the first time it is emitted.</summary>
    private int Row(XpfParticle effect)
    {
        if (_effectRows.TryGetValue(effect, out int row))
            return row;

        if (_effectRows.Count == MaxEffects)
            _effectRows.Clear();

        row = _effectRows.Count;
        _effectRows[effect] = row;
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
        _draw.Dispose();
        _effects.Dispose();
        _effectUpload.Dispose();
    }
}
