using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Rendering;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = Xanadu.Core.Maths.Color;
using MwColor = MoonWorks.Graphics.Color;
using Rect = MoonWorks.Graphics.Rect;
using Shader = MoonWorks.Graphics.Shader;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Draws a <see cref="CanvasTree"/> (Docs/host.md §5.3). Each frame it walks
/// the tree in order and turns every visible item's primitives into GPU
/// records in pixels; one pipeline pulls them by vertex id and shapes them in
/// the fragment stage (primitives.slang), so plain content is a handful of
/// draws however much there is.
/// </summary>
/// <remarks>
/// Order is depth: each primitive gets the depth of its place in the frame.
/// Plain opaque rects (square, solid, whole-pixel) go first in a pre-pass,
/// front to back with depth writes, so anything they cover is rejected before
/// it shades; everything else follows in order with the depth test on.
/// Items with a material keep the effect path: their primitives are cut into
/// canvas vertices for the effect's own pipeline. Effects and particles test
/// their place's depth too; only an effect that moves its own vertices
/// (a deform) can't, so once one has drawn, no later rect may join the pre-pass.
/// <para>
/// Screen reads: before a draw whose effect samples <c>screenTexture</c>,
/// the pass is ended, the target copied into a mipmapped screen texture, and
/// the pass resumes. A copy is only made if something was drawn since the last.
/// </para>
/// </remarks>
internal sealed partial class CanvasRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ViewUniforms { public float Width, Height, Depth, Span; }

    /// <summary>particle_mesh.slang's <c>Place</c>: vertex uniform buffer 1 of a particle-mesh draw.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PlaceUniforms { public float OriginX, OriginY, Pad0, Pad1, XX, XY, YX, YY; }

    /// <summary>xanadu.stage's <c>Frame</c>: fragment uniform buffer 0. Only the engine's clocks; anything else is a global.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms
    {
        public float Width, Height, Time, ItemPremultiplied;
        public float Beat, Pad0, Pad1, Pad2;

        public static FrameUniforms Now(float width, float height, float time) => new()
        {
            Width = width, Height = height, Time = time, Beat = ShaderClock.Beat,
        };
    }

    /// <summary>A kept-state effect's update's uniform buffer 0 (SlangShaders.WrapUpdate).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct UpdateUniforms { public float Dt, Time, Pad0, Pad1; }


    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public float Width, Height;
        public uint First, Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExpandUniforms
    {
        public uint Instances;
        public float DepthStep;
        public uint First, Pad1;
    }

    /// <summary>One item's draws in a job: its place, and what it inherits.</summary>
    /// <remarks>A resident segment (<see cref="Piece"/>) has no item: its items are placed on the GPU.</remarks>
    private readonly record struct Entry(int Z, int Sequence, CanvasItem Item, Transform2D Transform, Color Color, Rect Clip,
        ResidentPiece? Piece = null);

    private enum StepKind
    {
        Primitives,
        Effect,
        Particles,

        /// <summary>An item's triangles drawn once per particle of its field (<see cref="CanvasItem.ParticleMesh"/>), through their effect.</summary>
        MeshParticles,
    }

    /// <summary>
    /// One draw, in order: a run of primitives (with up to <see cref="Slots"/>
    /// textures, from <see cref="FirstTexture"/> in the texture list), an effect's
    /// vertices, or a particle field.
    /// </summary>
    private readonly record struct Step(
        StepKind Kind, uint Start, uint Count, int FirstTexture = 0,
        Rect Clip = default, CanvasMaterial? Material = null, IReadOnlyDictionary<string, Vector4>? Values = null, ulong Texture = 0,
        ParticleField? Particles = null, Transform2D Transform = default, Color Color = default, float Depth = 0, float Span = 0,
        KeptMesh? Mesh = null);

    /// <summary>
    /// A big [Grid] effect's vertices, kept on the GPU between frames. Cutting a 96 × 54 grid
    /// into vertices costs the CPU milliseconds and sends megabytes, and the shader tool's
    /// thumbnails hold several such grids that almost never change, so they are cut again only
    /// when the item's primitives, its place or its tint change. Start and Count on a step
    /// that uses one are within <see cref="Buffer"/>.
    /// </summary>
    private sealed class KeptMesh
    {
        public int Hash;
        public Transform2D Transform;
        public Color Color;
        public Buffer? Buffer;
        public uint Capacity;
        public readonly List<CanvasVertex> Vertices = [];
        public readonly List<(uint Start, uint Count, ulong Texture)> Pieces = [];
        public bool Pending;
        public long LastFrame;
    }

    /// <summary>Grids with at least this many cells are kept on the GPU (<see cref="KeptMesh"/>).</summary>
    private const int KeptCells = 256;

    /// <summary>Frames a kept mesh may go undrawn before its buffer is let go.</summary>
    private const int KeptFrames = 120;

    private readonly Dictionary<(CanvasItem Item, int From), KeptMesh> _kept = [];
    private readonly List<(CanvasItem, int)> _keptStale = [];
    private long _buildFrame;

    /// <summary>One root drawn into one target.</summary>
    private sealed class Job(CanvasItem root, Texture target, MwColor clear)
    {
        public readonly CanvasItem Root = root;
        public readonly Texture Target = target;
        public readonly MwColor Clear = clear;
        public readonly List<Entry> Entries = [];
        public int FirstStep, StepCount;
        public uint OpaqueStart, OpaqueReserved;
    }

    /// <summary>Textures one primitive draw can read; a run starts again when it needs a ninth.</summary>
    private const int Slots = 8;

    private const uint NoTexture = 0xFFFF;

    /// <summary>Redraws in a row that differed before an item stops hashing its primitives, and how often it checks again.</summary>
    private const int HashMissesBeforeSkipping = 8, HashRecheck = 32;

    /// <summary>A primitive's texture slot with this bit is a glyph page's layer in <see cref="Glyphs"/>' array, not a run slot.</summary>
    private const uint GlyphLayer = 0x8000;

    /// <summary>Depth steps per primitive: room for four million in a frame before they share a depth.</summary>
    private const float DepthStep = 1f / (1 << 22);

    /// <summary>
    /// Depth steps a deforming effect takes, so the depths its <c>deform</c> sets spread over its own
    /// slice (about 65 thousand levels in a 24-bit buffer) and never reach the next draw.
    /// </summary>
    private const uint DeformSlice = 1 << 14;

    private readonly GraphicsDevice _device;
    private readonly CanvasTree _tree;
    private readonly CanvasPipelines _pipelines;
    private readonly Texture _white;
    private readonly Texture _screen;

    /// <summary>
    /// The main frame's multisampled target (MSAA): the tree draws into it and
    /// it resolves into the frame at each pass end, so screen copies and the
    /// window see smoothed edges. Null when the GPU can't multisample the format.
    /// Offscreen roots stay single-sampled.
    /// </summary>
    private readonly Texture? _msaa;
    private readonly SampleCount _samples;
    private readonly TextureFormat _format;
    private readonly TextureFormat _depthFormat;
    private readonly Texture _depth;

    /// <summary>
    /// The depth effects read (xanadu.stage's <c>sampleDepth</c>): single-sampled, so it can be
    /// sampled, filled from the opaque pre-pass before a job whose effects read it.
    /// </summary>
    private readonly Texture _depthRead;

    private GraphicsPipeline? _depthOnly;
    private readonly Texture? _msaaDepth;

    /// <summary>The MSAA sample count asked for; drops to what the GPU can do.</summary>
    public const SampleCount WantedSamples = SampleCount.Four;
    private readonly ScreenMips _mips;
    private readonly Sampler _sampler;
    private readonly uint _width, _height;

    private readonly Shader _primitiveVertex, _primitiveShade, _primitiveOpaque;
    private readonly ComputePipeline _expand;
    private readonly Dictionary<(bool Opaque, SampleCount Samples), GraphicsPipeline> _primitivePipelines = [];

    private readonly List<Job> _jobs = [];
    private readonly List<Step> _steps = [];
    private readonly List<(CanvasItem Root, Texture Target)> _offscreen = [];

    private readonly List<ulong> _stepTextures = [];

    /// <summary>Every recording's primitives in its own space, in ranges that stay while it does (<see cref="Resident"/>).</summary>
    private Buffer? _pool;
    private uint _poolCapacity = 1 << 16;
    private int _poolUsed;
    private int _poolEpoch;
    private bool _overflow;

    /// <summary>Recordings changed since they were sent: their primitives, and where they go.</summary>
    private GpuPrimitive[] _staging = new GpuPrimitive[1 << 12];
    private int _stagingCount;
    private readonly List<(int From, int To, int Count)> _uploads = [];
    private TransferBuffer? _stagingTransfer;
    private uint _stagingTransferCapacity;

    /// <summary>One a recording drawn this frame, and each one's run slots.</summary>
    private readonly List<GpuInstance> _instances = [];
    private readonly List<uint> _slots = [];
    private Buffer? _instanceBuffer, _slotBuffer;
    private uint _instanceCapacity, _slotCapacity;
    private TransferBuffer? _instanceTransfer, _slotTransfer;
    private uint _instanceTransferCapacity, _slotTransferCapacity;

    /// <summary>
    /// The expand stage's output: placed primitives in order, and the pre-pass list, one slot per
    /// placed primitive holding its opaque cut or a skip. Fixed slots rather than an atomic count and
    /// an indirect draw: the indirect args raced the compute pass (the pre-pass drew nothing on most
    /// frames), and a skip costs the vertex stage next to nothing.
    /// </summary>
    private Buffer? _placed, _opaqueBuffer;
    private uint _placedCapacity, _opaqueCapacity;
    private int _placedCount, _opaqueReserved;

    private const uint AllowOpaque = 1;

    private readonly List<CanvasVertex> _vertices = [];
    private Buffer? _buffer;
    private TransferBuffer? _transfer;
    private uint _capacity;

    /// <summary>Each kept-state material's state, made zeroed the first frame it is drawn.</summary>
    private readonly Dictionary<CanvasMaterial, Buffer> _states = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasMaterial> _updated = new(ReferenceEqualityComparer.Instance);
    private readonly TransferBuffer _zeros;

    /// <summary>
    /// Each declared shader global's buffer (<see cref="ShaderGlobals"/>), uploaded when its version moves:
    /// bound to every effect stage in <see cref="SlangShaders.Globals"/>' order.
    /// </summary>
    private readonly Dictionary<string, GlobalBuffer> _globalBuffers = new(StringComparer.Ordinal);

    /// <summary>This frame's globals in binding order, and one slot after them for a kept state.</summary>
    private Buffer[] _bound = [];

    /// <summary>Zeros, bound for a global with no data (or nothing declaring it) and for a state not made yet.</summary>
    private Buffer? _blank;

    private sealed class GlobalBuffer
    {
        public Buffer Buffer = null!;
        public TransferBuffer Transfer = null!;
        public int Capacity, Version = -1;
    }

    /// <summary>Textures the game can draw by id, minted from the tree so they never clash with items.</summary>
    public Dictionary<ulong, Texture> Textures { get; } = [];

    public CanvasPipelines Pipelines => _pipelines;

    /// <summary>The text atlas, whose pages plain primitives read from its layer array instead of a run slot.</summary>
    public Host.Text.TextAtlas? Glyphs { get; set; }

    /// <summary>Bound in place of the layer array before the atlas has a page.</summary>
    private Texture? _noLayers;

    public CanvasParticles Particles { get; }

    private float _lastTime = float.NaN;

    /// <summary>Draw calls in the last frame.</summary>
    public int LastBatchCount { get; private set; }

    /// <summary>Screen copies in the last frame.</summary>
    public int LastCopyCount { get; private set; }

    /// <summary>Offscreen passes in the last frame.</summary>
    public int LastOffscreenCount => Math.Max(0, _jobs.Count - 1);

    /// <summary>Primitives placed in the last frame, and how many were sent up because they changed.</summary>
    public int LastPrimitiveCount => _placedCount;

    public int LastUploadCount => _stagingCount;

    public CanvasRenderer(GraphicsDevice device, CanvasTree tree, TextureFormat format, uint width, uint height)
    {
        _device = device;
        _tree = tree;
        _width = width;
        _height = height;
        _format = format;
        _depthFormat = device.SupportedDepthFormat;
        _pipelines = new CanvasPipelines(device, format) { DepthFormat = _depthFormat };
        Particles = new CanvasParticles(device, tree, format) { DepthFormat = _depthFormat };

        var uploader = new ResourceUploader(device);
        _white = uploader.CreateTexture2D<uint>("white", [0xFFFFFFFFu], TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler, 1, 1);
        uploader.UploadAndWait();
        uploader.Dispose();

        uint levels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;
        _screen = Texture.Create2D(device, "screen copy", width, height, format,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget, levels);
        _mips = new ScreenMips(device, format, width, height, levels);

        _samples = Supported(device, format, WantedSamples);
        if (_samples != SampleCount.One)
        {
            _msaa = Texture.Create2D(device, "msaa frame", width, height, format, TextureUsageFlags.ColorTarget, 1, _samples);
            _msaaDepth = Texture.Create2D(device, "msaa depth", width, height, _depthFormat, TextureUsageFlags.DepthStencilTarget, 1, _samples);
        }

        _depth = Texture.Create2D(device, "depth", width, height, _depthFormat, TextureUsageFlags.DepthStencilTarget);
        _depthRead = Texture.Create2D(device, "depth read", width, height, _depthFormat,
            TextureUsageFlags.DepthStencilTarget | TextureUsageFlags.Sampler);
        _pipelines.MainSamples = _samples;

        // Linear with linear mips (MaxLod unclamped), so mipmapped textures such as glyph strips filter smoothly.
        _sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);

        string module = Path.Combine(AppContext.BaseDirectory, "Shaders", "primitives.slang");
        _primitiveVertex = Shader.Create(device, SlangShaders.Compile(module, "primitiveVertex", "vertex"), "main", new ShaderCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            Stage = ShaderStage.Vertex,
            NumStorageBuffers = 1,
            NumUniformBuffers = 1,
        });
        _primitiveShade = Shader.Create(device, SlangShaders.Compile(module, "primitiveShade", "fragment"), "main", new ShaderCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            Stage = ShaderStage.Fragment,
            NumSamplers = Slots + 1,
        });
        _primitiveOpaque = Shader.Create(device, SlangShaders.Compile(module, "primitiveOpaque", "fragment"), "main", new ShaderCreateInfo
        {
            Format = ShaderFormat.SPIRV,
            Stage = ShaderStage.Fragment,
        });

        _expand = ComputePipeline.Create(device,
            SlangShaders.Compile(Path.Combine(AppContext.BaseDirectory, "Shaders", "primitive_expand.slang"), "expandPrimitives", "compute"),
            "main", new ComputePipelineCreateInfo
            {
                Format = ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 4,
                NumReadWriteStorageBuffers = 2,
                NumUniformBuffers = 1,
                ThreadCountX = 64,
                ThreadCountY = 1,
                ThreadCountZ = 1,
            });

        _zeros = TransferBuffer.Create<byte>(device, "state zeros", TransferBufferUsage.Upload, SlangShaders.MaxStateBytes);
        _zeros.Map<byte>(cycle: false).Clear();
        _zeros.Unmap();
        tree.Unmounted += DropResident;
        _pipelines.Forgotten += material =>
        {
            if (_states.Remove(material, out Buffer? state))
                state.Dispose();
        };
    }

    /// <summary>The most samples a pixel, up to <paramref name="wanted"/>, the GPU can render <paramref name="format"/> with.</summary>
    private static SampleCount Supported(GraphicsDevice device, TextureFormat format, SampleCount wanted)
    {
        for (SampleCount samples = wanted; samples > SampleCount.One; samples--)
        {
            if (SDL3.SDL.SDL_GPUTextureSupportsSampleCount(device.Handle, (SDL3.SDL.SDL_GPUTextureFormat)format, (SDL3.SDL.SDL_GPUSampleCount)samples))
                return samples;
        }

        return SampleCount.One;
    }

    /// <summary>Makes a target the size of the frame, for an offscreen root.</summary>
    public Texture CreateTarget(string name) =>
        Texture.Create2D(_device, name, _width, _height, TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler | TextureUsageFlags.ColorTarget);

    /// <summary>Draws <paramref name="root"/> (an item with no parent) into <paramref name="target"/> each frame, before the tree.</summary>
    public void AddOffscreen(CanvasItem root, Texture target) => _offscreen.Add((root, target));

    public void RemoveOffscreen(CanvasItem root) => _offscreen.RemoveAll(entry => entry.Root == root);

    /// <summary>Draws the whole frame into <paramref name="target"/>, cleared to <paramref name="clear"/>; <paramref name="time"/> is an effect's TIME.</summary>
    public void Render(CommandBuffer commands, Texture target, MwColor clear, float time, HostFramePhases? phases = null)
    {
        long lap = HostFramePhases.Now;
        DrainTouched();
        _jobs.Clear();
        foreach ((CanvasItem root, Texture offscreen) in _offscreen)
            Collect(root, offscreen, new MwColor(0, 0, 0, 0), phases);
        Collect(_tree.Root, target, clear, phases);
        phases?.Lap(HostFramePhases.Phase.Collect, ref lap);

        _buildFrame++;
        Build();
        phases?.Lap(HostFramePhases.Phase.Build, ref lap);
        using (commands.Scope("upload primitives"))
        {
            Upload(commands);
            UploadKept(commands);
            UploadResident(commands);
        }
        using (commands.Scope("expand primitives"))
            Expand(commands);
        using (commands.Scope("upload globals"))
            UploadGlobals(commands);
        phases?.Lap(HostFramePhases.Phase.Upload, ref lap);

        // The particles and kept state move by the frame's time, before any pass draws them.
        float dt = float.IsNaN(_lastTime) ? 0f : Math.Clamp(time - _lastTime, 0f, 0.1f);
        _lastTime = time;
        using (commands.Scope("shader updates"))
            UpdateStates(commands, dt, time);
        using (commands.Scope("particles step"))
            Particles.Step(commands, dt);
        phases?.Lap(HostFramePhases.Phase.ShaderStates, ref lap);

        LastCopyCount = 0;
        LastBatchCount = 0;
        foreach (Job job in _jobs)
        {
            using (commands.Scope(job.Target == target ? "main target" : "offscreen target"))
                Draw(commands, job, time, job.Target == target && _msaa is not null);
        }

        phases?.Lap(HostFramePhases.Phase.DrawPasses, ref lap);
    }

    private Rect FullClip => new() { X = 0, Y = 0, W = (int)_width, H = (int)_height };

    // ---- Collecting -------------------------------------------------------

    private int _sequence;

    // The walk's timing is exclusive: one clock, each stretch charged to the part running then,
    // so nested parts (a mount placing items whose children hold mounts) never count twice.
    private HostFramePhases? _walkPhases;
    private HostFramePhases.Phase _walkPart;
    private long _walkStamp;

    /// <summary>One root's flat, sorted draw list; the walk and the sort are timed apart, under "collect items".</summary>
    private void Collect(CanvasItem root, Texture target, MwColor clear, HostFramePhases? phases)
    {
        long lap = HostFramePhases.Now;
        var job = new Job(root, target, clear);
        _sequence = 0;
        _walkPhases = phases;
        _walkStamp = lap;
        _walkPart = HostFramePhases.Phase.CollectCanvas;
        Visit(job, root, Transform2D.Identity, Colors.White, 0, FullClip);
        Switch(HostFramePhases.Phase.CollectCanvas);
        _walkPhases = null;
        phases?.Lap(HostFramePhases.Phase.CollectWalk, ref lap);
        SortByZ(job.Entries);
        phases?.Lap(HostFramePhases.Phase.CollectSort, ref lap);
        _jobs.Add(job);
    }

    private int[] _zStarts = [];
    private Entry[] _zSorted = [];

    /// <summary>
    /// Puts entries in draw order, z then sequence, by counting sort: z is a small
    /// integer and entries are added in sequence order, so counting each z, turning
    /// the counts into start positions and copying entries over in the order they
    /// came keeps ties in sequence for free. Two passes, no comparisons.
    /// </summary>
    private void SortByZ(List<Entry> entries)
    {
        Span<Entry> span = CollectionsMarshal.AsSpan(entries);
        if (span.Length < 2)
            return;

        int min = int.MaxValue, max = int.MinValue;
        foreach (ref readonly Entry entry in span)
        {
            min = Math.Min(min, entry.Z);
            max = Math.Max(max, entry.Z);
        }

        if (min == max)
            return;

        int range = max - min + 1;
        if (_zStarts.Length < range + 1)
            _zStarts = new int[range + 1];
        else
            Array.Clear(_zStarts, 0, range + 1);

        foreach (ref readonly Entry entry in span)
            _zStarts[entry.Z - min + 1]++;
        for (int i = 1; i <= range; i++)
            _zStarts[i] += _zStarts[i - 1];

        if (_zSorted.Length < span.Length)
            _zSorted = new Entry[Math.Max(span.Length, _zSorted.Length * 2)];
        foreach (ref readonly Entry entry in span)
            _zSorted[_zStarts[entry.Z - min]++] = entry;
        _zSorted.AsSpan(0, span.Length).CopyTo(span);
    }

    /// <summary>Charges the walk's time so far to the part that was running and starts <paramref name="next"/>; returns the old part.</summary>
    private HostFramePhases.Phase Switch(HostFramePhases.Phase next)
    {
        HostFramePhases.Phase was = _walkPart;
        _walkPhases?.Lap(was, ref _walkStamp);
        _walkPart = next;
        return was;
    }

    private void Visit(Job job, CanvasItem item, Transform2D parent, Color parentModulate, int parentZ, Rect clip)
    {
        if (!item.Visible)
            return;

        Place(job, item, parent * item.Transform, parentModulate * item.Modulate, Math.Clamp(parentZ + item.Z, -4096, 4096), clip);
    }

    /// <summary>An item already placed (by the tree, or by a mount), then its children and mounts.</summary>
    private void Place(Job job, CanvasItem item, Transform2D transform, Color modulate, int z, Rect clip)
    {
        if (item.Clip && item.UseCustomRect)
            clip = Intersect(clip, Bounds(transform, item.CustomRect));

        if (modulate.A <= 0f || clip.W <= 0 || clip.H <= 0)
            return;

        if (item.Primitives.Count > 0 || item.Particles is { Live: true })
            job.Entries.Add(new Entry(z, _sequence++, item, transform, modulate * item.SelfModulate, clip));

        item.SortChildren();
        foreach (CanvasItem child in item.Children)
            Visit(job, child, transform, modulate, z, clip);

        if (item.Mounts is { } mounts)
        {
            foreach (ICanvasMount mount in mounts)
            {
                HostFramePhases.Phase was = Switch(HostFramePhases.Phase.CollectOtherMounts);
                mount.Collect(new MountCollector(this, job, transform, modulate, z, clip));
                Switch(was);
            }
        }
    }

    /// <summary>Places a mount's items relative to the item it hangs under.</summary>
    private sealed class MountCollector(CanvasRenderer renderer, Job job, Transform2D transform, Color modulate, int z, Rect clip)
        : ICanvasCollector
    {
        public void Item(Handle handle, in Transform2D local, Color tint, int layer, Rect2? cut)
        {
            if (renderer._tree.Item(handle) is not { Visible: true } item)
                return;

            HostFramePhases.Phase was = renderer.Switch(HostFramePhases.Phase.CollectCanvas);
            Rect within = cut is { } area ? Intersect(clip, Bounds(transform, area)) : clip;
            renderer.Place(job, item, transform * local * item.Transform, modulate * tint * item.Modulate,
                Math.Clamp(z + layer, -4096, 4096), within);
            renderer.Switch(was);
        }

        public void Begin(MountStep step) => renderer.Switch(step switch
        {
            MountStep.Restructure => HostFramePhases.Phase.CollectUiRestructure,
            _ => HostFramePhases.Phase.CollectUiScan,
        });

        public IResidentTree? Resident(ICanvasMount owner) => renderer.ResidentFor(owner);

        public void Draw(IResidentTree tree) => renderer.DrawResident(job, tree, transform, modulate, z, clip);
    }

    // ---- Building ---------------------------------------------------------

    private void Build()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            _steps.Clear();
            _stepTextures.Clear();
            _vertices.Clear();
            _instances.Clear();
            _slots.Clear();
            _uploads.Clear();
            _stagingCount = 0;
            _placedCount = 0;
            _opaqueReserved = 0;
            _overflow = false;
            PrepareResident();
            for (int j = 0; j < _jobs.Count; j++)
                BuildJob(_jobs[j], j);

            if (!_overflow)
                return;

            // The pool is full of old ranges: start a bigger, empty one and send everything drawn again.
            _poolCapacity = Math.Max(_poolCapacity * 2, (uint)_poolUsed * 2);
            _pool?.Dispose();
            _pool = null;
            _poolUsed = 0;
            _poolEpoch++;
        }
    }

    private void BuildJob(Job job, int index)
    {
        job.FirstStep = _steps.Count;
        job.OpaqueStart = (uint)_opaqueReserved;
        int jobFirst = _placedCount;
        uint sequence = 0;
        int runStart = -1, runTextures = 0;

        void CloseRun()
        {
            if (runStart >= 0 && _placedCount > runStart)
                _steps.Add(new Step(StepKind.Primitives, (uint)runStart, (uint)(_placedCount - runStart), _stepTextures.Count - Slots));
            runStart = -1;
        }

        void OpenRun()
        {
            runStart = _placedCount;
            runTextures = 0;
            for (int i = 0; i < Slots; i++)
                _stepTextures.Add(0);
        }

        // The run's slot for a texture, or -1 when the run is full.
        int SlotOf(ulong texture)
        {
            int first = _stepTextures.Count - Slots;
            for (int i = 0; i < runTextures; i++)
            {
                if (_stepTextures[first + i] == texture)
                    return i;
            }

            if (runTextures == Slots)
                return -1;

            _stepTextures[first + runTextures] = texture;
            return runTextures++;
        }

        // A plain range of a resident item: one instance, its textures in the open run's slots.
        void AddPlain(CanvasItem item, in Entry entry, int from, int to)
        {
            if (runStart < 0)
                OpenRun();

            // Its textures must all fit the run's slots; if not, a new run.
            int slotBase = _slots.Count;
            bool fits = true;
            foreach (ulong texture in item.LocalTextures)
            {
                int slot = SlotOf(texture);
                if (slot < 0)
                {
                    fits = false;
                    break;
                }

                _slots.Add((uint)slot);
            }

            if (!fits)
            {
                _slots.RemoveRange(slotBase, _slots.Count - slotBase);
                CloseRun();
                OpenRun();
                foreach (ulong texture in item.LocalTextures)
                    _slots.Add((uint)Math.Max(0, SlotOf(texture)));
            }

            Transform2D t = entry.Transform;
            int count = to - from;
            _instances.Add(new GpuInstance
            {
                Basis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y),
                Origin = new Vector4(t.Origin.X, t.Origin.Y, MathF.Sqrt(MathF.Abs(t.X.X * t.Y.Y - t.X.Y * t.Y.X)), 0),
                Tint = new Vector4(entry.Color.R, entry.Color.G, entry.Color.B, entry.Color.A),
                Clip = new Vector4(entry.Clip.X, entry.Clip.Y, entry.Clip.X + entry.Clip.W, entry.Clip.Y + entry.Clip.H),
                PoolStart = (uint)(item.PoolStart + from),
                Count = (uint)count,
                OutStart = (uint)_placedCount,
                SlotBase = (uint)slotBase,
                Flags = AllowOpaque,
                OpaqueBase = job.OpaqueStart + (uint)(_placedCount - jobFirst),
                DepthBase = sequence,
            });
            _placedCount += count;
        }

        foreach (Entry entry in job.Entries)
        {
            if (entry.Piece is { } piece)
            {
                CloseRun();
                sequence += AddPiece(job, piece, jobFirst, sequence);
                continue;
            }

            CanvasItem item = entry.Item;
            int total = item.Primitives.Count;

            // The item's draws are its particles' look: drawn once per particle, never on their own.
            if (item.ParticleMesh)
            {
                if (item.Particles is { Live: true } field && total > 0)
                {
                    CloseRun();
                    for (int from = 0; from < total;)
                    {
                        int run = item.Primitives[from].Run;
                        int to = from + 1;
                        while (to < total && item.Primitives[to].Run == run)
                            to++;
                        (CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? values) =
                            item.Material is not null ? (item.Material, null) : run > 0 ? item.Runs[run - 1] : (null, null);
                        sequence += AddEffect(item, entry, from, to, material, values, sequence, field);
                        from = to;
                    }
                }
                continue;
            }

            if (item.Material is not null)
            {
                CloseRun();
                sequence += AddEffect(item, entry, 0, total, item.Material, null, sequence);
            }
            else if (total > 0)
            {
                Resident(item);
                // The item's draws in order, cut where a shaded run starts or ends.
                for (int from = 0; from < total;)
                {
                    int run = item.Primitives[from].Run;
                    int to = from + 1;
                    while (to < total && item.Primitives[to].Run == run)
                        to++;

                    if (run > 0)
                    {
                        CloseRun();
                        (CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? values) = item.Runs[run - 1];
                        sequence += AddEffect(item, entry, from, to, material, values, sequence);
                    }
                    else
                    {
                        AddPlain(item, entry, from, to);
                        sequence += (uint)(to - from);
                    }

                    from = to;
                }
            }

            // Particles draw after the item's own draws, at the next depth.
            if (item.Particles is { Live: true } particles)
            {
                CloseRun();
                _steps.Add(new Step(StepKind.Particles, 0, 0, Clip: entry.Clip, Texture: particles.Strip,
                    Particles: particles, Transform: entry.Transform, Color: entry.Color, Depth: 1f - ++sequence * DepthStep));
            }
        }

        CloseRun();
        job.StepCount = _steps.Count - job.FirstStep;
        // A pre-pass slot for every placed primitive.
        _opaqueReserved += _placedCount - jobFirst;
        job.OpaqueReserved = (uint)(_placedCount - jobFirst);
    }

    /// <summary>
    /// Makes sure the item's primitives are in the pool as they are now: sent
    /// again only when they changed, into the same range while they fit.
    /// </summary>
    private void Resident(CanvasItem item)
    {
        if (item.PoolEpoch == _poolEpoch && item.PoolVersion == item.Version && item.PoolStart >= 0)
            return;

        int count = item.Primitives.Count;

        // The hash only pays when a redraw comes out the same. An item that has
        // differed every time lately (a live graph) skips it, checking again now and then.
        bool check = item.HashMisses < HashMissesBeforeSkipping || ++item.HashSkips % HashRecheck == 0;
        int content = 0;
        if (check)
        {
            var hash = new HashCode();
            hash.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(item.Primitives)));
            content = hash.ToHashCode();
            if (item.PoolHashKnown && item.PoolEpoch == _poolEpoch && item.PoolStart >= 0 && item.PoolHash == content)
            {
                // Drawn again exactly as it was (most drawers clear and redraw every frame): nothing to send.
                item.PoolVersion = item.Version;
                item.HashMisses = 0;
                return;
            }

            item.HashMisses++;
        }

        item.PoolHash = content;
        item.PoolHashKnown = check;
        if (item.PoolEpoch != _poolEpoch || item.PoolStart < 0 || count > item.PoolCapacity)
        {
            int capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(count, 4));
            if (_poolUsed + capacity > _poolCapacity)
                _overflow = true;
            item.PoolStart = _poolUsed;
            item.PoolCapacity = capacity;
            item.PoolEpoch = _poolEpoch;
            _poolUsed += capacity;
        }

        item.PoolVersion = item.Version;
        item.LocalTextures.Clear();
        Reserve(ref _staging, _stagingCount + count);
        for (int i = 0; i < count; i++)
        {
            Primitive p = item.Primitives[i];
            uint local = NoTexture;
            if (p.Texture != 0 && Glyphs?.LayerOf(p.Texture, out int layer) == true)
            {
                local = GlyphLayer | (uint)layer;
            }
            else if (p.Texture != 0)
            {
                int at = item.LocalTextures.IndexOf(p.Texture);
                if (at < 0 && item.LocalTextures.Count < Slots)
                {
                    at = item.LocalTextures.Count;
                    item.LocalTextures.Add(p.Texture);
                }

                if (at >= 0)
                    local = (uint)at;
            }

            p.Write(ref _staging[_stagingCount + i], local);
        }

        _uploads.Add((_stagingCount, item.PoolStart, count));
        _stagingCount += count;
    }

    /// <summary>An item with a material: its primitives as canvas vertices for the effect's pipeline, one draw per texture.</summary>
    /// <summary>
    /// Whether an effect moves its own vertices: its depth is then its own, so
    /// rects after it must draw in order rather than in the pre-pass.
    /// </summary>
    private bool Deforms(CanvasMaterial? material) => _pipelines.Effect(material).Vertex is not null;

    /// <summary>The effect's steps at the draw-order depth after <paramref name="sequence"/>; returns the depth steps it takes.</summary>
    /// <param name="particles">Draws the triangles once per particle of this field instead (a particle mesh); its deform is left out.</param>
    private uint AddEffect(CanvasItem item, in Entry entry, int from, int to, CanvasMaterial? material,
        IReadOnlyDictionary<string, Vector4>? values, uint sequence, ParticleField? particles = null)
    {
        float depth = 1f - (sequence + 1) * DepthStep;
        uint taken = (uint)(to - from);
        float span = 0;
        StepKind kind = particles is null ? StepKind.Effect : StepKind.MeshParticles;
        // A deform's [Grid]: each rect it draws is cut into cells.
        (int columns, int rows) = particles is null ? _pipelines.Effect(material).Compiled.Vertex?.Grid ?? (1, 1) : (1, 1);
        if (particles is null && Deforms(material))
        {
            taken = Math.Max(taken, DeformSlice);
            span = (taken - 1) * DepthStep;
        }

        if (particles is null && columns * rows >= KeptCells)
        {
            KeptMesh mesh = Kept(item, entry, from, to, columns, rows);
            foreach ((uint pieceStart, uint pieceCount, ulong pieceTexture) in mesh.Pieces)
            {
                _steps.Add(new Step(kind, pieceStart, pieceCount,
                    Clip: entry.Clip, Material: material, Values: values, Texture: pieceTexture, Depth: depth, Span: span,
                    Transform: entry.Transform, Mesh: mesh));
            }

            return taken;
        }

        int points = 0;
        int start = _vertices.Count;
        ulong texture = from < to ? item.Primitives[from].Texture : 0;
        for (int i = from; i < to; i++)
        {
            Primitive p = item.Primitives[i];
            if (p.Texture != texture && _vertices.Count > start)
            {
                _steps.Add(new Step(kind, (uint)start, (uint)(_vertices.Count - start),
                    Clip: entry.Clip, Material: material, Values: values, Texture: texture, Depth: depth, Span: span,
                    Particles: particles, Transform: entry.Transform));
                start = _vertices.Count;
            }

            texture = p.Texture;
            p.Tessellate(_vertices, entry.Transform, entry.Color, ref points, columns, rows);
        }

        if (_vertices.Count > start)
        {
            _steps.Add(new Step(kind, (uint)start, (uint)(_vertices.Count - start),
                Clip: entry.Clip, Material: material, Values: values, Texture: texture, Depth: depth, Span: span,
                Particles: particles, Transform: entry.Transform));
        }

        return taken;
    }

    /// <summary>The item's grid vertices for this range, cut again only when what they come from changed.</summary>
    private KeptMesh Kept(CanvasItem item, in Entry entry, int from, int to, int columns, int rows)
    {
        var hash = new HashCode();
        hash.AddBytes(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(item.Primitives).Slice(from, to - from)));
        hash.Add(columns);
        hash.Add(rows);
        int content = hash.ToHashCode();

        if (!_kept.TryGetValue((item, from), out KeptMesh? mesh))
            _kept[(item, from)] = mesh = new KeptMesh();
        mesh.LastFrame = _buildFrame;
        if (mesh.Buffer is not null && mesh.Hash == content && mesh.Transform == entry.Transform && mesh.Color == entry.Color)
            return mesh;

        mesh.Hash = content;
        mesh.Transform = entry.Transform;
        mesh.Color = entry.Color;
        mesh.Vertices.Clear();
        mesh.Pieces.Clear();
        mesh.Pending = true;

        int points = 0, start = 0;
        ulong texture = item.Primitives[from].Texture;
        for (int i = from; i < to; i++)
        {
            Primitive p = item.Primitives[i];
            if (p.Texture != texture && mesh.Vertices.Count > start)
            {
                mesh.Pieces.Add(((uint)start, (uint)(mesh.Vertices.Count - start), texture));
                start = mesh.Vertices.Count;
            }

            texture = p.Texture;
            p.Tessellate(mesh.Vertices, entry.Transform, entry.Color, ref points, columns, rows);
        }

        if (mesh.Vertices.Count > start)
            mesh.Pieces.Add(((uint)start, (uint)(mesh.Vertices.Count - start), texture));
        return mesh;
    }

    /// <summary>Sends kept meshes that were cut again this frame, and lets go of those no longer drawn.</summary>
    private void UploadKept(CommandBuffer commands)
    {
        _keptStale.Clear();
        foreach (((CanvasItem, int) key, KeptMesh mesh) in _kept)
        {
            if (_buildFrame - mesh.LastFrame > KeptFrames)
            {
                _keptStale.Add(key);
                continue;
            }

            if (!mesh.Pending || mesh.Vertices.Count == 0)
                continue;

            mesh.Pending = false;
            uint count = (uint)mesh.Vertices.Count;
            if (mesh.Buffer is null || count > mesh.Capacity)
            {
                mesh.Buffer?.Dispose();
                mesh.Capacity = Math.Max(count, 1 << 10);
                mesh.Buffer = Buffer.Create<CanvasVertex>(_device, "kept grid vertices", BufferUsageFlags.Vertex, mesh.Capacity);
            }

            TransferBuffer staging = GrowTransfer<CanvasVertex>(_device, ref _keptTransfer, ref _keptTransferCapacity, (int)count, "kept grid upload");
            CollectionsMarshal.AsSpan(mesh.Vertices).CopyTo(staging.Map<CanvasVertex>(cycle: true));
            staging.Unmap();
            CopyPass copy = commands.BeginCopyPass();
            copy.UploadToBuffer<CanvasVertex>(staging, mesh.Buffer, 0, 0, count, cycle: true);
            commands.EndCopyPass(copy);
        }

        foreach ((CanvasItem, int) key in _keptStale)
        {
            _kept[key].Buffer?.Dispose();
            _kept.Remove(key);
        }
    }

    private TransferBuffer? _keptTransfer;
    private uint _keptTransferCapacity;

    private static void Reserve<T>(ref T[] array, int count)
    {
        if (count > array.Length)
            Array.Resize(ref array, Math.Max(array.Length * 2, count));
    }

    // ---- GPU --------------------------------------------------------------

    private static Buffer Grow<T>(GraphicsDevice device, ref Buffer? buffer, ref uint capacity, int needed, BufferUsageFlags usage, string name)
        where T : unmanaged
    {
        if (buffer is null || needed > capacity)
        {
            buffer?.Dispose();
            capacity = (uint)Math.Max(needed, Math.Max(capacity * 2, 1 << 12));
            buffer = Buffer.Create<T>(device, name, usage, capacity);
        }

        return buffer;
    }

    private static TransferBuffer GrowTransfer<T>(GraphicsDevice device, ref TransferBuffer? buffer, ref uint capacity, int needed, string name)
        where T : unmanaged
    {
        if (buffer is null || needed > capacity)
        {
            buffer?.Dispose();
            capacity = (uint)Math.Max(needed, Math.Max(capacity * 2, 1 << 12));
            buffer = TransferBuffer.Create<T>(device, name, TransferBufferUsage.Upload, capacity);
        }

        return buffer;
    }

    /// <summary>
    /// Sends what changed: recordings into their pool ranges, the frame's
    /// instances and slots.
    /// </summary>
    private void Upload(CommandBuffer commands)
    {
        _pool ??= Buffer.Create<GpuPrimitive>(_device, "primitive pool", BufferUsageFlags.ComputeStorageRead, _poolCapacity);
        Grow<GpuPrimitive>(_device, ref _placed, ref _placedCapacity, Math.Max(1, _placedCount),
            BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, "placed primitives");
        Grow<GpuPrimitive>(_device, ref _opaqueBuffer, ref _opaqueCapacity, Math.Max(1, _opaqueReserved),
            BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead, "opaque primitives");
        // Resident trees' instances follow the CPU's, written by their resolve pass.
        Grow<GpuInstance>(_device, ref _instanceBuffer, ref _instanceCapacity, Math.Max(1, _instances.Count + ResidentInstances),
            BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite, "primitive instances");
        Grow<uint>(_device, ref _slotBuffer, ref _slotCapacity, Math.Max(1, _slots.Count),
            BufferUsageFlags.ComputeStorageRead, "texture slots");

        CopyPass copy = commands.BeginCopyPass();
        if (_stagingCount > 0)
        {
            TransferBuffer staging = GrowTransfer<GpuPrimitive>(_device, ref _stagingTransfer, ref _stagingTransferCapacity, _stagingCount, "pool upload");
            _staging.AsSpan(0, _stagingCount).CopyTo(staging.Map<GpuPrimitive>(cycle: true));
            staging.Unmap();
            foreach ((int from, int to, int count) in _uploads)
            {
                if (count > 0)
                    copy.UploadToBuffer<GpuPrimitive>(staging, _pool, (uint)from, (uint)to, (uint)count, cycle: false);
            }
        }

        if (_instances.Count > 0)
        {
            TransferBuffer instances = GrowTransfer<GpuInstance>(_device, ref _instanceTransfer, ref _instanceTransferCapacity, _instances.Count, "instance upload");
            CollectionsMarshal.AsSpan(_instances).CopyTo(instances.Map<GpuInstance>(cycle: true));
            instances.Unmap();
            copy.UploadToBuffer<GpuInstance>(instances, _instanceBuffer!, 0, 0, (uint)_instances.Count, cycle: true);
        }

        if (_slots.Count > 0)
        {
            TransferBuffer slots = GrowTransfer<uint>(_device, ref _slotTransfer, ref _slotTransferCapacity, _slots.Count, "slot upload");
            CollectionsMarshal.AsSpan(_slots).CopyTo(slots.Map<uint>(cycle: true));
            slots.Unmap();
            copy.UploadToBuffer<uint>(slots, _slotBuffer!, 0, 0, (uint)_slots.Count, cycle: true);
        }

        commands.EndCopyPass(copy);

        if (_vertices.Count == 0)
            return;

        if (_buffer is null || _vertices.Count > _capacity)
        {
            _buffer?.Dispose();
            _transfer?.Dispose();
            _capacity = (uint)Math.Max(_vertices.Count, Math.Max(_capacity * 2, 1 << 12));
            _buffer = Buffer.Create<CanvasVertex>(_device, "canvas vertices", BufferUsageFlags.Vertex, _capacity);
            _transfer = TransferBuffer.Create<CanvasVertex>(_device, "canvas upload", TransferBufferUsage.Upload, _capacity);
        }

        Span<CanvasVertex> vertices = _transfer!.Map<CanvasVertex>(cycle: true);
        CollectionsMarshal.AsSpan(_vertices).CopyTo(vertices);
        _transfer.Unmap();

        CopyPass vertexCopy = commands.BeginCopyPass();
        vertexCopy.UploadToBuffer<CanvasVertex>(_transfer, _buffer, 0, 0, (uint)_vertices.Count, cycle: true);
        commands.EndCopyPass(vertexCopy);
    }

    /// <summary>Places every instance's primitives for the frame (primitive_expand.slang).</summary>
    private void Expand(CommandBuffer commands)
    {
        if (_instances.Count == 0 && ResidentInstances == 0)
            return;

        ResolveResident(commands);
        ComputePass pass = commands.BeginComputePass([],
        [
            new StorageBufferReadWriteBinding(_placed!, cycle: true),
            new StorageBufferReadWriteBinding(_opaqueBuffer!, cycle: true),
        ]);
        pass.BindComputePipeline(_expand);
        if (_instances.Count > 0)
        {
            pass.BindStorageBuffers(_pool!, _instanceBuffer!, _slotBuffer!, _slotBuffer!);
            commands.PushComputeUniformData(new ExpandUniforms { Instances = (uint)_instances.Count, DepthStep = DepthStep });
            pass.Dispatch((uint)_instances.Count, 1, 1);
        }

        // Each resident tree's instances read its own run slots.
        foreach (ResidentGpu gpu in _residentFrame)
        {
            uint count = (uint)gpu.Tree.Entries.Count;
            if (count == 0)
                continue;

            pass.BindStorageBuffers(_pool!, _instanceBuffer!, _slotBuffer!, gpu.RunSlots!);
            commands.PushComputeUniformData(new ExpandUniforms { Instances = count, DepthStep = DepthStep, First = gpu.InstanceBase });
            pass.Dispatch(count, 1, 1);
        }

        commands.EndComputePass(pass);
    }


    /// <summary>
    /// Runs the globals' per-frame writers, then copies each global that changed to the GPU
    /// (growing its buffer to fit) and lines them up in binding order for the frame's draws.
    /// </summary>
    private void UploadGlobals(CommandBuffer commands)
    {
        ShaderGlobals.RunEveryFrame();
        if (_blank is null)
        {
            _blank = Buffer.Create<byte>(_device, "blank global",
                BufferUsageFlags.GraphicsStorageRead | BufferUsageFlags.ComputeStorageRead, SlangShaders.MaxStateBytes);
            CopyPass zero = commands.BeginCopyPass();
            zero.UploadToBuffer(_zeros, _blank, cycle: false);
            commands.EndCopyPass(zero);
        }

        IReadOnlyList<string> names = SlangShaders.Globals;
        if (_bound.Length != names.Count + 1)
            _bound = new Buffer[names.Count + 1];
        for (int i = 0; i < names.Count; i++)
            _bound[i] = ShaderGlobals.Find(names[i]) is { Count: > 0 } global ? Uploaded(commands, global) : _blank;
        _bound[names.Count] = _blank;
    }

    private Buffer Uploaded(CommandBuffer commands, ShaderGlobal global)
    {
        if (!_globalBuffers.TryGetValue(global.Name, out GlobalBuffer? held))
            _globalBuffers[global.Name] = held = new GlobalBuffer();
        if (held.Version == global.Version)
            return held.Buffer;

        held.Version = global.Version;
        ReadOnlySpan<byte> bytes = global.Bytes;
        if (held.Capacity < bytes.Length)
        {
            held.Buffer?.Dispose();
            held.Transfer?.Dispose();
            held.Capacity = Math.Max(bytes.Length, held.Capacity * 2);
            held.Buffer = Buffer.Create<byte>(_device, global.Name,
                BufferUsageFlags.GraphicsStorageRead | BufferUsageFlags.ComputeStorageRead, (uint)held.Capacity);
            held.Transfer = TransferBuffer.Create<byte>(_device, global.Name + " upload", TransferBufferUsage.Upload, (uint)held.Capacity);
        }

        Span<byte> mapped = held.Transfer.Map<byte>(cycle: true);
        bytes.CopyTo(mapped);
        held.Transfer.Unmap();

        CopyPass copy = commands.BeginCopyPass();
        copy.UploadToBuffer<byte>(held.Transfer, held.Buffer, 0, 0, (uint)bytes.Length, cycle: true);
        commands.EndCopyPass(copy);
        return held.Buffer;
    }

    /// <summary>The frame's globals, then <paramref name="state"/> (or zeros) when a stage binds one more: <paramref name="count"/> in all.</summary>
    private Span<Buffer> Bound(int count, Buffer? state = null)
    {
        int globals = _bound.Length - 1;
        _bound[globals] = state ?? _blank!;
        return _bound.AsSpan(0, Math.Min(count, _bound.Length));
    }

    /// <summary>A pass on a job's target and the depth buffer; with <paramref name="msaa"/>, on the multisampled frame, resolved into the target when it ends.</summary>
    private RenderPass BeginPass(CommandBuffer commands, Job job, bool msaa, bool load)
    {
        var depth = new DepthStencilTargetInfo
        {
            Texture = msaa ? _msaaDepth!.Handle : _depth.Handle,
            ClearDepth = 1f,
            LoadOp = load ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.DontCare,
        };
        if (!msaa)
            return commands.BeginRenderPass(depth, load ? new ColorTargetInfo(job.Target, LoadOp.Load) : new ColorTargetInfo(job.Target, job.Clear));

        // Resolved and kept: a screen copy mid-frame reads the resolve, and the pass then carries on drawing.
        return commands.BeginRenderPass(depth, new ColorTargetInfo
        {
            Texture = _msaa!,
            LoadOp = load ? LoadOp.Load : LoadOp.Clear,
            ClearColor = job.Clear,
            StoreOp = StoreOp.ResolveAndStore,
            ResolveTexture = job.Target,
        });
    }

    private GraphicsPipeline PrimitivePipeline(bool opaque, SampleCount samples)
    {
        if (_primitivePipelines.TryGetValue((opaque, samples), out GraphicsPipeline? pipeline))
            return pipeline;

        pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
        {
            VertexShader = _primitiveVertex,
            FragmentShader = opaque ? _primitiveOpaque : _primitiveShade,
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
        _primitivePipelines[(opaque, samples)] = pipeline;
        return pipeline;
    }

    private void DrawPrimitives(CommandBuffer commands, RenderPass pass, SampleCount samples, bool opaque, uint first, uint count, int firstTexture)
    {
        pass.BindGraphicsPipeline(PrimitivePipeline(opaque, samples));
        pass.BindVertexStorageBuffers(opaque ? _opaqueBuffer! : _placed!);
        pass.SetScissor(FullClip);
        if (!opaque)
        {
            Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[Slots + 1];
            for (int i = 0; i < Slots; i++)
            {
                ulong id = _stepTextures[firstTexture + i];
                samplers[i] = new TextureSamplerBinding(id != 0 ? Textures.GetValueOrDefault(id) ?? _white : _white, _sampler);
            }

            _noLayers ??= Texture.Create2DArray(_device, "no glyph layers", 1, 1, 1, TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);
            samplers[Slots] = new TextureSamplerBinding(Glyphs?.Layers ?? _noLayers, _sampler);
            pass.BindFragmentSamplers(samplers);
        }

        commands.PushVertexUniformData(new DrawUniforms { Width = _width, Height = _height, First = first });
        pass.DrawPrimitives(count * 6, 1, 0, 0);
        LastBatchCount++;
    }

    /// <summary>Whether any effect the job draws samples the depth, in its shade or its deform.</summary>
    private bool ReadsDepth(Job job)
    {
        for (int s = job.FirstStep; s < job.FirstStep + job.StepCount; s++)
        {
            Step step = _steps[s];
            if (step.Kind is StepKind.Primitives or StepKind.Particles)
                continue;
            CanvasEffect effect = _pipelines.Effect(step.Material);
            if (effect.Compiled.ReadsDepth || effect.Compiled.Vertex?.ReadsDepth == true)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The job's opaque pre-pass again, depth only, into <see cref="_depthRead"/>: the depth the
    /// main pass tests against, in a texture effects can sample (MSAA depth can't be resolved).
    /// </summary>
    private void FillDepth(CommandBuffer commands, Job job)
    {
        using GpuScope scope = commands.Scope("depth for reads");
        RenderPass pass = commands.BeginRenderPass(new DepthStencilTargetInfo
        {
            Texture = _depthRead.Handle,
            ClearDepth = 1f,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.DontCare,
        });
        if (job.OpaqueReserved > 0)
        {
            pass.BindGraphicsPipeline(DepthOnlyPipeline());
            pass.BindVertexStorageBuffers(_opaqueBuffer!);
            pass.SetScissor(FullClip);
            commands.PushVertexUniformData(new DrawUniforms { Width = _width, Height = _height, First = job.OpaqueStart });
            pass.DrawPrimitives(job.OpaqueReserved * 6, 1, 0, 0);
        }

        commands.EndRenderPass(pass);
    }

    private GraphicsPipeline DepthOnlyPipeline() => _depthOnly ??= GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
    {
        VertexShader = _primitiveVertex,
        FragmentShader = _primitiveOpaque,
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

    private void Draw(CommandBuffer commands, Job job, float time, bool msaa)
    {
        SampleCount samples = msaa ? _samples : SampleCount.One;
        if (job.StepCount > 0 && ReadsDepth(job))
            FillDepth(commands, job);
        RenderPass pass = BeginPass(commands, job, msaa, load: false);
        if (job.StepCount == 0)
        {
            commands.EndRenderPass(pass);
            return;
        }

        if (job.OpaqueReserved > 0)
            DrawPrimitives(commands, pass, samples, opaque: true, job.OpaqueStart, job.OpaqueReserved, 0);

        var frame = FrameUniforms.Now(_width, _height, time);
        bool drawnSinceCopy = true, copied = false, effectState = false;
        Span<TextureSamplerBinding> samplers = stackalloc TextureSamplerBinding[8];
        for (int s = job.FirstStep; s < job.FirstStep + job.StepCount; s++)
        {
            Step step = _steps[s];
            switch (step.Kind)
            {
                case StepKind.Primitives:
                    DrawPrimitives(commands, pass, samples, opaque: false, step.Start, step.Count, step.FirstTexture);
                    effectState = false;
                    drawnSinceCopy = true;
                    continue;

                case StepKind.Particles:
                    pass.SetScissor(step.Clip);
                    Particles.Draw(commands, pass, step.Particles!, step.Transform, step.Color, _width, _height,
                        Textures.GetValueOrDefault(step.Texture) ?? _white, _sampler, samples, step.Depth);
                    LastBatchCount++;
                    effectState = false;
                    drawnSinceCopy = true;
                    continue;
            }

            CanvasEffect effect = _pipelines.Effect(step.Material);
            string[] names = effect.Compiled.Samplers;
            // The first screen read copies the target and every later read in the pass shares that copy.
            if (effect.Compiled.ReadsScreen && !copied)
            {
                copied = true;
                commands.EndRenderPass(pass);
                CopyScreen(commands, job.Target);
                pass = BeginPass(commands, job, msaa, load: true);
                effectState = false;
                drawnSinceCopy = false;
            }

            commands.PushVertexUniformData(new ViewUniforms { Width = _width, Height = _height, Depth = step.Depth, Span = step.Span });
            if (!effectState)
            {
                // A deforming effect's vertex stage reads the frame too; only its clocks matter there.
                commands.PushVertexUniformData(frame, SlangShaders.VertexFrameSlot);
                commands.PushFragmentUniformData(frame);
                effectState = true;
            }

            bool instanced = step.Kind == StepKind.MeshParticles;
            GraphicsPipeline pipeline = instanced
                ? _pipelines.ParticlePipeline(effect, step.Material, samples)
                : _pipelines.Pipeline(effect, step.Material, samples, depth: true);
            if (step.Material?.Custom is { UsesConstant: true } custom)
                pass.SetBlendConstants(new MwColor(custom.Constant.R, custom.Constant.G, custom.Constant.B, custom.Constant.A));
            pass.BindGraphicsPipeline(pipeline);
            pass.BindVertexBuffers(step.Mesh?.Buffer ?? _buffer!);

            pass.SetScissor(step.Clip);
            int count = Math.Clamp(names.Length, 1, samplers.Length);
            samplers[0] = new TextureSamplerBinding(Textures.GetValueOrDefault(step.Texture) ?? _white, _sampler);
            for (int i = 1; i < count; i++)
            {
                if (i == SlangShaders.FragmentDepth && names[i] == "depthTexture")
                {
                    samplers[i] = new TextureSamplerBinding(_depthRead, _device.PointSampler);
                    continue;
                }

                Texture? texture = i == 1 && names[i] == "screenTexture"
                    ? _screen
                    : names[i] is { } name && step.Material?.Textures.TryGetValue(name, out Handle id) == true
                        ? Textures.GetValueOrDefault(id.Id)
                        : null;
                samplers[i] = new TextureSamplerBinding(texture ?? _white, _sampler);
            }

            pass.BindFragmentSamplers(samplers[..count]);
            if (effect.Compiled.StorageBuffers > 0)
                pass.BindFragmentStorageBuffers(Bound(effect.Compiled.StorageBuffers,
                    step.Material is { } kept && _states.TryGetValue(kept, out Buffer? state) ? state : null));
            if (effect.Compiled.ParamsSize > 0)
                PushParams(commands, effect.Compiled, step.Material, step.Values);
            if (instanced)
            {
                // Each particle places a copy of the triangles; the effect's own deform sits out.
                pass.BindVertexStorageBuffers(step.Particles!.Buffer, Particles.Effects);
                Transform2D place = step.Transform;
                commands.PushVertexUniformData(new PlaceUniforms
                {
                    OriginX = place.Origin.X, OriginY = place.Origin.Y,
                    XX = place.X.X, XY = place.X.Y, YX = place.Y.X, YY = place.Y.Y,
                }, 1);
                pass.DrawPrimitives(step.Count, ParticleField.Capacity, step.Start, 0);
                LastBatchCount++;
                drawnSinceCopy = true;
                continue;
            }

            if (effect.Compiled.Vertex is { } vertex)
            {
                if (vertex.Samplers.Length > 0)
                    pass.BindVertexSamplers(new TextureSamplerBinding(_depthRead, _device.PointSampler));
                if (vertex.StorageBuffers > 0)
                    pass.BindVertexStorageBuffers(Bound(vertex.StorageBuffers));
                if (vertex.ParamsSize > 0)
                    PushParams(commands, vertex, step.Material, step.Values, stage: ParamsStage.Vertex);
            }

            pass.DrawPrimitives(step.Count, 1, step.Start, 0);
            LastBatchCount++;
            drawnSinceCopy = true;
        }

        commands.EndRenderPass(pass);
    }

    /// <summary>
    /// Runs each kept-state effect's <c>update</c> once, on one GPU thread, for
    /// every material drawn this frame, so all its pixels read the same state.
    /// </summary>
    private void UpdateStates(CommandBuffer commands, float dt, float time)
    {
        _updated.Clear();
        foreach (Step step in _steps)
        {
            // Code that asked for state, or a lab-only effect file (Docs/shaders.md §14).
            if (step.Material is not { } material || !(material.KeepsState || (material.Source is null && SlangShaders.IsLabOnly(material.Shader)))
                || !_updated.Add(material))
                continue;

            CanvasEffect effect = _pipelines.Effect(material);
            if (effect.Update is not { } update || effect.Compiled.Update is not { } compiled)
                continue;

            if (!_states.TryGetValue(material, out Buffer? state))
            {
                state = Buffer.Create<byte>(_device, "shader state",
                    BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.GraphicsStorageRead,
                    SlangShaders.MaxStateBytes);
                CopyPass copy = commands.BeginCopyPass();
                copy.UploadToBuffer(_zeros, state, cycle: false);
                commands.EndCopyPass(copy);
                _states[material] = state;
            }

            commands.PushComputeUniformData(new UpdateUniforms { Dt = dt, Time = time });
            if (compiled.ParamsSize > 0)
                PushParams(commands, compiled, material, null, stage: ParamsStage.Compute);
            ComputePass pass = commands.BeginComputePass(new StorageBufferReadWriteBinding(state, cycle: false));
            pass.BindComputePipeline(update);
            if (compiled.StorageBuffers > 0)
                pass.BindStorageBuffers(Bound(compiled.StorageBuffers));
            pass.Dispatch(1, 1, 1);
            commands.EndComputePass(pass);
        }
    }

    /// <summary>
    /// The target as it stands into the screen texture, with Gaussian-filtered
    /// mips for blurred reads. Leaves fragment uniform slot 0 changed.
    /// </summary>
    private void CopyScreen(CommandBuffer commands, Texture target)
    {
        using GpuScope scope = commands.Scope("screen copy + mips");
        commands.Blit(target, _screen, Filter.Linear);
        _mips.Build(commands, _screen, _width, _height);
        LastCopyCount++;
    }

    /// <summary>Packs an effect's values (its defaults, then the material's, then the item's own) into uniform buffer 1.</summary>
    /// <summary>Which stage a values block goes to.</summary>
    private enum ParamsStage { Fragment, Vertex, Compute }

    private static unsafe void PushParams(CommandBuffer commands, CompiledShader compiled, CanvasMaterial? material, IReadOnlyDictionary<string, Vector4>? instance, ParamsStage stage = ParamsStage.Fragment)
    {
        Span<byte> bytes = stackalloc byte[compiled.ParamsSize];
        bytes.Clear();
        foreach (ShaderParam param in compiled.Params)
        {
            if (param.IsMatrix)
            {
                PackMatrix(bytes, param, material);
                continue;
            }

            Vector4 value = param.Default;
            if (material?.Values.TryGetValue(param.Name, out Vector4 set) == true)
                value = set;
            if (instance?.TryGetValue(param.Name, out Vector4 own) == true)
                value = own;

            Span<float> floats = [value.X, value.Y, value.Z, value.W];
            for (int i = 0; i < Math.Min(4, param.Size / 4); i++)
            {
                Span<byte> slot = bytes.Slice(param.Offset + i * 4, 4);
                if (param.IsInt)
                    BitConverter.TryWriteBytes(slot, (int)MathF.Round(floats[i]));
                else
                    BitConverter.TryWriteBytes(slot, floats[i]);
            }
        }

        fixed (byte* data = bytes)
        {
            switch (stage)
            {
                case ParamsStage.Compute:
                    commands.PushComputeUniformData(data, (uint)bytes.Length, 1);
                    break;
                case ParamsStage.Vertex:
                    commands.PushVertexUniformData(data, (uint)bytes.Length, 1);
                    break;
                default:
                    commands.PushFragmentUniformData(data, (uint)bytes.Length, 1);
                    break;
            }
        }
    }

    /// <summary>A matrix value, row by row, each row 16 bytes on from the last as Slang lays it out; the identity when the material sets none.</summary>
    private static void PackMatrix(Span<byte> bytes, ShaderParam param, CanvasMaterial? material)
    {
        System.Numerics.Matrix4x4 value = material?.Matrices.TryGetValue(param.Name, out var set) == true
            ? set
            : System.Numerics.Matrix4x4.Identity;
        int rows = Math.Min(param.Rows, 4), columns = Math.Min(param.Columns, 4);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int at = param.Offset + row * 16 + column * 4;
                if (at + 4 > bytes.Length)
                    continue;
                Span<byte> slot = bytes.Slice(at, 4);
                if (param.IsInt)
                    BitConverter.TryWriteBytes(slot, (int)MathF.Round(value[row, column]));
                else
                    BitConverter.TryWriteBytes(slot, value[row, column]);
            }
        }
    }

    private static Rect Bounds(Transform2D t, Rect2 rect)
    {
        Vector2 a = t * rect.Position;
        Vector2 b = t * new Vector2(rect.End.X, rect.Position.Y);
        Vector2 c = t * rect.End;
        Vector2 d = t * new Vector2(rect.Position.X, rect.End.Y);
        float minX = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        float minY = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        float maxX = MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X));
        float maxY = MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y));
        int x = (int)MathF.Floor(minX), y = (int)MathF.Floor(minY);
        return new Rect { X = x, Y = y, W = (int)MathF.Ceiling(maxX) - x, H = (int)MathF.Ceiling(maxY) - y };
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        int x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.W, b.X + b.W), bottom = Math.Min(a.Y + a.H, b.Y + b.H);
        return new Rect { X = x, Y = y, W = Math.Max(0, right - x), H = Math.Max(0, bottom - y) };
    }

    public void Dispose()
    {
        foreach (KeptMesh mesh in _kept.Values)
            mesh.Buffer?.Dispose();
        _keptTransfer?.Dispose();
        _pipelines.Dispose();
        Particles.Dispose();
        foreach (GraphicsPipeline pipeline in _primitivePipelines.Values)
            pipeline.Dispose();
        _primitiveVertex.Dispose();
        _primitiveShade.Dispose();
        _primitiveOpaque.Dispose();
        _white.Dispose();
        _noLayers?.Dispose();
        _screen.Dispose();
        _msaa?.Dispose();
        _msaaDepth?.Dispose();
        _depth.Dispose();
        _depthRead.Dispose();
        _depthOnly?.Dispose();
        _mips.Dispose();
        _sampler.Dispose();
        _zeros.Dispose();
        foreach (Buffer state in _states.Values)
            state.Dispose();
        _buffer?.Dispose();
        _transfer?.Dispose();
        _expand.Dispose();
        DisposeResident();
        _pool?.Dispose();
        _placed?.Dispose();
        _opaqueBuffer?.Dispose();
        _instanceBuffer?.Dispose();
        _slotBuffer?.Dispose();
        _stagingTransfer?.Dispose();
        _instanceTransfer?.Dispose();
        _slotTransfer?.Dispose();
        foreach (GlobalBuffer held in _globalBuffers.Values)
        {
            held.Buffer?.Dispose();
            held.Transfer?.Dispose();
        }

        _blank?.Dispose();
    }
}
