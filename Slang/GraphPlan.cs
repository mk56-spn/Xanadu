using System.Collections.Generic;

namespace Xanadu.Slang;

/// <summary>What a node of a lowered graph does.</summary>
public enum NodeKind
{
    /// <summary>A <c>[Pass]</c> call: compute over elements or groups.</summary>
    Compute,

    /// <summary>A <c>[Pixels]</c> call: compute over the item's box.</summary>
    Pixels,

    /// <summary>A <c>[Draw]</c> call into the graph's own images, or onto the frame (an exit).</summary>
    Draw,

    /// <summary>The item's own quad (its deform places the corners, its shade colours them) drawn onto the frame.</summary>
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
    /// <summary>Once a frame for the material, on its buffers: a <c>[Pass]</c>.</summary>
    Material,

    /// <summary>Once for each item showing the material, on that item's images: pixels, draws, the exits.</summary>
    Item,
}

/// <summary>An image of a lowered graph (<see cref="GraphPlan.Images"/>).</summary>
/// <param name="Source">For a history, the place of the image it is last frame's copy of; -1 otherwise.</param>
/// <param name="Material">Whether the material holds it for all its items, since a <c>[Pass]</c> touches it.</param>
public readonly record struct PlannedImage(string Name, string Format, float Clear, ImageLife Life, int Source, ImageSize Size, bool Material);

/// <summary>One node of a lowered graph: what it does, where it runs, and for a schedule's node the call it came from.</summary>
/// <param name="Fills">
/// It fills a cache: every image it touches is <c>[Cached]</c> and it writes no buffer, so it runs only when those go
/// stale (<see cref="ImageLife.Cached"/>).
/// </param>
public readonly record struct GraphNode(NodeKind Kind, NodePlace Place, StackBuild.Run Call = default, bool Fills = false)
{
    /// <summary>How often it runs, from what its kind binds.</summary>
    public NodeScope Scope => Kind == NodeKind.Compute ? NodeScope.Material : NodeScope.Item;
}

/// <summary>
/// A compiled stack lowered to one graph (Docs/render-graph.md): every call of its schedule becomes a node, in order, each
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

    /// <summary>Whether a mid-frame node reads the screen copy: the break copies the canvas drawn so far first.</summary>
    public bool ReadsScreen { get; private set; }

    /// <summary>Why the schedule can't run with these values (a loop that won't end); null when it can.</summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// The graph's images, by the places its nodes name: the schedule's <c>Image</c>s. Each item showing the graph gets
    /// its own, but for the material's.
    /// </summary>
    public static IEnumerable<PlannedImage> Images(CompiledShader fragment)
    {
        if (fragment.Build is { } build)
            for (int k = 0; k < build.Shape.Images.Count; k++)
            {
                BuildImage image = build.Shape.Images[k];
                yield return new PlannedImage(image.Name, image.Format, image.Clear, image.Life, image.Source, image.Size, build.MaterialImages[k]);
            }
    }

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
        ReadsScreen = false;
        ReadsValues = false;
        Problem = null;

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
                // A pixel pass reading the canvas so far waits for it, mid-frame, and so does every item node after it.
                CompiledPass pass = build.Passes[run.Pass];
                ReadsScreen |= pass.ReadsScreen;
                NodePlace item = BreaksPass || pass.ReadsDepth || pass.ReadsScreen ? NodePlace.MidFrame : NodePlace.Prelude;
                bool fills = item == NodePlace.Prelude && Fills(build, pass, run);
                if (pass.Drawn is not null)
                    Add(NodeKind.Draw, run.OntoFrame ? NodePlace.Exit : item, run, fills && !run.OntoFrame);
                else if (pass.Tile is not null)
                    Add(NodeKind.Pixels, item, run, fills);
                else if (run.Groups > 0)
                    Add(NodeKind.Compute, NodePlace.Prelude, run, fills);
            }

            alone = build.Alone;
        }

        if (!alone)
            Add(NodeKind.Shade, NodePlace.Exit);
    }

    /// <summary>
    /// Whether a call only fills caches: it touches at least one <c>[Cached]</c> image or writes a <c>[Cached]</c> buffer,
    /// every image it touches is cached (a draw's targets too), every buffer it writes is cached, and every buffer it
    /// reads is cached or the host's: nothing it reads can move without the material's stamp moving too.
    /// </summary>
    private static bool Fills(CompiledBuild build, CompiledPass pass, StackBuild.Run run)
    {
        IReadOnlyList<BuildImage> images = build.Shape.Images;
        IReadOnlyList<BuildBuffer> buffers = build.Shape.Buffers;
        bool touches = false;
        foreach (int image in pass.Images)
        {
            if (images[image].Life != ImageLife.Cached)
                return false;
            touches = true;
        }

        if (pass.Drawn is { } drawn)
        {
            if (Array.Exists(drawn.Reads, image => images[image].Life != ImageLife.Cached)
                || Array.Exists(drawn.Vertex.StackBuffers, buffer => !Steady(buffers[buffer]))
                || Array.Exists(drawn.Fragment.StackBuffers, buffer => !Steady(buffers[buffer])))
                return false;
            for (int k = 0; run.Target(k) != StackBuild.Run.None; k++)
            {
                if (run.Target(k) == StackBuild.Run.Frame || images[(int)run.Target(k)].Life != ImageLife.Cached)
                    return false;
                touches = true;
            }

            return touches;
        }

        for (int k = 0; k < pass.Buffers.Length; k++)
        {
            BuildBuffer buffer = buffers[pass.Buffers[k]];
            if (pass.Access[k].HasFlag(GraphAccess.Write))
            {
                if (!buffer.Cached)
                    return false;
                touches = true;
            }
            else if (!Steady(buffer))
                return false;
        }

        return touches;
    }

    /// <summary>A buffer that only changes with the material's stamp: cached, or filled by the host.</summary>
    private static bool Steady(BuildBuffer buffer) => buffer.Cached || buffer.Host;

    private void Add(NodeKind kind, NodePlace place, StackBuild.Run call = default, bool fills = false)
    {
        _nodes.Add(new GraphNode(kind, place, call, fills));
        BreaksPass |= place == NodePlace.MidFrame;
    }
}
