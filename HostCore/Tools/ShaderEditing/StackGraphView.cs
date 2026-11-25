using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// A stack's render graph (<see cref="StackGraph"/>) on a read-only board, laid out top to bottom as data flows: each
/// pass a layer below what it reads, each resource a layer below the pass that first writes it (inputs nothing writes
/// at the top, the frame at the bottom), and each layer ordered by its neighbours so wires cross little.
/// Boxes are coloured by what they are: compute, pixels, draws, the item's quad; buffers, images and the targets. An
/// arrow for every way data goes: pass to resource for a write, resource to pass for a read, both for read-write.
/// Remade on every compile that changes it; pans and zooms, and fits itself to its box when remade. Under it a stack
/// (<see cref="GraphUnderlay"/>) sends pulses along the wires and glows under costly passes and the ones that break
/// the item's pass.
/// </summary>
internal sealed class StackGraphView
{
    private const float Width = 210, Height = 50;

    /// <summary>Between two boxes of a layer, and from one layer to the next: room for the wires to fan out.</summary>
    private const float Gap = 30, Step = Height + 56;

    /// <summary>The share of the box the graph may fill when fitted, leaving a margin round it.</summary>
    private const float Fill = 0.9f;

    /// <summary>Theme categories (<see cref="UiThemeRoles.Categories"/>) by what a box is.</summary>
    private const int TargetCategory = 0, ComputeCategory = 2, DrawCategory = 3, PixelsCategory = 4, ImageCategory = 5,
        ShadeCategory = 6, BufferCategory = 7;

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
        UiThemeRoles roles = UiThemes.Of(_node.Entity);

        (int[] passColumn, int[] resourceColumn) = Columns(graph);
        var columns = new SortedDictionary<int, List<(bool Pass, int Index)>>();
        for (int i = 0; i < passColumn.Length; i++)
            Column(columns, passColumn[i]).Add((true, i));
        for (int r = 0; r < resourceColumn.Length; r++)
            if (resourceColumn[r] >= 0)
                Column(columns, resourceColumn[r]).Add((false, r));
        Order(graph, columns);
        // Layers nothing landed in (a resource written again later keeps its first place) close up.
        int[] used = [.. columns.Keys];

        int[] passes = new int[graph.Passes.Count];
        int[] resources = new int[graph.Resources.Count];
        foreach ((int column, List<(bool Pass, int Index)> boxes) in columns)
        {
            float left = -boxes.Count * (Width + Gap) / 2;
            for (int k = 0; k < boxes.Count; k++)
            {
                var rect = new Rect2(left + k * (Width + Gap), Array.IndexOf(used, column) * Step, Width, Height);
                (bool isPass, int index) = boxes[k];
                if (isPass)
                {
                    GraphPass pass = graph.Passes[index];
                    passes[index] = _board.Add(rect, $"{Title(pass)}\n{Says(pass)}", Category(pass)).Id;
                    if (Heat(pass, roles) is { } tone)
                        _underlay.Mark(passes[index], tone);
                }
                else
                {
                    GraphResource resource = graph.Resources[index];
                    resources[index] = _board.Add(rect, $"{resource.Name}\n{Says(resource)}", Category(resource)).Id;
                }
            }
        }

        foreach (GraphEdge edge in graph.Edges)
        {
            if (resources[edge.Resource] == 0)
                continue;
            if (edge.Access.HasFlag(GraphAccess.Write))
                _board.Link(passes[edge.Pass], resources[edge.Resource]);
            if (edge.Access.HasFlag(GraphAccess.Read))
                _board.Link(resources[edge.Resource], passes[edge.Pass]);
        }

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
    /// How a pass glows under its box: a pass break strongly, in the bad tone; a BUILD pass by how many threads it
    /// runs, from a faint glow at a thousand to full at ten million; nothing for the rest.
    /// </summary>
    private static Color? Heat(GraphPass pass, UiThemeRoles roles)
    {
        if (pass.Place == NodePlace.MidFrame)
            return roles.Bad with { A = 0.7f };
        if (pass.Threads < 1000 || pass.Fills)
            return null;
        return roles.Warn with { A = 0.15f + 0.5f * Math.Clamp((MathF.Log10(pass.Threads) - 3) / 4, 0, 1) };
    }

    /// <summary>
    /// Each pass's column (odd) and each resource's (even; -1 for one nothing touches). A pass goes one past the latest
    /// write of what it reads, so it lands after the passes it waits on; a resource sits one past its first writer, or
    /// at 0 when it is read before anything writes it (a host buffer, last frame's).
    /// </summary>
    private static (int[] Passes, int[] Resources) Columns(StackGraph graph)
    {
        int[] passes = new int[graph.Passes.Count];
        int[] resources = Enumerable.Repeat(-1, graph.Resources.Count).ToArray();
        int[] ready = new int[graph.Resources.Count];
        bool[] touched = new bool[graph.Resources.Count];
        for (int p = 0; p < passes.Length; p++)
        {
            // The targets wait at the bottom: draws onto the frame all land there, whatever their order.
            GraphEdge[] edges = [.. graph.Edges.Where(e => e.Pass == p && !Target(graph.Resources[e.Resource]))];
            int column = 1;
            foreach (GraphEdge edge in edges)
                if (edge.Access.HasFlag(GraphAccess.Read))
                    column = Math.Max(column, ready[edge.Resource] + 1);
            passes[p] = column;
            foreach (GraphEdge edge in edges)
            {
                touched[edge.Resource] = true;
                if (!edge.Access.HasFlag(GraphAccess.Write))
                    continue;
                if (resources[edge.Resource] < 0)
                    resources[edge.Resource] = column + 1;
                ready[edge.Resource] = Math.Max(ready[edge.Resource], column + 1);
            }
        }

        int last = passes.DefaultIfEmpty(0).Max() + 1;
        for (int r = 0; r < resources.Length; r++)
            if (Target(graph.Resources[r]))
                resources[r] = graph.Edges.Any(e => e.Resource == r) ? last : -1;
            else if (resources[r] < 0 && touched[r])
                resources[r] = 0;
        return (passes, resources);
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
    private static void Order(StackGraph graph, SortedDictionary<int, List<(bool Pass, int Index)>> columns)
    {
        var place = new Dictionary<(bool, int), float>();
        void Remember()
        {
            foreach (List<(bool Pass, int Index)> column in columns.Values)
                for (int k = 0; k < column.Count; k++)
                    place[column[k]] = k - (column.Count - 1) / 2f;
        }

        IEnumerable<(bool, int)> Neighbours((bool Pass, int Index) box) => graph.Edges
            .Where(e => box.Pass ? e.Pass == box.Index : e.Resource == box.Index)
            .Select(e => box.Pass ? (false, e.Resource) : (true, e.Pass));

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

    /// <summary>What a pass is and what it costs, in a few words.</summary>
    private static string Says(GraphPass pass)
    {
        string calls = pass.Calls > 1 ? $"×{pass.Calls} · " : "";
        string what = pass.Kind switch
        {
            NodeKind.Pixels => "pixels",
            NodeKind.Draw => "draw",
            NodeKind.Shade => "the item's quad",
            _ => pass.Fills ? "cached" : Threads(pass.Threads),
        };
        return calls + what + (pass.Place == NodePlace.MidFrame ? " · breaks pass" : "");
    }

    /// <summary>What a resource is, in a few words: a buffer's size, an image's format and size, or which target.</summary>
    private static string Says(GraphResource resource) => resource.Kind switch
    {
        GraphResourceKind.Buffer => Bytes(resource.Bytes),
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
