using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Rendering.Passes;
using Xanadu.Rendering.Text;
using EntityStore = Friflo.Engine.ECS.EntityStore;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Rendering;

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
/// The frame runs as passes over one <see cref="CanvasFrame"/>: collect
/// (<see cref="CanvasCollect"/>), build (<see cref="StepBuilder"/>), upload, expand
/// (<see cref="PlacedPrimitives"/>), grids, globals, graph preludes (<see cref="GraphRunner"/>),
/// particles, then each job's draw (<see cref="DrawPass"/>). What is kept between frames
/// lives in one entity store: materials, kept meshes, resident trees and globals.
/// </para>
/// </remarks>
internal sealed class CanvasRenderer : IDisposable
{
    /// <summary>The MSAA sample count asked for; drops to what the GPU can do.</summary>
    public const SampleCount WantedSamples = FrameTargets.WantedSamples;

    private readonly CanvasTree _tree;
    private readonly CanvasFrame _frame = new();
    private readonly List<(CanvasItem Root, Texture Target)> _offscreen = [];

    /// <summary>The GPU state the renderer keeps between frames, one entity per thing kept (materials, meshes, trees, globals).</summary>
    private readonly EntityStore _cache = new();

    private readonly FrameTargets _targets;
    private readonly PrimitivePipelines _primitives;
    private readonly PrimitivePool _pool;
    private readonly PlacedPrimitives _placed;
    private readonly GpuArray<CanvasVertex> _vertices;
    private readonly MaterialStore _materials;
    private readonly GlobalBindings _globals;
    private readonly KeptMeshes _keptMeshes;
    private readonly ResidentTrees _residents;
    private readonly GridCutter _grids;
    private readonly CanvasCollect _collect;
    private readonly StepBuilder _builder;
    private readonly GraphRunner _graphs;
    private readonly ExitDraws _exits;
    private readonly DrawPass _draw;

    private float _lastTime = float.NaN;

    public CanvasRenderer(GraphicsDevice device, CanvasTree tree, TextureFormat format, uint width, uint height)
    {
        _tree = tree;
        _targets = new FrameTargets(device, format, width, height);
        _primitives = new PrimitivePipelines(device, format, _targets.DepthFormat);
        Pipelines = new CanvasPipelines(device, format) { DepthFormat = _targets.DepthFormat, MainSamples = _targets.Samples };
        Particles = new CanvasParticles(device, tree, format) { DepthFormat = _targets.DepthFormat };
        _pool = new PrimitivePool(device);
        _placed = new PlacedPrimitives(device);
        _vertices = new GpuArray<CanvasVertex>(device, "canvas vertices", BufferUsageFlags.Vertex);

        _materials = new MaterialStore(_cache);
        _globals = new GlobalBindings(device, _cache);
        _keptMeshes = new KeptMeshes(device, _cache);
        _residents = new ResidentTrees(device, tree, _cache);
        tree.Unmounted += _residents.Drop;
        Pipelines.Forgotten += _materials.Forget;

        _grids = new GridCutter(device, _frame.Steps);
        _collect = new CanvasCollect(tree, _residents, _targets.FullClip);
        _builder = new StepBuilder(_frame, Pipelines, _pool, _placed, _residents, _keptMeshes, _grids);
        _graphs = new GraphRunner(device, _frame, _targets, Pipelines, _materials, _globals);
        _exits = new ExitDraws(device, _targets, Pipelines, Textures, Particles, _materials, _globals, _grids, _vertices, _graphs.Indirect);
        _draw = new DrawPass(_frame, _targets, _primitives, Pipelines, _placed, _pool, Textures, Particles, _graphs, _exits);
    }

    /// <summary>Textures the game can draw by id, minted from the tree so they never clash with items.</summary>
    public Dictionary<ulong, Texture> Textures { get; } = [];

    public CanvasPipelines Pipelines { get; }

    public CanvasParticles Particles { get; }

    /// <summary>The text atlas, whose pages plain primitives read from its layer array instead of a run slot.</summary>
    public TextAtlas? Glyphs { get => _pool.Glyphs; set => _pool.Glyphs = value; }

    /// <summary>Draw calls in the last frame.</summary>
    public int LastBatchCount => _draw.BatchCount;

    /// <summary>Screen copies in the last frame.</summary>
    public int LastCopyCount => _draw.CopyCount;

    /// <summary>Offscreen passes in the last frame.</summary>
    public int LastOffscreenCount => Math.Max(0, _frame.Jobs.Count - 1);

    /// <summary>Primitives placed in the last frame, and how many were sent up because they changed.</summary>
    public int LastPrimitiveCount => _placed.PlacedCount;

    public int LastUploadCount => _pool.ChangedCount;

    /// <summary>Makes a target the size of the frame, for an offscreen root.</summary>
    public Texture CreateTarget(string name) => _targets.CreateTarget(name);

    /// <summary>Draws <paramref name="root"/> (an item with no parent) into <paramref name="target"/> each frame, before the tree.</summary>
    public void AddOffscreen(CanvasItem root, Texture target) => _offscreen.Add((root, target));

    public void RemoveOffscreen(CanvasItem root) => _offscreen.RemoveAll(entry => entry.Root == root);

    /// <summary>Draws the whole frame into <paramref name="target"/>, cleared to <paramref name="clear"/>; <paramref name="time"/> is an effect's TIME.</summary>
    public void Render(CommandBuffer commands, Texture target, MwColor clear, float time, HostFramePhases? phases = null)
    {
        long lap = HostFramePhases.Now;
        _residents.BeginFrame();
        _frame.Jobs.Clear();
        foreach ((CanvasItem root, Texture offscreen) in _offscreen)
            _frame.Jobs.Add(_collect.Collect(root, offscreen, new MwColor(0, 0, 0, 0), phases));
        _frame.Jobs.Add(_collect.Collect(_tree.Root, target, clear, phases));
        phases?.Lap(HostFramePhases.Phase.Collect, ref lap);

        _frame.Number++;
        _builder.Build();
        phases?.Lap(HostFramePhases.Phase.Build, ref lap);
        using (commands.Scope("upload primitives"))
        {
            Upload(commands);
            _keptMeshes.Upload(commands, _frame.Number);
            _residents.Upload(commands, _placed.Instances.Count);
        }
        using (commands.Scope("expand primitives"))
            _placed.Expand(commands, _pool, _residents);
        using (commands.Scope("cut grids"))
            _grids.Cut(commands);
        using (commands.Scope("upload globals"))
            _globals.Upload(commands);
        phases?.Lap(HostFramePhases.Phase.Upload, ref lap);

        // The particles and kept state move by the frame's time, before any pass draws them.
        _frame.Dt = float.IsNaN(_lastTime) ? 0f : Math.Clamp(time - _lastTime, 0f, 0.1f);
        _frame.Time = time;
        _lastTime = time;
        using (commands.Scope("shader graphs"))
            _graphs.Prelude(commands);
        using (commands.Scope("particles step"))
            Particles.Step(commands, _frame.Dt);
        phases?.Lap(HostFramePhases.Phase.ShaderStates, ref lap);

        _draw.BatchCount = 0;
        _draw.CopyCount = 0;
        foreach (Job job in _frame.Jobs)
        {
            using (commands.Scope(job.Target == target ? "main target" : "offscreen target"))
                _draw.Draw(commands, job, time, job.Target == target && _targets.Msaa is not null);
        }

        phases?.Lap(HostFramePhases.Phase.DrawPasses, ref lap);
    }

    /// <summary>Sends what changed: recordings into their pool ranges, the frame's instances, slots and effect vertices.</summary>
    private void Upload(CommandBuffer commands)
    {
        CopyPass copy = commands.BeginCopyPass();
        _pool.Upload(copy);
        _placed.Upload(copy, _residents.Instances);
        _vertices.Upload(copy, CollectionsMarshal.AsSpan(_frame.Vertices));
        commands.EndCopyPass(copy);
    }

    public void Dispose()
    {
        _keptMeshes.Dispose();
        Pipelines.Dispose();
        Particles.Dispose();
        _primitives.Dispose();
        _targets.Dispose();
        _materials.Dispose();
        _graphs.Dispose();
        _exits.Dispose();
        _vertices.Dispose();
        _pool.Dispose();
        _placed.Dispose();
        _residents.Dispose();
        _grids.Dispose();
        _globals.Dispose();
    }
}
