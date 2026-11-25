using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>What a stack's data is: kept state, a pool's items, a BUILD buffer, or the frame it lands on.</summary>
public enum GraphResourceKind
{
    /// <summary>The UPDATE brick's one struct, read everywhere below it as <c>state</c>.</summary>
    State,

    /// <summary>The STEP brick's items, each drawn once as <c>item</c>.</summary>
    Pool,

    /// <summary>A BUILD brick's <c>[Buffer]</c> (or <c>[Draws]</c>), kept from frame to frame.</summary>
    Buffer,

    /// <summary>A graph's <c>[Image]</c>, or a trace's colour or depth: each item's own, its box's size, for the frame unless kept or a history.</summary>
    Image,

    /// <summary>The target's depth: a present writes it, triangles test and write it, a mid-frame trace reads it.</summary>
    Depth,

    /// <summary>The target's colour: what the stack draws lands here, through its blend.</summary>
    Frame,

    /// <summary>A copy of the frame drawn so far, which a shade may sample.</summary>
    Screen,
}

/// <summary>How a pass touches a resource. Compute binds its buffers read-write, so "may write" is what it says there.</summary>
[Flags]
public enum GraphAccess
{
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
}

/// <summary>One piece of data the stack's passes share: its kind, its name, and its bytes (0 when the host sizes it).</summary>
public sealed record GraphResource(GraphResourceKind Kind, string Name, long Bytes);

/// <summary>
/// One pass of the stack, in the order they first run this frame: its name, the brick it comes
/// from, whether it is compute, and for a BUILD pass how many times <c>build()</c> calls it and
/// the threads those calls run in all (0 for the brick's own passes, which run once).
/// </summary>
public sealed record GraphPass(string Name, BrickKind Brick, bool Compute, int Calls = 1, long Threads = 0)
{
    /// <summary>Where it runs around the job's render pass; mid-frame costs the item a pass break.</summary>
    public NodePlace Place { get; init; } = NodePlace.Prelude;
}

/// <summary>A pass touching a resource, by their places in <see cref="StackGraph.Passes"/> and <see cref="StackGraph.Resources"/>.</summary>
public readonly record struct GraphEdge(int Pass, int Resource, GraphAccess Access);

/// <summary>
/// A compiled stack as a render graph: the passes it runs this frame, every resource they
/// share, and who reads and writes what. The BUILD passes are the ones <c>build()</c> really
/// calls with the values as they are (one a pass, in the order first called, with its calls
/// counted), so a pass a value switches off isn't there. A compute pass's reads and writes are
/// what its compiled code does with each buffer, not the read-write binding it gets; a
/// graphics stage can only read. Kept state and pool items are as bound.
/// </summary>
public sealed class StackGraph
{
    private readonly List<GraphPass> _passes = [];
    private readonly List<GraphResource> _resources = [];
    private readonly List<GraphEdge> _edges = [];

    public IReadOnlyList<GraphPass> Passes => _passes;

    public IReadOnlyList<GraphResource> Resources => _resources;

    public IReadOnlyList<GraphEdge> Edges => _edges;

    /// <summary>The blend the shade's colour lands on the frame through; it is set where the stack is placed.</summary>
    public const string BlendNote = "blend (where placed)";

    /// <summary>Why <c>build()</c> can't run with these values (a loop that won't end); null when it can.</summary>
    public string? ScheduleProblem { get; private init; }

    /// <summary>
    /// The graph of a compiled stack (the fragment <see cref="SlangShaders.CompileEffectBody"/> returns)
    /// with its values as <paramref name="uniform"/> reads them; every value at its default without one.
    /// It is the stack's <see cref="GraphPlan"/> as the renderer runs it, a schedule's calls of one pass gathered into one.
    /// </summary>
    public static StackGraph Of(CompiledShader fragment, Func<string, BuildValue?>? uniform = null)
    {
        GraphPlan plan = GraphPlan.Of(fragment, uniform);
        var graph = new StackGraph { ScheduleProblem = plan.Problem };
        int frame = graph.Resource(GraphResourceKind.Frame, "frame", 0);
        int depth = graph.Resource(GraphResourceKind.Depth, "depth", 0);
        int state = fragment.Update is { } update ? graph.Resource(GraphResourceKind.State, "state", update.StateBytes) : -1;
        int pool = fragment.Pool is { } stepped ? graph.Resource(GraphResourceKind.Pool, "items", (long)stepped.ItemBytes * stepped.ItemCount) : -1;
        int[] buffers = fragment.Build is { } built
            ? [.. built.Shape.Buffers.Select((buffer, i) => graph.Resource(GraphResourceKind.Buffer, buffer.Name, built.Bytes[i]))]
            : [];
        int[] images = [.. GraphPlan.Images(fragment).Select(image => graph.Resource(GraphResourceKind.Image, image.Name, 0))];
        int screen = fragment.ReadsScreen ? graph.Resource(GraphResourceKind.Screen, "screen", 0) : -1;

        // Kept state is bound by every stage below the update.
        void ReadsState(int pass)
        {
            if (state >= 0)
                graph.Edge(pass, state, GraphAccess.Read);
        }

        void Binds(int pass, IEnumerable<int> places, GraphAccess access)
        {
            foreach (int place in places)
                graph.Edge(pass, buffers[place], access);
        }

        // Compute binds read-write; what it does is in its code.
        void Uses(int pass, int[] places, GraphAccess[] access)
        {
            for (int k = 0; k < places.Length; k++)
                if (access[k] != 0)
                    graph.Edge(pass, buffers[places[k]], access[k]);
        }

        // A schedule's calls of one pass are one box, with its calls counted and its threads summed.
        var called = new Dictionary<int, int>();
        foreach (GraphNode node in plan.Nodes)
        {
            switch (node.Kind)
            {
                case NodeKind.Update:
                    graph.Edge(graph.Pass("update", BrickKind.Update, true), state, GraphAccess.ReadWrite);
                    break;

                case NodeKind.Step:
                {
                    int step = graph.Pass("step", BrickKind.Step, true);
                    ReadsState(step);
                    graph.Edge(step, pool, GraphAccess.ReadWrite);
                    break;
                }

                case NodeKind.Compute or NodeKind.Pixels or NodeKind.Draw:
                {
                    CompiledPass compiled = fragment.Build!.Passes[node.Call.Pass];
                    long threads = node.Call.Over > 0 ? node.Call.Over : (long)node.Call.Groups * compiled.Threads;
                    if (called.TryGetValue(node.Call.Pass, out int seen))
                    {
                        GraphPass was = graph._passes[seen];
                        graph._passes[seen] = was with { Calls = was.Calls + 1, Threads = was.Threads + threads };
                    }
                    else
                    {
                        int pass = graph.Pass(compiled.Name, BrickKind.Build, true, 1, threads, place: node.Place);
                        called[node.Call.Pass] = seen = pass;
                        ReadsState(pass);
                        Uses(pass, compiled.Buffers, compiled.Access);
                        foreach (int image in compiled.Images)
                            graph.Edge(pass, images[image], GraphAccess.ReadWrite);
                        foreach (int image in compiled.Drawn?.Reads ?? [])
                            graph.Edge(pass, images[image], GraphAccess.Read);
                    }

                    // A draw writes the images its calls name.
                    if (node.Kind == NodeKind.Draw && node.Call.OntoFrame)
                    {
                        graph.Edge(seen, frame, GraphAccess.Write);
                        graph.Edge(seen, depth, GraphAccess.ReadWrite);
                    }
                    else
                        for (int k = 0; node.Kind == NodeKind.Draw && k < DrawOutput.Most && node.Call.Target(k) != StackBuild.Run.None; k++)
                            graph.Edge(seen, images[(int)node.Call.Target(k)], GraphAccess.Write);
                    break;
                }

                case NodeKind.Trace:
                {
                    CompiledTrace trace = fragment.Trace!;
                    int pass = graph.Pass("trace", BrickKind.Trace, true, place: node.Place);
                    ReadsState(pass);
                    Uses(pass, trace.Buffers, trace.Access);
                    int traced = GraphPlan.TracedImage(fragment);
                    graph.Edge(pass, images[traced], GraphAccess.Write);
                    graph.Edge(pass, images[traced + 1], GraphAccess.Write);
                    if (trace.Shader.ReadsDepth)
                        graph.Edge(pass, depth, GraphAccess.Read);
                    break;
                }

                // What it presents lands on the frame, its depth with it.
                case NodeKind.Present:
                {
                    int present = graph.Pass("present", fragment.Trace is not null && node.Call.Shown == GraphPlan.TracedImage(fragment) ? BrickKind.Trace : BrickKind.Build,
                        false, place: NodePlace.Exit);
                    graph.Edge(present, images[(int)node.Call.Shown], GraphAccess.Read);
                    if (node.Call.ShownDepth != StackBuild.Run.None)
                    {
                        graph.Edge(present, images[(int)node.Call.ShownDepth], GraphAccess.Read);
                        graph.Edge(present, depth, GraphAccess.ReadWrite);
                    }

                    graph.Edge(present, frame, GraphAccess.Write);
                    break;
                }

                case NodeKind.Shade:
                    graph.Shade(fragment, frame, depth, pool, screen, ReadsState, Binds);
                    break;
            }
        }

        return graph;
    }

    /// <summary>The stack's triangles: a DEFORM brick places the corners (without one the host's standard quad does), SHADE colours them.</summary>
    private void Shade(CompiledShader fragment, int frame, int depth, int pool, int screen, Action<int> readsState,
        Action<int, IEnumerable<int>, GraphAccess> binds)
    {
        if (fragment.Vertex is { } vertex)
        {
            int deform = Pass("deform", BrickKind.Deform, false, place: NodePlace.Exit);
            readsState(deform);
            if (pool >= 0)
                Edge(deform, pool, GraphAccess.Read);
            binds(deform, vertex.StackBuffers, GraphAccess.Read);
            // The triangles test the depth and, unless translucent, write it.
            Edge(deform, depth, vertex.Translucent ? GraphAccess.Read : GraphAccess.ReadWrite);
        }

        int shade = Pass("shade", BrickKind.Shade, false, place: NodePlace.Exit);
        readsState(shade);
        if (pool >= 0)
            Edge(shade, pool, GraphAccess.Read);
        binds(shade, fragment.StackBuffers, GraphAccess.Read);
        if (fragment.ReadsDepth)
            Edge(shade, depth, GraphAccess.Read);
        if (screen >= 0)
            Edge(shade, screen, GraphAccess.Read);
        Edge(shade, frame, GraphAccess.Write);
    }

    /// <summary>The passes that write a resource, then those that read it, in run order.</summary>
    public (IEnumerable<int> Writers, IEnumerable<int> Readers) Users(int resource) =>
        (_edges.Where(e => e.Resource == resource && e.Access.HasFlag(GraphAccess.Write)).Select(e => e.Pass).Distinct(),
         _edges.Where(e => e.Resource == resource && e.Access.HasFlag(GraphAccess.Read)).Select(e => e.Pass).Distinct());

    private int Pass(string name, BrickKind brick, bool compute, int calls = 1, long threads = 0, NodePlace place = NodePlace.Prelude)
    {
        _passes.Add(new GraphPass(name, brick, compute, calls, threads) { Place = place });
        return _passes.Count - 1;
    }

    private int Resource(GraphResourceKind kind, string name, long bytes)
    {
        _resources.Add(new GraphResource(kind, name, bytes));
        return _resources.Count - 1;
    }

    private void Edge(int pass, int resource, GraphAccess access) => _edges.Add(new GraphEdge(pass, resource, access));
}
