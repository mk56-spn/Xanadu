using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Ui.Editing;

/// <summary>
/// Keeps right-angled lines from lying on each other: after routing, each upright run that overlaps another along
/// the same x (and each level run along the same y) steps sideways to the nearest free lane, a few units over. The
/// runs next to it stretch or shrink to meet it, so the line stays right-angled. A run leaving or entering a box only
/// steps along that box's side, so the line still touches it.
/// </summary>
internal static class BoardLanes
{
    /// <summary>How far apart two parallel runs keep, and how many lanes either way a run looks for a free one.</summary>
    private const float Apart = 13;
    private const int Tries = 8;

    /// <summary>How far a run on a box's side keeps from its corners.</summary>
    private const float CornerRoom = 4;

    /// <summary>One run of a line: points [Index - 1] to [Index] of path Path.</summary>
    private readonly record struct Run(int Path, int Index, float Coord, float Low, float High);

    /// <summary>
    /// Spreads the runs of <paramref name="paths"/> (right-angled ones only; others are left alone). <paramref name="ends"/>
    /// gives each path's two boxes, so its first and last runs keep to their sides.
    /// </summary>
    public static void Spread(List<List<Vector2>?> paths, Func<int, (Rect2 From, Rect2 To)?> ends)
    {
        for (int p = 0; p < paths.Count; p++)
            if (paths[p] is { } path)
                paths[p] = Tidy(path);
        Pass(paths, ends, true);
        Pass(paths, ends, false);
    }

    private static void Pass(List<List<Vector2>?> paths, Func<int, (Rect2 From, Rect2 To)?> ends, bool upright)
    {
        List<Run> runs = [];
        for (int p = 0; p < paths.Count; p++)
        {
            if (paths[p] is not { } path || ends(p) is null || !Square(path))
                continue;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                if ((a.X == b.X) != upright || a == b)
                    continue;
                (float c, float a0, float b0) = upright ? (a.X, a.Y, b.Y) : (a.Y, a.X, b.X);
                runs.Add(new Run(p, i, c, MathF.Min(a0, b0), MathF.Max(a0, b0)));
            }
        }

        List<Run> placed = [];
        // Longest first: the runs that cross the most of the board keep their place, short ones make room.
        foreach (Run run in runs.OrderByDescending(r => r.High - r.Low).ThenBy(r => r.Coord))
        {
            List<Vector2> path = paths[run.Path]!;
            (float Low, float High)? side = Side(path, run, ends(run.Path)!.Value, upright);
            float chosen = run.Coord;
            for (int k = 0; k <= Tries * 2; k++)
            {
                float c = run.Coord + (k % 2 == 1 ? 1 : -1) * ((k + 1) / 2) * Apart;
                if (side is { } s && (c < s.Low || c > s.High))
                    continue;
                if (!placed.Any(o => MathF.Abs(o.Coord - c) < Apart - 0.01f && o.Low < run.High + Apart / 2 && run.Low < o.High + Apart / 2))
                {
                    chosen = c;
                    break;
                }
            }

            Move(path, run.Index - 1, chosen, upright);
            Move(path, run.Index, chosen, upright);
            placed.Add(run with { Coord = chosen });
        }
    }

    /// <summary>For a path's first or last run, the stretch of its box's side it may slide along; null for the rest.</summary>
    private static (float, float)? Side(List<Vector2> path, Run run, (Rect2 From, Rect2 To) ends, bool upright)
    {
        Rect2? box = run.Index == 1 ? ends.From : run.Index == path.Count - 1 ? ends.To : null;
        if (box is not { } rect)
            return null;
        return upright
            ? (rect.Position.X + CornerRoom, rect.End.X - CornerRoom)
            : (rect.Position.Y + CornerRoom, rect.End.Y - CornerRoom);
    }

    private static void Move(List<Vector2> path, int at, float coord, bool upright) =>
        path[at] = upright ? path[at] with { X = coord } : path[at] with { Y = coord };

    private static bool Square(List<Vector2> path)
    {
        for (int i = 1; i < path.Count; i++)
            if (path[i - 1].X != path[i].X && path[i - 1].Y != path[i].Y)
                return false;
        return true;
    }

    /// <summary>Drops repeated points and points in the middle of a straight run, so runs alternate upright and level.</summary>
    private static List<Vector2> Tidy(List<Vector2> path)
    {
        List<Vector2> kept = [];
        foreach (Vector2 point in path)
        {
            if (kept.Count > 0 && kept[^1] == point)
                continue;
            if (kept.Count >= 2 && ((kept[^2].X == kept[^1].X && kept[^1].X == point.X) || (kept[^2].Y == kept[^1].Y && kept[^1].Y == point.Y)))
                kept[^1] = point;
            else
                kept.Add(point);
        }

        return kept;
    }
}
