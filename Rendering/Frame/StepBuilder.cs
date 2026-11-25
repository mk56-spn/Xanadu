using System.Collections.Generic;
using System.Numerics;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Rendering.Passes;
using Xanadu.Ui.Systems.Canvas;
using Entity = Friflo.Engine.ECS.Entity;

namespace Xanadu.Rendering.Frame;

/// <summary>
/// Building: turns the frame's jobs into steps in draw order. Plain primitives become instances
/// in runs that share up to <see cref="PrimitivePipelines.Slots"/> textures; an effect range is
/// kept (<see cref="KeptMeshes"/>), cut on the GPU (<see cref="GridCutter"/>) or cut here; a
/// resident tree's segments join as their runs; each takes its depth from its place.
/// </summary>
internal sealed class StepBuilder(CanvasFrame frame, CanvasPipelines pipelines, PrimitivePool pool, PlacedPrimitives placed,
    ResidentTrees residents, KeptMeshes keptMeshes, GridCutter grids)
{
    private const int Slots = PrimitivePipelines.Slots;
    private const float DepthStep = PlacedPrimitives.DepthStep;

    /// <summary>
    /// Depth steps a deforming effect takes, so the depths its <c>deform</c> sets spread over its own
    /// slice (about 65 thousand levels in a 24-bit buffer) and never reach the next draw.
    /// </summary>
    private const uint DeformSlice = 1 << 14;

    private readonly CanvasFrame _frame = frame;
    private readonly CanvasPipelines _pipelines = pipelines;
    private readonly PrimitivePool _pool = pool;
    private readonly PlacedPrimitives _placed = placed;
    private readonly ResidentTrees _residents = residents;
    private readonly KeptMeshes _keptMeshes = keptMeshes;
    private readonly GridCutter _grids = grids;
    private readonly List<Job> _jobs = frame.Jobs;
    private readonly List<Step> _steps = frame.Steps;
    private readonly List<ulong> _stepTextures = frame.StepTextures;
    private readonly List<CanvasVertex> _vertices = frame.Vertices;

    public void Build()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            _steps.Clear();
            _stepTextures.Clear();
            _vertices.Clear();
            _pool.Clear();
            _placed.Clear();
            _grids.Clear();
            _residents.Prepare(_pool);
            for (int j = 0; j < _jobs.Count; j++)
                BuildJob(_jobs[j], j);

            if (!_pool.Overflowed)
                return;

            // The pool is full of old ranges: start a bigger, empty one and send everything drawn again.
            _pool.Grow();
        }
    }

    private void BuildJob(Job job, int index)
    {
        job.FirstStep = _steps.Count;
        job.OpaqueStart = (uint)_placed.OpaqueReserved;
        int jobFirst = _placed.PlacedCount;
        uint sequence = 0;
        int runStart = -1, runTextures = 0;

        void CloseRun()
        {
            if (runStart >= 0 && _placed.PlacedCount > runStart)
                _steps.Add(new Step(StepKind.Primitives, (uint)runStart, (uint)(_placed.PlacedCount - runStart), _stepTextures.Count - Slots));
            runStart = -1;
        }

        void OpenRun()
        {
            runStart = _placed.PlacedCount;
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
            int slotBase = _placed.Slots.Count;
            bool fits = true;
            foreach (ulong texture in item.LocalTextures)
            {
                int slot = SlotOf(texture);
                if (slot < 0)
                {
                    fits = false;
                    break;
                }

                _placed.Slots.Add((uint)slot);
            }

            if (!fits)
            {
                _placed.Slots.RemoveRange(slotBase, _placed.Slots.Count - slotBase);
                CloseRun();
                OpenRun();
                foreach (ulong texture in item.LocalTextures)
                    _placed.Slots.Add((uint)Math.Max(0, SlotOf(texture)));
            }

            Transform2D t = entry.Transform;
            int count = to - from;
            _placed.Instances.Add(new GpuInstance
            {
                Basis = new Vector4(t.X.X, t.X.Y, t.Y.X, t.Y.Y),
                Origin = new Vector4(t.Origin.X, t.Origin.Y, MathF.Sqrt(MathF.Abs(t.X.X * t.Y.Y - t.X.Y * t.Y.X)), 0),
                Tint = new Vector4(entry.Color.R, entry.Color.G, entry.Color.B, entry.Color.A),
                Clip = new Vector4(entry.Clip.X, entry.Clip.Y, entry.Clip.X + entry.Clip.W, entry.Clip.Y + entry.Clip.H),
                PoolStart = (uint)(item.PoolStart + from),
                Count = (uint)count,
                OutStart = (uint)_placed.PlacedCount,
                SlotBase = (uint)slotBase,
                Flags = PlacedPrimitives.AllowOpaque,
                OpaqueBase = job.OpaqueStart + (uint)(_placed.PlacedCount - jobFirst),
                DepthBase = sequence,
            });
            _placed.PlacedCount += count;
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
                _pool.Send(item);
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
        _placed.OpaqueReserved += _placed.PlacedCount - jobFirst;
        job.OpaqueReserved = (uint)(_placed.PlacedCount - jobFirst);
    }

    /// <summary>An item with a material: its primitives as canvas vertices for the effect's pipeline, one draw per texture.</summary>
    /// <summary>
    /// Whether an effect sets its own depths, moving its own vertices or tracing its pixels: it then
    /// takes a slice of the frame's depth to spread them over.
    /// </summary>
    private bool OwnsDepth(CanvasMaterial? material) =>
        _pipelines.Effect(material) is { } effect
        && (effect.Vertex is not null || effect.Trace is not null || effect.Compiled.Build?.Shape.Presents == true);

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
        if (particles is null && OwnsDepth(material))
        {
            taken = Math.Max(taken, DeformSlice);
            span = (taken - 1) * DepthStep;
        }

        // A [Grid]'s boxes are cut on the GPU, so a grid that changes every frame costs the CPU nothing.
        if (particles is null && columns * rows > 1 &&
            _grids.Add(item, entry, from, to, columns, rows, kind, material, values, depth, span))
            return taken;

        // Every other effect range is kept: most shaded items (thumbnails, panels, glass) look the same
        // frame after frame, and cutting them again each frame was the bulk of a frame's CPU in the shader tool.
        if (particles is null)
        {
            Entity kept = _keptMeshes.Cut(item, entry.Transform, entry.Color, from, to, columns, rows, _frame.Number);
            KeptVertices mesh = kept.GetComponent<KeptMesh>().Mesh;
            foreach ((uint pieceStart, uint pieceCount, ulong pieceTexture) in mesh.Pieces)
            {
                _steps.Add(new Step(kind, pieceStart, pieceCount,
                    Clip: entry.Clip, Material: material, Values: values, Texture: pieceTexture, Depth: depth, Span: span,
                    Transform: entry.Transform, Mesh: mesh, Kept: kept));
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

    /// <summary>A segment's draws, at this point in the job: its runs as steps, its bases noted for the resolve pass.</summary>
    private uint AddPiece(Job job, ResidentPiece piece, int jobFirst, uint sequence)
    {
        piece.OutBase = (uint)_placed.PlacedCount;
        piece.DepthBase = sequence;
        piece.OpaqueBase = job.OpaqueStart + (uint)(_placed.PlacedCount - jobFirst);
        List<ulong> textures = piece.Tree.RunTextures;
        foreach ((uint start, uint count, int firstTexture) in piece.Runs)
        {
            int first = _stepTextures.Count;
            for (int i = 0; i < Slots; i++)
                _stepTextures.Add(textures[firstTexture + i]);
            _steps.Add(new Step(StepKind.Primitives, piece.OutBase + start, count, first));
        }

        _placed.PlacedCount += (int)piece.Primitives;
        return piece.Primitives;
    }
}
