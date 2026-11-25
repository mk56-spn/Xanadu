using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>What a node of a lowered graph does.</summary>
public enum NodeKind
{
    /// <summary>The UPDATE brick: kept state, once.</summary>
    Update,

    /// <summary>The STEP brick: the pool's items, stepped once.</summary>
    Step,

    /// <summary>A <c>[Pass]</c> call: compute over elements or groups.</summary>
    Compute,

    /// <summary>A <c>[Pixels]</c> call: compute over the item's box.</summary>
    Pixels,

    /// <summary>A <c>[Draw]</c> call into the graph's own images, or onto the frame (an exit).</summary>
    Draw,

    /// <summary>The TRACE brick: compute over the item's box into the trace's two images.</summary>
    Trace,

    /// <summary>Images drawn onto the frame over the item's box: a <c>present</c>, or what a trace made.</summary>
    Present,

    /// <summary>The stack's own triangles (DEFORM places them, SHADE colours them) drawn onto the frame.</summary>
    Shade,
}

/// <summary>
/// Where a node runs. The runner splits every graph in two around the job's render pass, since SDL_GPU can't run
/// compute or another render pass inside one.
/// </summary>
public enum NodePlace
{
    /// <summary>Before the job's render pass: everything that doesn't read <c>frame</c>, <c>screen</c> or <c>depth</c>.</summary>
    Prelude,

    /// <summary>
    /// At the item's place, between two halves of the pass: a node that reads the canvas so far but isn't an exit. It
    /// costs a pass break per item, which the editor shows.
    /// </summary>
    MidFrame,

    /// <summary>A draw into <c>frame</c>, inside the pass in tree order, blended as the item is placed.</summary>
    Exit,
}

/// <summary>
/// How often a node runs. Its scope comes from what it binds: a node that reads no item data runs once a frame for its
/// material, however many items show it; one that reads the item (its quad, its box, its values) runs for each.
/// </summary>
public enum NodeScope
{
    /// <summary>Once a frame for the material, on its buffers: UPDATE, STEP, a <c>[Pass]</c>.</summary>
    Material,

    /// <summary>Once for each item showing the material, on that item's images: pixels, draws, a trace, the exits.</summary>
    Item,
}

/// <summary>An image of a lowered graph (<see cref="GraphPlan.Images"/>).</summary>
/// <param name="Source">For a history, the place of the image it is last frame's copy of; -1 otherwise.</param>
/// <param name="Material">Whether the material holds it for all its items, since a <c>[Pass]</c> touches it.</param>
public readonly record struct PlannedImage(string Name, string Format, float Clear, ImageLife Life, int Source, ImageSize Size, bool Material);

/// <summary>One node of a lowered graph: what it does, where it runs, and for a schedule's node the call it came from.</summary>
public readonly record struct GraphNode(NodeKind Kind, NodePlace Place, StackBuild.Run Call = default)
{
    /// <summary>How often it runs, from what its kind binds.</summary>
    public NodeScope Scope => Kind is NodeKind.Update or NodeKind.Step or NodeKind.Compute ? NodeScope.Material : NodeScope.Item;
}

/// <summary>
/// A compiled stack lowered to one graph (Docs/render-graph.md): every brick becomes a node, in the order they run, each
/// placed by what it reads. A shade-only stack is one <see cref="NodeKind.Shade"/> exit; a schedule's calls are walked
/// with the material's values, so a pass a value switches off isn't there. The renderer runs this list and the editor's
/// board (<see cref="StackGraph"/>) draws it: what is shown is what runs.
/// </summary>
public sealed class GraphPlan
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<StackBuild.Run> _runs = [];

    public IReadOnlyList<GraphNode> Nodes => _nodes;

    /// <summary>Whether a node runs mid-frame, so the item breaks the pass.</summary>
    public bool BreaksPass { get; private set; }

    /// <summary>
    /// Whether the last lowering read any of the stack's values. A schedule that read none comes out the same every
    /// frame, so it is lowered once (Docs/render-graph.md, stage D: static graphs flattened).
    /// </summary>
    public bool ReadsValues { get; private set; }

    /// <summary>Why the schedule can't run with these values (a loop that won't end); null when it can.</summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// The graph's images, by the places its nodes name: the schedule's <c>[Image]</c>s, then a trace's colour and depth
    /// (cleared to nothing there, the box's size). Each item showing the graph gets its own, but for the material's.
    /// </summary>
    public static IEnumerable<PlannedImage> Images(CompiledShader fragment)
    {
        if (fragment.Build is { } build)
            for (int k = 0; k < build.Shape.Images.Count; k++)
            {
                BuildImage image = build.Shape.Images[k];
                yield return new PlannedImage(image.Name, image.Format, image.Clear, image.Life, image.Source, image.Size, build.MaterialImages[k]);
            }

        if (fragment.Trace is not null)
        {
            yield return new PlannedImage("traced", "rgba16f", 0f, ImageLife.Frame, -1, default, false);
            yield return new PlannedImage("traced depth", "r32f", 0f, ImageLife.Frame, -1, default, false);
        }
    }

    /// <summary>The place of a trace's colour among <see cref="Images"/>; its depth is the next.</summary>
    public static int TracedImage(CompiledShader fragment) => fragment.Build?.Shape.Images.Count ?? 0;

    /// <summary>The stack lowered with its values as <paramref name="uniform"/> reads them; every value at its default without one.</summary>
    public static GraphPlan Of(CompiledShader fragment, Func<string, BuildValue?>? uniform = null)
    {
        var plan = new GraphPlan();
        plan.Lower(fragment, uniform);
        return plan;
    }

    /// <summary>Lowers the stack again into this plan, reusing its lists: what the renderer does each frame for a schedule.</summary>
    public void Lower(CompiledShader fragment, Func<string, BuildValue?>? uniform = null)
    {
        _nodes.Clear();
        BreaksPass = false;
        ReadsValues = false;
        Problem = null;
        if (fragment.Update is not null)
            Add(NodeKind.Update, NodePlace.Prelude);
        if (fragment.Pool is not null)
            Add(NodeKind.Step, NodePlace.Prelude);

        bool alone = false;
        if (fragment.Build is { } build)
        {
            _runs.Clear();
            try
            {
                Func<string, BuildValue?> values = uniform ?? (name => fragment.ScheduleValue(name, null));
                build.Shape.Walk(name =>
                {
                    BuildValue? value = values(name);
                    ReadsValues |= value is not null;
                    return value;
                }, _runs);
            }
            catch (BuildProblem problem)
            {
                Problem = problem.Message;
                _runs.Clear();
            }

            foreach (StackBuild.Run run in _runs)
            {
                if (run.Presents)
                {
                    Add(NodeKind.Present, NodePlace.Exit, run);
                    continue;
                }

                CompiledPass pass = build.Passes[run.Pass];
                if (pass.Drawn is not null)
                    Add(NodeKind.Draw, run.OntoFrame ? NodePlace.Exit : NodePlace.Prelude, run);
                else if (pass.Tile is not null)
                    Add(NodeKind.Pixels, NodePlace.Prelude, run);
                else if (run.Groups > 0)
                    Add(NodeKind.Compute, NodePlace.Prelude, run);
            }

            alone = build.Alone;
        }

        if (fragment.Trace is { } trace)
        {
            // A trace that reads the canvas depth must wait for the canvas drawn so far; one that doesn't runs before.
            Add(NodeKind.Trace, trace.Shader.ReadsDepth ? NodePlace.MidFrame : NodePlace.Prelude);
            uint colour = (uint)TracedImage(fragment);
            Add(NodeKind.Present, NodePlace.Exit, new StackBuild.Run(StackBuild.Run.Present, 0, 0, StackBuild.RunValues.Of([colour, colour + 1], StackBuild.Run.None)));
            alone |= trace.Alone;
        }

        if (!alone)
            Add(NodeKind.Shade, NodePlace.Exit);
    }

    private void Add(NodeKind kind, NodePlace place, StackBuild.Run call = default)
    {
        _nodes.Add(new GraphNode(kind, place, call));
        BreaksPass |= place == NodePlace.MidFrame;
    }
}
