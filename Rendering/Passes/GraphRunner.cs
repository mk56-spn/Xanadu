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
    private readonly ComputeNodes _compute = new(device, targets, materials, globals);
    private readonly HashSet<CanvasEffect> _brokenSchedules = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<CanvasEffect> _uncut = new(ReferenceEqualityComparer.Instance);
    private readonly ImageDraws _draws = new(device, targets, globals);

    /// <summary>The draw nodes: into the item's images here, onto the frame from the pass (<see cref="ImageDraws.Exit"/>).</summary>
    public ImageDraws Draws => _draws;

    /// <summary>Each step's images this frame, by its place in the frame's steps; null until its first item node.</summary>
    private readonly List<ItemSet?> _sets = [];

    /// <summary>Each material's own images this frame, for the materials whose graph has some; taken at their first use.</summary>
    private readonly Dictionary<CanvasMaterial, Texture?[]> _shared = new(ReferenceEqualityComparer.Instance);

    /// <summary>Steps whose item has no box to work in (off screen, or its quad cut on the GPU).</summary>
    private readonly HashSet<int> _boxless = [];

    public IndirectDraws Indirect { get; } = new(device, frame, pipelines, materials);

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
    /// for every step; then the indirect args are filled.
    /// </summary>
    public void Prelude(CommandBuffer commands)
    {
        _images.Begin(frame.Number);
        _sets.Clear();
        _shared.Clear();
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
            // A particle mesh borrows the effect's shade only: its graph runs for the material's own items.
            bool item = step.Kind == StepKind.Effect;
            foreach (GraphNode node in plan.Nodes)
            {
                if (node.Place == NodePlace.Prelude && (node.Scope == NodeScope.Material ? first : item))
                    Run(commands, node, effect, step, s, ref kept);
            }
        }

        using (commands.Scope("shader draws"))
            Indirect.Fill(commands);
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
        switch (node.Kind)
        {
            case NodeKind.Update:
                _compute.Update(commands, material, effect, frame.Dt, frame.Time);
                return;
            case NodeKind.Step:
                _compute.Step(commands, material, effect, frame.Dt, frame.Time);
                return;
        }

        kept ??= _resources.For(commands, material, effect);
        if (node.Kind == NodeKind.Compute)
        {
            _compute.Pass(commands, effect, kept.Value, Shared(commands, material, effect), node.Call, material, frame.Dt, frame.Time);
            return;
        }

        if (Set(commands, effect, step, s) is not { } set)
            return;
        RestFrame rest = step.Mesh!.Vertices[(int)step.Start].Rest;
        switch (node.Kind)
        {
            case NodeKind.Pixels:
                _compute.Pixels(commands, effect, kept.Value, set, rest, node.Call, step, frame.Dt, frame.Time);
                break;
            case NodeKind.Trace:
                _compute.Trace(commands, effect, kept.Value, set, rest, step, frame.Dt, frame.Time);
                break;
            case NodeKind.Draw:
                _draws.Draw(commands, effect, set, rest, node.Call, step, frame.Time);
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
        if (step.Mesh is not { } mesh || step.Start >= mesh.Vertices.Count)
        {
            // A [Grid]'s cells are cut on the GPU: the CPU has no corner to hand the node.
            if (_uncut.Add(effect))
                Console.Error.WriteLine($"Shader stack {step.Material?.Shader}: its graph needs the item's quad uncut; drop the deform's [Grid].");
            _boxless.Add(s);
            return null;
        }

        if (ExitDraws.BoxOf(mesh.Vertices[(int)step.Start].Rest, step.Clip, targets.Width, targets.Height) is not { } box)
        {
            _boxless.Add(s);
            return null;
        }

        return _sets[s] = _images.Take(commands, effect, step.Kept, step.Start, box, Shared(commands, step.Material!, effect));
    }

    /// <summary>The material's own images this frame (<see cref="ItemImages.Shared"/>); null when its graph has none.</summary>
    private Texture?[]? Shared(CommandBuffer commands, CanvasMaterial material, CanvasEffect effect)
    {
        if (!effect.SharesImages)
            return null;
        if (!_shared.TryGetValue(material, out Texture?[]? shared))
            _shared[material] = shared = _images.Shared(commands, effect, materials.For(material));
        return shared;
    }

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
        lowered.Plan.Lower(compiled, name => compiled.ScheduleValue(name, material.Values.TryGetValue(name, out Vector4 set) ? set : null));
        // Its values made the schedule impossible (a loop that won't end): said once, nothing of it run.
        if (lowered.Plan.Problem is { } problem && _brokenSchedules.Add(effect))
            Console.Error.WriteLine($"Shader stack {material.Shader}: graph() can't run: {problem}");
        return lowered.Plan;
    }

    public void Dispose()
    {
        _resources.Dispose();
        _images.Dispose();
        Indirect.Dispose();
    }
}
