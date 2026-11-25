using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Rendering.Frame;
using Xanadu.Rendering.Gpu;
using Xanadu.Rendering.Kept;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;
using Entity = Friflo.Engine.ECS.Entity;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// The one runner for every stack's graph (Docs/render-graph.md). Each stack is lowered to a <see cref="GraphPlan"/>;
/// its prelude runs before any job's render pass, in the plan's order: a material's nodes once a frame on its buffers,
/// an item's nodes for each item showing it, on that item's own images (<see cref="ItemImages"/>). A mid-frame node runs
/// at its item's place between two halves of the pass; the exits draw inside the pass (<see cref="ExitDraws"/>). A
/// plain stack's plan is fixed, one shade exit, so it costs nothing here; a schedule is lowered again each frame with
/// its material's values.
/// </summary>
internal sealed class GraphRunner(GraphicsDevice device, CanvasFrame frame, FrameTargets targets, CanvasPipelines pipelines,
    MaterialStore materials, GlobalBindings globals) : IDisposable
{
    private readonly GraphResources _resources = new(device, materials);
    private readonly ItemImages _images = new(device, targets);
    private readonly ComputeNodes _compute = new(device, targets, globals);
    private readonly ImageDraws _draws = new(device, targets, globals, materials);

    /// <summary>The draw nodes: into the item's images here, onto the frame from the pass (<see cref="ImageDraws.Exit"/>).</summary>
    public ImageDraws Draws => _draws;

    /// <summary>Each step's images this frame, by its place in the frame's steps; null until its first item node.</summary>
    private readonly List<ItemSet?> _sets = [];

    /// <summary>
    /// Each material's own images this frame, for the materials whose graph has some, and whether their caches need
    /// filling; taken at their first use.
    /// </summary>
    private readonly Dictionary<CanvasMaterial, (Texture?[] Images, bool Stale)> _shared = new(ReferenceEqualityComparer.Instance);

    /// <summary>The materials whose <c>[Cached]</c> buffers need filling this frame.</summary>
    private readonly HashSet<CanvasMaterial> _staleBuffers = new(ReferenceEqualityComparer.Instance);

    /// <summary>Steps whose item has no box to work in (off screen, or its quad cut on the GPU).</summary>
    private readonly HashSet<int> _boxless = [];


    /// <summary>The plan a material's stack runs this frame: its effect's fixed one, or its schedule lowered with its values.</summary>
    public GraphPlan Plan(CanvasMaterial? material, CanvasEffect effect)
    {
        if (effect.Compiled.Build is null || material is null)
            return effect.Plan;
        Entity entity = materials.Find(material);
        return !entity.IsNull && entity.TryGetComponent(out LoweredGraph lowered) ? lowered.Plan : effect.Plan;
    }

    /// <summary>Step <paramref name="s"/>'s images this frame; null when its graph made none.</summary>
    public ItemSet? Images(int s) => s < _sets.Count ? _sets[s] : null;

    /// <summary>
    /// Every graph's prelude, before any pass draws: a material's nodes the first time a step shows it, an item's nodes
    /// for every step.
    /// </summary>
    public void Prelude(CommandBuffer commands)
    {
        _images.Begin(frame.Number);
        _sets.Clear();
        _shared.Clear();
        _staleBuffers.Clear();
        CollectionsMarshal.SetCount(_sets, frame.Steps.Count);
        _boxless.Clear();
        for (int s = 0; s < frame.Steps.Count; s++)
        {
            Step step = frame.Steps[s];
            if (step.Material is not { } material)
                continue;
            CanvasEffect effect = pipelines.Effect(material);
            bool first = materials.FirstRun(material, frame.Number);
            GraphPlan plan = first ? Lower(material, effect) : Plan(material, effect);
            // A schedule's buffers exist from its first frame, whether or not its values call anything yet.
            BuildBuffers? kept = effect.Compiled.Build is null || !first ? null : _resources.For(commands, material, effect);
            if (kept is not null && effect.Compiled.Build!.Shape.CachesBuffers && _resources.Stale(material, Stamp(material)))
                _staleBuffers.Add(material);
            // A particle mesh borrows the effect's shade only: its graph runs for the material's own items.
            bool item = step.Kind == StepKind.Effect;
            foreach (GraphNode node in plan.Nodes)
            {
                // A draw counted on the GPU takes its count once a frame, wherever it draws, after the passes before it.
                if (first && kept is { } counting && node.Kind == NodeKind.Draw && effect.Compiled.Build!.Passes[node.Call.Pass].Drawn!.CopiesBuffer >= 0)
                    GraphResources.Count(commands, counting, effect.Compiled.Build, node.Call.Pass);
                if (node.Place == NodePlace.Prelude && (node.Scope == NodeScope.Material ? first : item))
                    Run(commands, node, effect, step, s, ref kept);
            }
        }
    }

    /// <summary>Step <paramref name="s"/>'s mid-frame nodes, run between two halves of its job's pass.</summary>
    public void MidFrame(CommandBuffer commands, GraphPlan plan, CanvasEffect effect, Step step, int s)
    {
        BuildBuffers? kept = null;
        foreach (GraphNode node in plan.Nodes)
        {
            if (node.Place == NodePlace.MidFrame)
                Run(commands, node, effect, step, s, ref kept);
        }
    }

    private void Run(CommandBuffer commands, GraphNode node, CanvasEffect effect, Step step, int s, ref BuildBuffers? kept)
    {
        CanvasMaterial material = step.Material!;
        kept ??= _resources.For(commands, material, effect);
        if (node.Kind == NodeKind.Compute)
        {
            // A cache a [Pass] fills is the material's: filled when its values move.
            if (node.Fills && !_staleBuffers.Contains(material) && !(effect.SharesImages && SharedStale(commands, material, effect)))
                return;
            _compute.Pass(commands, effect, kept.Value, Shared(commands, material, effect), node.Call, material, frame.Dt, frame.Time);
            return;
        }

        if (Set(commands, effect, step, s) is not { } set || (node.Fills && !set.Stale))
            return;
        RestFrame rest = step.Mesh!.Vertices[(int)step.Start].Rest;
        switch (node.Kind)
        {
            case NodeKind.Pixels:
                _compute.Pixels(commands, effect, kept.Value, set, rest, node.Call, step, frame.Dt, frame.Time);
                break;
            case NodeKind.Draw:
                _draws.Draw(commands, effect, kept.Value, set, rest, node.Call, step, frame.Time);
                break;
        }
    }

    /// <summary>Step <paramref name="s"/>'s images, taken at its first item node; null when its item has no box to work in.</summary>
    private ItemSet? Set(CommandBuffer commands, CanvasEffect effect, Step step, int s)
    {
        if (_sets[s] is { } set)
            return set;
        if (_boxless.Contains(s))
            return null;
        // No corner on the CPU (a [Grid] cut on the GPU, which the compile refuses in a graph) or no box on screen: nothing to work in.
        if (step.Mesh is not { } mesh || step.Start >= mesh.Vertices.Count
            || ExitDraws.BoxOf(mesh.Vertices[(int)step.Start].Rest, step.Clip, targets.Width, targets.Height) is not { } box)
        {
            _boxless.Add(s);
            return null;
        }

        return _sets[s] = _images.Take(commands, effect, step.Kept, step.Start, box, Shared(commands, step.Material!, effect), Stamp(step.Material!));
    }

    /// <summary>The material's own images this frame (<see cref="ItemImages.Shared"/>); null when its graph has none.</summary>
    private Texture?[]? Shared(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect) =>
        effect.SharesImages ? Taken(commands, material, effect).Images : null;

    /// <summary>Whether the material's own cached images need filling this frame.</summary>
    private bool SharedStale(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect) => Taken(commands, material, effect).Stale;

    private (Texture?[] Images, bool Stale) Taken(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect)
    {
        if (!_shared.TryGetValue(material, out (Texture?[] Images, bool Stale) shared))
        {
            Texture?[] images = _images.Shared(commands, effect, materials.For(material), Stamp(material), out bool stale);
            _shared[material] = shared = (images, stale);
        }

        return shared;
    }

    /// <summary>What a cache is filled against: the material's values and host buffers, as their versions.</summary>
    private static long Stamp(CanvasMaterial material) => ((long)material.Version << 32) | (uint)material.BufferVersion;

    /// <summary>The material's plan for this frame: a schedule lowered with its values, kept on its entity.</summary>
    private GraphPlan Lower(CanvasMaterial material, CanvasEffect effect)
    {
        if (effect.Compiled.Build is null)
            return effect.Plan;
        Entity entity = materials.For(material);
        bool had = entity.TryGetComponent(out LoweredGraph lowered) && lowered.Effect == effect;
        if (had && !lowered.Plan.ReadsValues && lowered.Plan.Problem is null)
            return lowered.Plan;
        if (!had)
            entity.AddComponent(lowered = new LoweredGraph(effect, new GraphPlan()));
        CompiledShader compiled = effect.Compiled;
        // Values that make the schedule impossible (a loop that won't end) leave it empty, its Problem saying why
        // (CanvasRenderer.GraphProblem): nothing of it runs.
        lowered.Plan.Lower(compiled, name => compiled.ScheduleValue(name, material.Values.TryGetValue(name, out Vector4 set) ? set : null));
        return lowered.Plan;
    }

    public void Dispose()
    {
        _resources.Dispose();
        _images.Dispose();
    }
}
