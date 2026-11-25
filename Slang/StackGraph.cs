using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>What a stack's data is: a graph's buffer or image, or the frame it lands on.</summary>
public enum GraphResourceKind
{
    /// <summary>A graph's <c>List</c> or <c>Counter</c>, kept from frame to frame.</summary>
    Buffer,

    /// <summary>A graph's <c>Image</c>: each item's own (or the material's), for the frame unless kept or a history.</summary>
    Image,

    /// <summary>The target's depth: a present writes it, triangles test and write it, a mid-frame pass reads it.</summary>
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
public sealed record GraphResource(GraphResourceKind Kind, string Name, long Bytes)
{
    /// <summary>For an image: its format, size and how long it lasts (<c>rgba16f · box · kept</c>); empty otherwise.</summary>
    public string Detail { get; init; } = "";
}

/// <summary>
/// One pass of the stack, in the order they first run this frame: its name, the brick it comes
/// from, whether it is compute, and for a BUILD pass how many times <c>build()</c> calls it and
/// the threads those calls run in all (0 for the brick's own passes, which run once).
/// </summary>
public sealed record GraphPass(string Name, StackRole Role, bool Compute, int Calls = 1, long Threads = 0)
{
    /// <summary>Where it runs around the job's render pass; mid-frame costs the item a pass break.</summary>
    public NodePlace Place { get; init; } = NodePlace.Prelude;

    /// <summary>It fills <c>[Cached]</c> images: it runs only when they go stale, not every frame (<see cref="GraphNode.Fills"/>).</summary>
    public bool Fills { get; init; }

    /// <summary>What sort of node it is: a <c>[Pass]</c>, a <c>[Pixels]</c> pass, a <c>[Draw]</c>, or the item's own quad.</summary>
    public NodeKind Kind { get; init; } = NodeKind.Compute;
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
        int[] buffers = fragment.Build is { } built
            ? [.. built.Shape.Buffers.Select((buffer, i) => graph.Resource(GraphResourceKind.Buffer, buffer.Name, built.Bytes[i]))]
            : [];
        int[] images = [.. GraphPlan.Images(fragment).Select(image => graph.Resource(GraphResourceKind.Image, image.Name, 0, Detail(image)))];
        int screen = fragment.ReadsScreen || fragment.Build?.Passes.Any(pass => pass.ReadsScreen) == true
            ? graph.Resource(GraphResourceKind.Screen, "screen", 0)
            : -1;

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
                case NodeKind.Compute or NodeKind.Pixels or NodeKind.Draw:
                {
                    CompiledPass compiled = fragment.Build!.Passes[node.Call.Pass];
                    long threads = node.Kind == NodeKind.Draw ? 0 : node.Call.Over > 0 ? node.Call.Over : (long)node.Call.Groups * compiled.Threads;
                    if (called.TryGetValue(node.Call.Pass, out int seen))
                    {
                        GraphPass was = graph._passes[seen];
                        graph._passes[seen] = was with { Calls = was.Calls + 1, Threads = was.Threads + threads };
                    }
                    else
                    {
                        int pass = graph.Pass(compiled.Name, StackRole.Graph, true, 1, threads, place: node.Place, fills: node.Fills, kind: node.Kind);
                        called[node.Call.Pass] = seen = pass;
                        Uses(pass, compiled.Buffers, compiled.Access);
                        // A pass's images sit at bindings 0.. of set 1, in order: its code says which it reads and which it writes.
                        for (int at = 0; at < compiled.Images.Length; at++)
                        {
                            GraphAccess used = SpirvScan.Access(compiled.Shader.Spirv, 1, at, image: true);
                            graph.Edge(pass, images[compiled.Images[at]], used == 0 ? GraphAccess.ReadWrite : used);
                        }
                        foreach (int image in compiled.Drawn?.Reads ?? [])
                            graph.Edge(pass, images[image], GraphAccess.Read);
                        if (compiled.ReadsScreen)
                            graph.Edge(pass, screen, GraphAccess.Read);
                        if (compiled.ReadsDepth)
                            graph.Edge(pass, depth, GraphAccess.Read);
                        if (compiled.Drawn is { } drawn)
                        {
                            Binds(pass, drawn.Vertex.StackBuffers.Union(drawn.Fragment.StackBuffers), GraphAccess.Read);
                            if (drawn.CopiesBuffer >= 0)
                                Binds(pass, [drawn.CopiesBuffer], GraphAccess.Read);
                        }
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

                case NodeKind.Shade:
                    graph.Shade(fragment, frame, depth, screen, Binds);
                    break;
            }
        }

        return graph;
    }

    /// <summary>The item's quad: its deform places the corners (without one the host's standard quad does), its shade colours them.</summary>
    private void Shade(CompiledShader fragment, int frame, int depth, int screen, Action<int, IEnumerable<int>, GraphAccess> binds)
    {
        if (fragment.Vertex is { } vertex)
        {
            int deform = Pass("deform", StackRole.Deform, false, place: NodePlace.Exit, kind: NodeKind.Shade);
            binds(deform, vertex.StackBuffers, GraphAccess.Read);
            // The triangles test the depth and, unless translucent, write it.
            Edge(deform, depth, vertex.Translucent ? GraphAccess.Read : GraphAccess.ReadWrite);
        }

        int shade = Pass("shade", StackRole.Shade, false, place: NodePlace.Exit, kind: NodeKind.Shade);
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

    private int Pass(string name, StackRole role, bool compute, int calls = 1, long threads = 0, NodePlace place = NodePlace.Prelude,
        bool fills = false, NodeKind kind = NodeKind.Compute)
    {
        _passes.Add(new GraphPass(name, role, compute, calls, threads) { Place = place, Fills = fills, Kind = kind });
        return _passes.Count - 1;
    }

    private int Resource(GraphResourceKind kind, string name, long bytes, string detail = "")
    {
        _resources.Add(new GraphResource(kind, name, bytes) { Detail = detail });
        return _resources.Count - 1;
    }

    /// <summary>An image in a few words: its format, its size against the item's box, and how long it lasts.</summary>
    private static string Detail(PlannedImage image)
    {
        string size = image.Size.Fixed ? $"{image.Size.Width}×{image.Size.Height}"
            : image.Size.Box ? "box" : $"{image.Size.Scale:0.##}× box";
        string life = image.Life switch
        {
            ImageLife.Kept => " · kept",
            ImageLife.Cached => " · cached",
            ImageLife.History => " · last frame",
            _ => "",
        };
        return $"{image.Format} · {size}{life}";
    }

    private void Edge(int pass, int resource, GraphAccess access) => _edges.Add(new GraphEdge(pass, resource, access));
}
