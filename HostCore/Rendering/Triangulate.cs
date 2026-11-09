using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Host.Rendering;

/// <summary>Turns a simple polygon, either winding, into triangles by ear clipping.</summary>
internal static class Triangulate
{
    public static int[] Polygon(ReadOnlySpan<Vector2> points)
    {
        int n = points.Length;
        if (n < 3)
            return [];
        if (n == 3)
            return [0, 1, 2];

        float area = 0f;
        for (int i = 0, j = n - 1; i < n; j = i++)
            area += points[j].X * points[i].Y - points[i].X * points[j].Y;
        float winding = area >= 0f ? 1f : -1f;

        var remaining = new List<int>(n);
        for (int i = 0; i < n; i++)
            remaining.Add(i);

        var result = new List<int>((n - 2) * 3);
        int guard = n * n;
        int at = 0;
        while (remaining.Count > 3 && guard-- > 0)
        {
            int count = remaining.Count;
            int ia = remaining[(at + count - 1) % count], ib = remaining[at % count], ic = remaining[(at + 1) % count];
            if (IsEar(points, remaining, ia, ib, ic, winding))
            {
                result.Add(ia);
                result.Add(ib);
                result.Add(ic);
                remaining.RemoveAt(at % count);
                continue;
            }

            at = (at + 1) % count;
        }

        if (remaining.Count == 3)
        {
            result.Add(remaining[0]);
            result.Add(remaining[1]);
            result.Add(remaining[2]);
        }
        else
        {
            // A self-intersecting outline has no clean ears left: fan the rest so something still draws.
            for (int i = 1; i + 1 < remaining.Count; i++)
            {
                result.Add(remaining[0]);
                result.Add(remaining[i]);
                result.Add(remaining[i + 1]);
            }
        }

        return result.ToArray();
    }

    private static bool IsEar(ReadOnlySpan<Vector2> p, List<int> remaining, int ia, int ib, int ic, float winding)
    {
        Vector2 a = p[ia], b = p[ib], c = p[ic];
        if (Cross(a, b, c) * winding <= 0f)
            return false;

        foreach (int i in remaining)
        {
            if (i == ia || i == ib || i == ic)
                continue;
            if (Inside(p[i], a, b, c, winding))
                return false;
        }

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool Inside(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float winding) =>
        Cross(a, b, point) * winding >= 0f &&
        Cross(b, c, point) * winding >= 0f &&
        Cross(c, a, point) * winding >= 0f;
}
