using System.Collections.Generic;

namespace Xanadu.Ui.Editing;

/// <summary>One place a line hops over another: which of its segments, and how far along it.</summary>
public readonly record struct LineHop(int Segment, float At);

/// <summary>
/// Where lines cross, so a drawing can make each crossing a little bridge: a line hops
/// over every line drawn before it, and the earlier one runs straight under. Crossings
/// within <c>clear</c> of either segment's ends aren't hops (lines leaving the same box,
/// or a corner of a line's own route).
/// </summary>
public static class LineHops
{
    /// <summary>For each path (null for none), its hops over the paths before it, in order along it.</summary>
    public static List<List<LineHop>> Of(IReadOnlyList<List<Vector2>?> paths, float clear)
    {
        var all = new List<List<LineHop>>(paths.Count);
        for (int p = 0; p < paths.Count; p++)
        {
            var hops = new List<LineHop>();
            all.Add(hops);
            if (paths[p] is not { } path)
                continue;

            for (int s = 1; s < path.Count; s++)
            {
                Vector2 a = path[s - 1], b = path[s];
                int first = hops.Count;
                for (int q = 0; q < p; q++)
                {
                    if (paths[q] is not { } under)
                        continue;
                    for (int u = 1; u < under.Count; u++)
                    {
                        if (Crossing(a, b, under[u - 1], under[u], clear) is { } at)
                            hops.Add(new LineHop(s - 1, at));
                    }
                }

                hops.Sort(first, hops.Count - first, Comparer<LineHop>.Create((x, y) => x.At.CompareTo(y.At)));
            }
        }

        return all;
    }

    /// <summary>How far along a→b it crosses c→d, when both cross well clear of their ends; null otherwise.</summary>
    private static float? Crossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float clear)
    {
        Vector2 r = b - a, s = d - c;
        float across = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(across) < 1e-6f)
            return null;

        Vector2 ac = c - a;
        float t = (ac.X * s.Y - ac.Y * s.X) / across;
        float u = (ac.X * r.Y - ac.Y * r.X) / across;
        float length = r.Length(), other = s.Length();
        float at = t * length, along = u * other;
        if (at < clear || at > length - clear || along < clear || along > other - clear)
            return null;
        return at;
    }
}
