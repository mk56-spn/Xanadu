using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Routes a board's lines with right angles only. Each line tries every shape that leaves one side of its box and
/// enters a side of the other: across a gap and back (a Z, down-across-down or out-across-in), round one corner (an
/// L), or out of both by the same side and round (a U, over whatever is between them). It keeps the shape that crosses the fewest boxes, then the shortest. Then the lines on one side of a box
/// each get their own spot along it, ordered by where they go so they don't cross there. Lines crossing the same gap
/// each get their own level in it; then <see cref="BoardLanes"/> moves apart any runs still lying on each other.
/// Frames are not in the way.
/// </summary>
internal static class BoardRoutes
{
    private enum Side { Top, Bottom, Left, Right }

    /// <summary>The share of a box's side the spots spread over, and of a gap the levels spread over.</summary>
    private const float SideShare = 0.8f, GapShare = 0.7f;

    /// <summary>What a box in the way costs against length, and a bend.</summary>
    private const float CrossCost = 100_000, BendCost = 20;

    /// <summary>How far a U goes past the boxes it goes round, how far apart Us at one level run, and how many boxes in its way it steps past.</summary>
    private const float Reach = 30, Lane = 10;
    private const int Steps = 6;

    private sealed class Wire(int at, BoardBox from, BoardBox to)
    {
        public readonly int At = at;
        public readonly BoardBox From = from, To = to;
        public Side Exit, Entry;
        public float ExitAt = 0.5f, EntryAt = 0.5f, Level;

        /// <summary>A Z or a U: leaves and enters along the same axis, so it turns twice at a level.</summary>
        public bool TwoTurns => Vertical(Exit) == Vertical(Entry);

        /// <summary>A Z: leaves and enters by opposite sides, its level between the two boxes.</summary>
        public bool Zed => TwoTurns && Exit != Entry;
    }

    public static List<List<Vector2>?> Paths(Board board)
    {
        List<BoardBox> solid = [.. board.Boxes.Where(box => !board.IsFrame(box))];
        var paths = new List<List<Vector2>?>(board.Links.Count);
        List<Wire> wires = [];
        for (int at = 0; at < board.Links.Count; at++)
        {
            BoardLink link = board.Links[at];
            paths.Add(null);
            if (board.DrawnByPartner(link) || board.Find(link.From) is not { } from || board.Find(link.To) is not { } to)
                continue;
            var wire = new Wire(at, from, to);
            if (Choose(wire, solid))
                wires.Add(wire);
            else
            {
                (Vector2 a, Vector2 b) = Board.Ends(from.Rect, to.Rect);
                paths[at] = [a, b];
            }
        }

        foreach (var side in wires.GroupBy(w => (w.From.Id, w.Exit)))
            Spread([.. side.OrderBy(w => Along(w.Exit, Point(w.To.Rect, w.Entry, 0.5f)))], (w, t) => w.ExitAt = t);
        foreach (var side in wires.GroupBy(w => (w.To.Id, w.Entry)))
            Spread([.. side.OrderBy(w => Along(w.Entry, Point(w.From.Rect, w.Exit, 0.5f)))], (w, t) => w.EntryAt = t);

        // Zs leaving across the same gap take a level each, in order along it.
        foreach (var gap in wires.Where(w => w.Zed).GroupBy(w => (w.Exit, MathF.Round(Across(w.Exit, Point(w.From.Rect, w.Exit, 0))))))
        {
            Wire[] across = [.. gap.OrderBy(w => Along(w.Exit, Point(w.From.Rect, w.Exit, w.ExitAt)))];
            float start = Across(w: across[0], exit: true);
            float room = across.Min(w => MathF.Abs(Across(w, false) - Across(w, true)));
            float sign = MathF.Sign(Across(across[0], false) - start);
            for (int k = 0; k < across.Length; k++)
                across[k].Level = start + sign * room * ((1 - GapShare) / 2 + GapShare * (k + 0.5f) / across.Length);
        }

        // Us round at the same level step out a lane each.
        foreach (var round in wires.Where(w => w.TwoTurns && !w.Zed).GroupBy(w => (w.Exit, MathF.Round(w.Level))))
        {
            Wire[] us = [.. round.OrderBy(w => Along(w.Exit, Point(w.From.Rect, w.Exit, w.ExitAt)))];
            for (int k = 0; k < us.Length; k++)
                us[k].Level += Outward(us[k].Exit) * Lane * k;
        }

        var ends = new Dictionary<int, (Rect2, Rect2)>();
        foreach (Wire wire in wires)
        {
            paths[wire.At] = Shape(wire);
            ends[wire.At] = (wire.From.Rect, wire.To.Rect);
        }

        BoardLanes.Spread(paths, at => ends.TryGetValue(at, out var both) ? both : null);
        return paths;
    }

    /// <summary>Picks the wire's sides: the shape crossing the fewest boxes, then the shortest. False when none fits.</summary>
    private static bool Choose(Wire wire, List<BoardBox> solid)
    {
        float best = float.MaxValue;
        (Side, Side, float) chosen = default;
        foreach (Side exit in Enum.GetValues<Side>())
        foreach (Side entry in Enum.GetValues<Side>())
        {
            (wire.Exit, wire.Entry) = (exit, entry);
            if (wire.Zed)
            {
                float from = Across(wire, true), to = Across(wire, false);
                // A Z needs the boxes apart, the way it leaves.
                if (Outward(exit) * (to - from) <= 0 || Outward(entry) * (from - to) <= 0)
                    continue;
                wire.Level = (from + to) / 2;
            }

            else if (exit == entry)
                wire.Level = Around(wire, solid);

            List<Vector2> path = Shape(wire);
            if (!Clear(path, wire))
                continue;
            float cost = Cost(path, solid, wire);
            if (cost < best)
                (best, chosen) = (cost, (exit, entry, wire.Level));
        }

        (wire.Exit, wire.Entry, wire.Level) = chosen;
        return best < float.MaxValue;
    }

    /// <summary>A U's level: past both its boxes, then past each box its level line would run through.</summary>
    private static float Around(Wire wire, List<BoardBox> solid)
    {
        float out_ = Outward(wire.Exit);
        float Past(Rect2 rect) => Across(wire.Exit, Point(rect, wire.Exit, 0)) + out_ * Reach;
        float level = out_ > 0 ? MathF.Max(Past(wire.From.Rect), Past(wire.To.Rect)) : MathF.Min(Past(wire.From.Rect), Past(wire.To.Rect));
        for (int step = 0; step < Steps; step++)
        {
            wire.Level = level;
            List<Vector2> path = Shape(wire);
            BoardBox? hit = solid.FirstOrDefault(box => box != wire.From && box != wire.To
                && Board.Crosses(path[1], path[2], box.Rect.Grow(Board.Clearance), out _));
            if (hit is null)
                break;
            level = Past(hit.Rect);
        }

        return level;
    }

    /// <summary>Whether a path stays out of its own two boxes past where it leaves and enters them.</summary>
    private static bool Clear(List<Vector2> path, Wire wire)
    {
        for (int i = 1; i < path.Count; i++)
            if (Board.Crosses(path[i - 1], path[i], wire.From.Rect.Grow(-1), out _) || Board.Crosses(path[i - 1], path[i], wire.To.Rect.Grow(-1), out _))
                return false;
        // An L's corner must lie beyond both sides it turns from.
        if (!wire.TwoTurns && path.Count == 3)
            return Outward(wire.Exit) * (Across(wire.Exit, path[1]) - Across(wire.Exit, path[0])) > 0
                && Outward(wire.Entry) * (Across(wire.Entry, path[1]) - Across(wire.Entry, path[2])) > 0;
        return true;
    }

    private static float Cost(List<Vector2> path, List<BoardBox> solid, Wire wire)
    {
        float cost = (path.Count - 2) * BendCost;
        for (int i = 1; i < path.Count; i++)
        {
            cost += Vector2.Distance(path[i - 1], path[i]);
            foreach (BoardBox box in solid)
                if (box != wire.From && box != wire.To && Board.Crosses(path[i - 1], path[i], box.Rect.Grow(Board.Clearance), out _))
                    cost += CrossCost;
        }

        return cost;
    }

    /// <summary>The wire's points for its sides, spots and level.</summary>
    private static List<Vector2> Shape(Wire wire)
    {
        Vector2 a = Point(wire.From.Rect, wire.Exit, wire.ExitAt), b = Point(wire.To.Rect, wire.Entry, wire.EntryAt);
        if (!wire.TwoTurns)
            return Vertical(wire.Exit) ? [a, new(a.X, b.Y), b] : [a, new(b.X, a.Y), b];
        if (wire.Zed && MathF.Abs(Along(wire.Exit, a) - Along(wire.Exit, b)) < 0.5f)
            return [a, b];
        return Vertical(wire.Exit)
            ? [a, new(a.X, wire.Level), new(b.X, wire.Level), b]
            : [a, new(wire.Level, a.Y), new(wire.Level, b.Y), b];
    }

    /// <summary>Spreads a side's wires evenly along it, in the order given.</summary>
    private static void Spread(Wire[] side, Action<Wire, float> place)
    {
        for (int k = 0; k < side.Length; k++)
            place(side[k], (1 - SideShare) / 2 + SideShare * (k + 1) / (side.Length + 1));
    }

    /// <summary>A point on a box's side, <paramref name="t"/> of the way along it.</summary>
    private static Vector2 Point(Rect2 rect, Side side, float t) => side switch
    {
        Side.Top => new(rect.Position.X + rect.Size.X * t, rect.Position.Y),
        Side.Bottom => new(rect.Position.X + rect.Size.X * t, rect.End.Y),
        Side.Left => new(rect.Position.X, rect.Position.Y + rect.Size.Y * t),
        _ => new(rect.End.X, rect.Position.Y + rect.Size.Y * t),
    };

    /// <summary>Whether a line leaves the side going up or down.</summary>
    private static bool Vertical(Side side) => side is Side.Top or Side.Bottom;

    /// <summary>Which way is out of the side, along the axis it faces.</summary>
    private static float Outward(Side side) => side is Side.Bottom or Side.Right ? 1 : -1;

    /// <summary>A point's place along a side (x for top and bottom), and across it (y).</summary>
    private static float Along(Side side, Vector2 point) => Vertical(side) ? point.X : point.Y;

    private static float Across(Side side, Vector2 point) => Vertical(side) ? point.Y : point.X;

    /// <summary>Where a wire's exit side (or entry side) sits, across it.</summary>
    private static float Across(Wire w, bool exit) =>
        exit ? Across(w.Exit, Point(w.From.Rect, w.Exit, 0)) : Across(w.Exit, Point(w.To.Rect, w.Entry, 0));
}
