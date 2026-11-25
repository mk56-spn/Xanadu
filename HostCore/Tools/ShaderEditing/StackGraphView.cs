using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Slang;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// A stack's render graph (<see cref="StackGraph"/>) on a read-only board: its passes down
/// the left in the order they run, coloured like their bricks on the strip, the data they
/// share down the right, each level with the passes that use it, and an arrow for every way data goes: pass to resource for a
/// write, resource to pass for a read, both for read-write. Remade on every compile that
/// changes it; pans and zooms, and fits itself to its box when remade. Under it a stack (<see cref="GraphUnderlay"/>)
/// sends pulses along the wires and glows under costly passes and the ones that break the item's pass.
/// </summary>
internal sealed class StackGraphView
{
    private const float Width = 170, Height = 50;

    /// <summary>Between two boxes of a column, and between the columns: wide enough for the arrows to fan out.</summary>
    private const float Gap = 16, Apart = 260;

    /// <summary>The share of the box the graph may fill when fitted, leaving a margin round it.</summary>
    private const float Fill = 0.9f;

    /// <summary>Categories for data: what the stack keeps, and the target it lands on.</summary>
    private const int KeptCategory = 1, TargetCategory = 0;

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

        int[] passes = new int[graph.Passes.Count];
        for (int i = 0; i < passes.Length; i++)
        {
            GraphPass pass = graph.Passes[i];
            // A graph pass is named by its function and says what graph() asks of it this frame; the other roles' passes run once.
            string what = pass.Role == StackRole.Graph
                ? $"{(pass.Calls > 1 ? $"×{pass.Calls} · " : "")}{Threads(pass.Threads)}"
                : pass.Compute ? "compute" : "draw";
            // A mid-frame node waits for the canvas drawn so far: the item's pass is broken for it.
            if (pass.Place == NodePlace.MidFrame)
                what += " · breaks pass";
            passes[i] = _board.Add(new Rect2(0, i * (Height + Gap), Width, Height), $"{pass.Name}\n{what}", FileStrip.Category(pass.Role)).Id;
            if (Heat(pass, roles) is { } tone)
                _underlay.Mark(passes[i], tone);
        }

        // Each resource level with the passes that use it, as near as the others leave room for.
        int[] resources = new int[graph.Resources.Count];
        float[] wanted = new float[resources.Length];
        for (int r = 0; r < resources.Length; r++)
        {
            int[] users = [.. graph.Edges.Where(e => e.Resource == r).Select(e => e.Pass).Distinct()];
            wanted[r] = users.Length == 0 ? 0 : (float)users.Average() * (Height + Gap);
        }

        float next = 0;
        foreach (int r in Enumerable.Range(0, resources.Length).OrderBy(r => wanted[r]))
        {
            GraphResource resource = graph.Resources[r];
            bool target = resource.Kind is GraphResourceKind.Frame or GraphResourceKind.Depth or GraphResourceKind.Screen;
            float y = Math.Max(wanted[r], next);
            next = y + Height + Gap;
            resources[r] = _board.Add(new Rect2(Width + Apart, y, Width, Height),
                $"{resource.Name}\n{(target ? "target" : Bytes(resource.Bytes))}", target ? TargetCategory : KeptCategory).Id;
        }

        foreach (GraphEdge edge in graph.Edges)
        {
            if (edge.Access.HasFlag(GraphAccess.Write))
                _board.Link(passes[edge.Pass], resources[edge.Resource]);
            if (edge.Access.HasFlag(GraphAccess.Read))
                _board.Link(resources[edge.Resource], passes[edge.Pass]);
        }

        if (graph.ScheduleProblem is { } problem)
            _board.Add(new Rect2(0, -(Height + Gap) * 2, Width * 2 + Apart, Height * 1.5f), $"build() can't run: {problem}", TargetCategory);
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
        if (pass.Threads < 1000)
            return null;
        return roles.Warn with { A = 0.15f + 0.5f * Math.Clamp((MathF.Log10(pass.Threads) - 3) / 4, 0, 1) };
    }

    /// <summary>The first pass that touches a resource.</summary>
    private static int First(StackGraph graph, int resource) =>
        graph.Edges.Where(e => e.Resource == resource).Select(e => e.Pass).DefaultIfEmpty(int.MaxValue).Min();

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
