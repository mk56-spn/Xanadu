using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Rendering.Passes;
using Xanadu.Rendering.Text;
using Xanadu.Ui.Systems.Canvas;
using EntityStore = Friflo.Engine.ECS.EntityStore;
using MwColor = MoonWorks.Graphics.Color;

namespace Xanadu.Rendering;

/// <summary>
/// Draws a <see cref="CanvasTree"/> (Docs/host.md §5.3). Each frame it walks
/// the tree in order and turns every visible item's primitives into GPU
/// records in pixels; the canvas stack's draws pull them by copy and shape them in
/// the fragment stage (Assets/Shaders/Engine/canvas/draws.slang), so plain content is a handful of
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
/// (<see cref="CanvasCollect"/>, then the materials' views, <see cref="MaterialViews"/>), build (<see cref="StepBuilder"/>), upload, the
/// canvas stack (<see cref="CanvasStack"/>: the pool, instances and resident trees placed on the GPU), grids, globals, graph preludes (<see cref="GraphRunner"/>),
/// particles, then each job's draw (<see cref="DrawPass"/>); the views' preludes run and their jobs draw before the rest. What is kept between frames
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

    /// <summary>The post list's items, one full-screen rect each, by the material it shows, in order.</summary>
    private readonly List<(CanvasMaterial Material, CanvasItem Item)> _post = [];

    /// <summary>The GPU state the renderer keeps between frames, one entity per thing kept (materials, meshes, trees, globals).</summary>
    private readonly EntityStore _cache = new();

    private readonly FrameTargets _targets;
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
    private readonly MaterialViews _views;
    private readonly GraphRunner _graphs;
    private readonly ExitDraws _exits;
    private readonly CopiesPipelines _copiesPipelines;
    private readonly CopiedMeshes _copiedMeshes;
    private readonly CopyDraws _copies;
    private readonly CanvasStack _canvas;
    private readonly DrawPass _draw;

    private float _lastTime = float.NaN;

    public CanvasRenderer(GraphicsDevice device, CanvasTree tree, TextureFormat format, uint width, uint height)
    {
        _tree = tree;
        _targets = new FrameTargets(device, format, width, height);
        Pipelines = new CanvasPipelines(device, format) { DepthFormat = _targets.DepthFormat, MainSamples = _targets.Samples };
        Particles = new CanvasParticles(device, tree, format) { DepthFormat = _targets.DepthFormat };
        _pool = new PrimitivePool();
        _placed = new PlacedPrimitives();
        _vertices = new GpuArray<CanvasVertex>(device, "canvas vertices", BufferUsageFlags.Vertex);

        _materials = new MaterialStore(_cache);
        _globals = new GlobalBindings(device, _cache);
        _keptMeshes = new KeptMeshes(device, _cache);
        _residents = new ResidentTrees(tree, _cache);
        tree.Unmounted += _residents.Drop;
        Pipelines.Forgotten += _materials.Forget;

        _grids = new GridCutter(device, _frame.Steps);
        _collect = new CanvasCollect(tree, _residents, _targets.FullClip);
        _copiesPipelines = new CopiesPipelines(device, format, _targets.DepthFormat);
        _copiedMeshes = new CopiedMeshes(device, _cache);
        _builder = new StepBuilder(_frame, Pipelines, _pool, _placed, _residents, _keptMeshes, _grids,
            new CopySteps(_frame, Pipelines, _copiesPipelines, _copiedMeshes));
        _views = new MaterialViews(device, _targets, tree);
        _graphs = new GraphRunner(device, _frame, _targets, Pipelines, _materials, _globals, _views);
        _exits = new ExitDraws(device, _targets, Pipelines, Textures, Particles, _materials, _globals, _grids, _vertices);
        _copies = new CopyDraws(device, _frame, _targets, Pipelines, _copiesPipelines, _globals, _exits);
        _canvas = new CanvasStack(_graphs, Pipelines);
        _draw = new DrawPass(_frame, _targets, Pipelines, _canvas, _pool, Textures, Particles, _graphs, _exits, _copies);
    }

    /// <summary>Textures the game can draw by id, minted from the tree so they never clash with items.</summary>
    public Dictionary<ulong, Texture> Textures { get; } = [];

    public CanvasPipelines Pipelines { get; }

    /// <summary>
    /// Why the material's graph can't run with its values this frame (a loop that won't end), or can't share one of its
    /// <c>[Shared]</c> images; null when it runs as written.
    /// </summary>
    public string? GraphProblem(CanvasMaterial material) =>
        _materials.Find(material) is { IsNull: false } entity && entity.TryGetComponent(out LoweredGraph lowered)
            ? lowered.Plan.Problem ?? _graphs.SharedProblem(lowered.Effect)
            : null;

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

    /// <summary>
    /// Adds <paramref name="material"/> to the end of the post list (Docs/render-graph.md, stage F): drawn over the whole
    /// frame after the tree, after every material listed before it, as a full-screen item (its <c>screen</c> is the frame
    /// so far). Listing one twice does nothing.
    /// </summary>
    public void AddPost(CanvasMaterial material)
    {
        if (_post.Exists(post => post.Material == material))
            return;
        Handle handle = _tree.CreateItem();
        _tree.AddRect(handle, new Rect2(0, 0, _targets.Width, _targets.Height), Colors.White);
        _tree.SetMaterial(handle, material);
        _post.Add((material, _tree.Item(handle)!));
    }

    public void RemovePost(CanvasMaterial material)
    {
        int at = _post.FindIndex(post => post.Material == material);
        if (at < 0)
            return;
        _tree.Free(CanvasTree.ToHandle(_post[at].Item.Id));
        _post.RemoveAt(at);
    }

    /// <summary>Draws the whole frame into <paramref name="target"/>, cleared to <paramref name="clear"/>; <paramref name="time"/> is an effect's TIME.</summary>
    public void Render(CommandBuffer commands, Texture target, MwColor clear, float time, HostFramePhases? phases = null)
    {
        long lap = HostFramePhases.Now;
        _residents.BeginFrame();
        _frame.Jobs.Clear();
        foreach ((CanvasItem root, Texture offscreen) in _offscreen)
            _frame.Jobs.Add(_collect.Collect(root, offscreen, new MwColor(0, 0, 0, 0), phases));
        _frame.Jobs.Add(_collect.Collect(_tree.Root, target, clear, phases));
        foreach ((CanvasMaterial _, CanvasItem item) in _post)
            _collect.Last(_frame.Jobs[^1], item);
        int views = _views.Collect(_frame, _collect, phases);
        phases?.Lap(HostFramePhases.Phase.Collect, ref lap);

        _frame.Number++;
        _builder.Build();
        int residentInstances = _residents.Instances;
        _pool.Send(_canvas.Material.Lists);
        _placed.Send(_canvas.Material, residentInstances);
        _residents.Send(_canvas.Material, _placed.Instances.Count);
        phases?.Lap(HostFramePhases.Phase.Build, ref lap);
        using (commands.Scope("upload primitives"))
        {
            Upload(commands);
            _keptMeshes.Upload(commands, _frame.Number);
            _copiedMeshes.Upload(commands, _frame.Number);
        }

        // The frame's clocks, for the canvas stack's passes as for every graph's.
        _frame.Dt = float.IsNaN(_lastTime) ? 0f : Math.Clamp(time - _lastTime, 0f, 0.1f);
        _frame.Time = time;
        _lastTime = time;
        _graphs.Begin();
        using (commands.Scope("cut grids"))
            _grids.Cut(commands);
        using (commands.Scope("upload globals"))
            _globals.Upload(commands);
        using (commands.Scope("canvas stack"))
            _canvas.Run(commands);
        phases?.Lap(HostFramePhases.Phase.Upload, ref lap);

        _draw.BatchCount = 0;
        _draw.CopyCount = 0;
        // The views draw first, their own preludes before them, so every other graph's nodes may read them.
        int viewSteps = views == 0 ? 0 : _frame.Jobs[views - 1].FirstStep + _frame.Jobs[views - 1].StepCount;
        using (commands.Scope("shader graphs"))
            _graphs.Prelude(commands, 0, viewSteps);
        using (commands.Scope("particles step"))
            Particles.Step(commands, _frame.Dt);
        for (int j = 0; j < views; j++)
            using (commands.Scope("view target"))
                _draw.Draw(commands, _frame.Jobs[j], time, msaa: false);
        using (commands.Scope("shader graphs"))
            _graphs.Prelude(commands, viewSteps, _frame.Steps.Count);
        phases?.Lap(HostFramePhases.Phase.ShaderStates, ref lap);

        for (int j = views; j < _frame.Jobs.Count; j++)
        {
            Job job = _frame.Jobs[j];
            using (commands.Scope(job.Target == target ? "main target" : "offscreen target"))
                _draw.Draw(commands, job, time, job.Target == target && _targets.Msaa is not null);
        }

        using (commands.Scope("graph reports"))
            _graphs.Report(commands);

        phases?.Lap(HostFramePhases.Phase.DrawPasses, ref lap);
    }

    /// <summary>Sends what changed outside the canvas stack: effect vertices and copies.</summary>
    private void Upload(CommandBuffer commands)
    {
        CopyPass copy = commands.BeginCopyPass();
        _vertices.Upload(copy, CollectionsMarshal.AsSpan(_frame.Vertices));
        _copies.Upload(copy);
        commands.EndCopyPass(copy);
    }

    public void Dispose()
    {
        _keptMeshes.Dispose();
        _copiedMeshes.Dispose();
        _copies.Dispose();
        _copiesPipelines.Dispose();
        Pipelines.Dispose();
        Particles.Dispose();
        _targets.Dispose();
        _materials.Dispose();
        _graphs.Dispose();
        _views.Dispose();
        _vertices.Dispose();
        _grids.Dispose();
        _globals.Dispose();
    }
}
