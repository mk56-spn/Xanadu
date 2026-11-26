using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// A stack's render graph (<see cref="StackGraph"/>) on a read-only board, laid out top to bottom as data flows, so
/// every arrow points down. Each write makes a new version of what it writes, a box of its own in the layer below the
/// writer; a pass reads the latest version and sits a layer below it. What is read before anything writes it (a host
/// buffer, last frame's) starts at the top, the targets every draw lands on wait at the bottom, and each layer is
/// ordered by its neighbours so wires cross little.
/// Each box is headed by its name and what it is, in its kind's colour (compute, pixels, draws, the item's quad;
/// buffers, images and the targets), with its value under them: a pass's threads, a buffer's size, an image's format.
/// An arrow for every way data goes: pass to the version it makes, version to the pass that reads it.
/// Remade on every compile that changes it; pans and zooms, and fits itself to its box when remade. Under it a stack
/// (<see cref="GraphUnderlay"/>) sends dots along the wires; a pass that breaks the item's pass, or runs a lot of
/// threads, carries a flag.
/// </summary>
internal sealed class StackGraphView
{
    private const float Width = 230, Height = 62;

    /// <summary>Between two boxes of a layer, and from one layer to the next: room for the wires to fan out.</summary>
    private const float Gap = 80, Step = Height + 56;

    /// <summary>The share of the box the graph may fill when fitted, leaving a margin round it.</summary>
    private const float Fill = 0.9f;

    /// <summary>Theme categories (<see cref="UiThemeRoles.Categories"/>) by what a box is.</summary>
    private const int TargetCategory = 0, ComputeCategory = 2, DrawCategory = 3, PixelsCategory = 4, ImageCategory = 5,
        ShadeCategory = 6, BufferCategory = 7;

    /// <summary>Threads a frame from which a BUILD pass is flagged heavy.</summary>
    private const long HeavyThreads = 1_000_000;

    /// <summary>How many times each column is reordered by its neighbours.</summary>
    private const int Sweeps = 4;

    private readonly UiNode _node;
    private readonly Board _board = new();
    private readonly GraphUnderlay _underlay;
    private string _shown = "", _shape = "";
    private bool _fit;

    public UiNode Node => _node;

    public StackGraphView(UiNode parent)
    {
        _node = parent.Board(_board).Grow().Tag<ReadOnly>().Hidden();
        _underlay = new GraphUnderlay(_board);
    }

    /// <summary>Shows a compile's graph; a graph like the one shown changes nothing, so the view keeps its pan and zoom.</summary>
    public void Show(StackGraph graph)
    {
        string key = Key(graph);
        if (key == _shown)
            return;
        // Only a new shape fits the view again; a count or a size changing keeps where you are.
        string shape = Shape(graph);
        _fit |= shape != _shape;
        (_shown, _shape) = (key, shape);
        _board.Boxes.Clear();
        _board.Links.Clear();
        _board.NextId = 1;
        _underlay.Rebuilt();

        (int[] passColumn, (int Resource, int Column)[] versions, List<Wire> wires) = Versions(graph);
        var columns = new SortedDictionary<int, List<(bool Pass, int Index)>>();
        for (int i = 0; i < passColumn.Length; i++)
            Column(columns, passColumn[i]).Add((true, i));
        for (int v = 0; v < versions.Length; v++)
            Column(columns, versions[v].Column).Add((false, v));
        Order(wires, columns);
        int[] used = [.. columns.Keys];

        int[] passes = new int[graph.Passes.Count];
        int[] boxes = new int[versions.Length];
        foreach ((int column, List<(bool Pass, int Index)> layer) in columns)
        {
            float left = -layer.Count * (Width + Gap) / 2;
            for (int k = 0; k < layer.Count; k++)
            {
                var rect = new Rect2(left + k * (Width + Gap), Array.IndexOf(used, column) * Step, Width, Height);
                (bool isPass, int index) = layer[k];
                if (isPass)
                {
                    GraphPass pass = graph.Passes[index];
                    BoardBox box = _board.Add(rect, Title(pass), Category(pass), Kind(pass), Value(pass));
                    passes[index] = box.Id;
                    (box.Flag, box.FlagTone) = Flag(pass);
                }
                else
                {
                    GraphResource resource = graph.Resources[versions[index].Resource];
                    boxes[index] = _board.Add(rect, resource.Name, Category(resource), Kind(resource), Value(resource)).Id;
                }
            }
        }

        foreach (Wire wire in wires)
            if (wire.Write)
                _board.Link(passes[wire.Pass], boxes[wire.Version]);
            else
                _board.Link(boxes[wire.Version], passes[wire.Pass]);

        if (graph.ScheduleProblem is { } problem)
            _board.Add(new Rect2(-Width * 1.5f, -Step * 1.5f, Width * 3, Height * 1.5f),
                $"graph() can't run: {problem}", TargetCategory);
    }

    /// <summary>Each frame: fits a newly made graph to the box once the box has its size, and feeds the underlay.</summary>
    public void Update()
    {
        if (_node.Entity.IsNull || !_node.Entity.Enabled)
            return;
        BoardState state = _node.Entity.GetComponent<UiBoard>().State;
        _underlay.Update(state, UiThemes.Of(_node.Entity));
        if (!_fit || state.Area.Size.X <= 0 || _board.Boxes.Count == 0)
            return;

        Rect2 all = _board.Boxes.Select(box => box.Rect).Aggregate((a, b) => a.Merge(b));
        state.Zoom = Math.Clamp(Math.Min(state.Area.Size.X / all.Size.X, state.Area.Size.Y / all.Size.Y) * Fill, 0.2f, 1f);
        state.Pan = all.GetCenter() - state.Area.Size / 2 / state.Zoom;
        _fit = false;
    }

    public void Delete() => UiLayout.DeleteSubtree(_node.Entity);

    /// <summary>
    /// What is wrong with a pass, if anything: breaking the item's pass (bad), or a BUILD pass running a million threads
    /// or more a frame (worth a look).
    /// </summary>
    private static (string, FlagTone) Flag(GraphPass pass) =>
        pass.Place == NodePlace.MidFrame ? ("breaks pass", FlagTone.Bad)
        : !pass.Fills && pass.Threads >= HeavyThreads ? ("heavy", FlagTone.Warn)
        : ("", FlagTone.Warn);

    /// <summary>A pass reading a version of a resource, or writing (making) one.</summary>
    private readonly record struct Wire(int Pass, int Version, bool Write);

    /// <summary>
    /// Each pass's layer, each version's (which resource, which layer) and the wires between them, in run order. A pass
    /// goes a layer below the latest version of each thing it reads; each write makes a version a layer below the pass.
    /// A read of something nothing has written yet makes its first version at the top. The targets keep one version,
    /// in the last layer, and every pass touching them writes it: draws land on them whatever their order.
    /// </summary>
    private static (int[] Passes, (int Resource, int Column)[] Versions, List<Wire> Wires) Versions(StackGraph graph)
    {
        int[] passes = new int[graph.Passes.Count];
        List<(int Resource, int Column)> versions = [];
        List<Wire> wires = [];
        var latest = new Dictionary<int, int>();
        var targets = new Dictionary<int, int>();
        int Make(int resource, int column)
        {
            versions.Add((resource, column));
            return versions.Count - 1;
        }

        for (int p = 0; p < passes.Length; p++)
        {
            var access = new Dictionary<int, GraphAccess>();
            foreach (GraphEdge edge in graph.Edges)
                if (edge.Pass == p)
                    access[edge.Resource] = access.GetValueOrDefault(edge.Resource) | edge.Access;

            int column = 1;
            foreach ((int resource, GraphAccess how) in access)
            {
                if (Target(graph.Resources[resource]) || !how.HasFlag(GraphAccess.Read))
                    continue;
                if (!latest.ContainsKey(resource))
                    latest[resource] = Make(resource, 0);
                column = Math.Max(column, versions[latest[resource]].Column + 1);
                wires.Add(new Wire(p, latest[resource], false));
            }

            passes[p] = column;
            foreach ((int resource, GraphAccess how) in access)
            {
                if (Target(graph.Resources[resource]))
                {
                    if (!targets.ContainsKey(resource))
                        targets[resource] = Make(resource, -1);
                    wires.Add(new Wire(p, targets[resource], true));
                }
                else if (how.HasFlag(GraphAccess.Write))
                {
                    latest[resource] = Make(resource, column + 1);
                    wires.Add(new Wire(p, latest[resource], true));
                }
            }
        }

        int last = Math.Max(passes.DefaultIfEmpty(0).Max(), versions.Select(v => v.Column).DefaultIfEmpty(0).Max() - 1) + 1;
        foreach (int target in targets.Values)
            versions[target] = (versions[target].Resource, last);
        return (passes, [.. versions], wires);
    }

    private static bool Target(GraphResource resource) => resource.Kind is GraphResourceKind.Frame or GraphResourceKind.Depth;

    private static List<(bool Pass, int Index)> Column(SortedDictionary<int, List<(bool Pass, int Index)>> columns, int at)
    {
        if (!columns.TryGetValue(at, out List<(bool Pass, int Index)>? column))
            columns[at] = column = [];
        return column;
    }

    /// <summary>
    /// Reorders each column by where its neighbours sit (the barycentre heuristic), sweeping left to right then back, so
    /// a box lines up with what it joins and wires cross little. Ties keep run order.
    /// </summary>
    private static void Order(List<Wire> wires, SortedDictionary<int, List<(bool Pass, int Index)>> columns)
    {
        var place = new Dictionary<(bool, int), float>();
        void Remember()
        {
            foreach (List<(bool Pass, int Index)> column in columns.Values)
                for (int k = 0; k < column.Count; k++)
                    place[column[k]] = k - (column.Count - 1) / 2f;
        }

        IEnumerable<(bool, int)> Neighbours((bool Pass, int Index) box) => wires
            .Where(w => box.Pass ? w.Pass == box.Index : w.Version == box.Index)
            .Select(w => box.Pass ? (false, w.Version) : (true, w.Pass));

        Remember();
        int[] keys = [.. columns.Keys];
        for (int sweep = 0; sweep < Sweeps; sweep++)
        {
            IEnumerable<int> order = sweep % 2 == 0 ? keys.Skip(1) : keys.Reverse().Skip(1);
            foreach (int key in order)
            {
                List<(bool Pass, int Index)> column = columns[key];
                float[] wanted = [.. column.Select((box, k) =>
                {
                    float[] near = [.. Neighbours(box).Where(place.ContainsKey).Select(n => place[n])];
                    return near.Length == 0 ? place[box] : near.Average();
                })];
                List<(bool Pass, int Index)> sorted = [.. column.Select((box, k) => (box, k)).OrderBy(x => wanted[x.k]).ThenBy(x => x.k).Select(x => x.box)];
                columns[key] = sorted;
                Remember();
            }
        }
    }

    /// <summary>A pass's name as the code has it; the generated present draw is just "present".</summary>
    private static string Title(GraphPass pass) => pass.Name.StartsWith("present_", StringComparison.Ordinal) ? "present" : pass.Name;

    private static string Kind(GraphPass pass) => pass.Kind switch
    {
        NodeKind.Pixels => "pixels",
        NodeKind.Draw => "draw",
        NodeKind.Shade => "item quad",
        _ => "compute",
    };

    /// <summary>What a pass costs: how often it runs, its threads (compute), and whether it breaks the item's pass.</summary>
    private static string Value(GraphPass pass)
    {
        List<string> parts = [];
        if (pass.Calls > 1)
            parts.Add($"×{pass.Calls}");
        if (pass.Kind is not (NodeKind.Pixels or NodeKind.Draw or NodeKind.Shade))
            parts.Add(pass.Fills ? "cached" : Threads(pass.Threads));
        if (pass.Place == NodePlace.MidFrame)
            parts.Add("breaks pass");
        return parts.Count == 0 ? "–" : string.Join(" · ", parts);
    }

    private static string Kind(GraphResource resource) => resource.Kind switch
    {
        GraphResourceKind.Buffer => "buffer",
        GraphResourceKind.Image => "image",
        GraphResourceKind.Depth => "depth",
        _ => "target",
    };

    /// <summary>What a resource holds: a buffer's size, an image's format and size, or which target.</summary>
    private static string Value(GraphResource resource) => resource.Kind switch
    {
        GraphResourceKind.Buffer => resource.Detail == "" ? Bytes(resource.Bytes) : $"{Bytes(resource.Bytes)} · {resource.Detail}",
        GraphResourceKind.Image => resource.Detail,
        GraphResourceKind.Frame => "the frame",
        GraphResourceKind.Depth => "the frame's depth",
        _ => "the frame so far",
    };

    private static int Category(GraphPass pass) => pass.Kind switch
    {
        NodeKind.Pixels => PixelsCategory,
        NodeKind.Draw => DrawCategory,
        NodeKind.Shade => ShadeCategory,
        _ => ComputeCategory,
    };

    private static int Category(GraphResource resource) => resource.Kind switch
    {
        GraphResourceKind.Buffer => BufferCategory,
        GraphResourceKind.Image => ImageCategory,
        _ => TargetCategory,
    };

    private static string Threads(long threads) => threads switch
    {
        >= 1_000_000 => $"{threads / 1e6:0.#}M threads",
        >= 10_000 => $"{threads / 1e3:0.#}K threads",
        _ => $"{threads} thread{(threads == 1 ? "" : "s")}",
    };

    private static string Bytes(long bytes) => bytes switch
    {
        >= 1 << 20 => $"{bytes / (double)(1 << 20):0.#} MB",
        >= 1 << 10 => $"{bytes / (double)(1 << 10):0.#} KB",
        _ => $"{bytes} B",
    };

    /// <summary>Which passes and resources there are, whatever their counts and sizes.</summary>
    private static string Shape(StackGraph graph) =>
        string.Join(";", graph.Passes.Select(p => p.Name)) + "|" + string.Join(";", graph.Resources.Select(r => r.Name));

    /// <summary>What the graph shows, as text: two compiles with the same key draw the same board.</summary>
    private static string Key(StackGraph graph) =>
        string.Join(";", graph.Passes.Select(p => $"{p.Name}:{p.Calls}:{p.Threads}:{p.Place}")) + "|" + graph.ScheduleProblem + "|" +
        string.Join(";", graph.Resources.Select(r => $"{r.Name}:{r.Bytes}")) + "|" +
        string.Join(";", graph.Edges.Select(e => $"{e.Pass}-{e.Resource}-{(int)e.Access}"));
}
