using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Builds a skin's mesh from its art's outline: points along the outline and a
/// staggered fill inside it, joined into triangles (Delaunay, so they are as
/// even as they can be) of which only those inside the outline are kept. The
/// outline is followed to within the point spacing, as a Spine or DragonBones
/// mesh traced round its image is.
/// </summary>
public static class XafSkinBuilder
{
    /// <summary>Points in art space and their triangles, about <paramref name="spacing"/> apart.</summary>
    public static (Vector2[] Points, int[] Triangles) Build(IReadOnlyList<Vector2[]> outline, float spacing)
    {
        spacing = Math.Max(0.5f, spacing);
        var points = new List<Vector2>();

        // Along each outline, at the spacing; outline that runs inside another shape is not an edge.
        foreach (Vector2[] polygon in outline)
        {
            foreach (Vector2 point in Resample(polygon, spacing))
            {
                if (!InsideAnother(outline, polygon, point))
                    points.Add(point);
            }
        }
        int edgePoints = points.Count;

        // A staggered fill, kept half a spacing clear of the edge points.
        Rect2 bounds = Bounds(outline);
        float rowStep = spacing * 0.866f;
        int row = 0;
        for (float y = bounds.Position.Y + rowStep / 2; y < bounds.End.Y; y += rowStep, row++)
        {
            float shift = row % 2 == 0 ? 0f : spacing / 2;
            for (float x = bounds.Position.X + shift; x < bounds.End.X; x += spacing)
            {
                var point = new Vector2(x, y);
                if (!XafSvgOutline.Inside(outline, point))
                    continue;
                bool clear = true;
                for (int i = 0; i < edgePoints && clear; i++)
                    clear = points[i].DistanceSquaredTo(point) > spacing * spacing * 0.36f;
                if (clear)
                    points.Add(point);
            }
        }

        int[] all = Delaunay(points);
        var kept = new List<int>();
        for (int t = 0; t < all.Length; t += 3)
        {
            Vector2 a = points[all[t]], b = points[all[t + 1]], c = points[all[t + 2]];
            // Inside if its middle and the middles of its sides are: a triangle
            // bridging a hollow such as the back of the knee has one outside.
            if (XafSvgOutline.Inside(outline, (a + b + c) / 3f) &&
                XafSvgOutline.Inside(outline, (a + b) / 2f) &&
                XafSvgOutline.Inside(outline, (b + c) / 2f) &&
                XafSvgOutline.Inside(outline, (c + a) / 2f))
            {
                kept.AddRange(new[] { all[t], all[t + 1], all[t + 2] });
            }
        }
        // Edge midpoints of a triangle along the edge lie on the outline itself, where
        // the test can fall either way; such a triangle is kept if its middle is inside.
        for (int t = 0; t < all.Length; t += 3)
        {
            Vector2 a = points[all[t]], b = points[all[t + 1]], c = points[all[t + 2]];
            if (!XafSvgOutline.Inside(outline, (a + b + c) / 3f))
                continue;
            bool listed = false;
            for (int k = 0; k < kept.Count && !listed; k += 3)
                listed = kept[k] == all[t] && kept[k + 1] == all[t + 1] && kept[k + 2] == all[t + 2];
            if (!listed && EdgeOnOutline(all[t], all[t + 1], all[t + 2], edgePoints, outline, points, spacing))
                kept.AddRange(new[] { all[t], all[t + 1], all[t + 2] });
        }

        // Points no kept triangle uses are dropped, and the rest renumbered.
        var used = new int[points.Count];
        Array.Fill(used, -1);
        var finalPoints = new List<Vector2>();
        var triangles = new int[kept.Count];
        for (int i = 0; i < kept.Count; i++)
        {
            int p = kept[i];
            if (used[p] < 0)
            {
                used[p] = finalPoints.Count;
                finalPoints.Add(points[p]);
            }
            triangles[i] = used[p];
        }
        return (finalPoints.ToArray(), triangles);
    }

    /// <summary>
    /// A triangle with two neighbouring outline points whose side between them
    /// fails the inside test only because it runs along a hollow stretch of the
    /// outline: its other sides pass.
    /// </summary>
    private static bool EdgeOnOutline(int i, int j, int k, int edgePoints, IReadOnlyList<Vector2[]> outline,
        List<Vector2> points, float spacing)
    {
        int[] corners = { i, j, k };
        int outside = 0;
        for (int s = 0; s < 3; s++)
        {
            int a = corners[s], b = corners[(s + 1) % 3];
            if (XafSvgOutline.Inside(outline, (points[a] + points[b]) / 2f))
                continue;
            // Only a side between neighbouring outline points runs along it; a
            // longer one bridges a hollow, such as the back of the knee.
            if (a >= edgePoints || b >= edgePoints || points[a].DistanceTo(points[b]) > spacing * 1.6f)
                return false;
            outside++;
        }
        return outside <= 1;
    }

    private static bool InsideAnother(IReadOnlyList<Vector2[]> outline, Vector2[] own, Vector2 point)
    {
        foreach (Vector2[] other in outline)
        {
            if (other != own && XafSvgOutline.Inside(new[] { other }, point))
                return true;
        }
        return false;
    }

    /// <summary>Points round a closed polygon about <paramref name="spacing"/> apart along it.</summary>
    public static List<Vector2> Resample(Vector2[] polygon, float spacing)
    {
        float perimeter = 0f;
        for (int i = 0; i < polygon.Length; i++)
            perimeter += polygon[i].DistanceTo(polygon[(i + 1) % polygon.Length]);
        int count = Math.Max(3, (int)MathF.Round(perimeter / spacing));
        float step = perimeter / count;

        var result = new List<Vector2>(count);
        float travelled = 0f, next = 0f;
        for (int i = 0; i < polygon.Length && result.Count < count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
            float length = a.DistanceTo(b);
            while (next <= travelled + length && result.Count < count)
            {
                result.Add(length < 1e-6f ? a : a.Lerp(b, (next - travelled) / length));
                next += step;
            }
            travelled += length;
        }
        return result;
    }

    private static Rect2 Bounds(IReadOnlyList<Vector2[]> outline)
    {
        var bounds = new Rect2(outline[0][0], Vector2.Zero);
        foreach (Vector2[] polygon in outline)
        {
            foreach (Vector2 point in polygon)
                bounds = bounds.Expand(point);
        }
        return bounds;
    }

    /// <summary>Bowyer–Watson Delaunay triangulation; three indices per triangle.</summary>
    public static int[] Delaunay(IReadOnlyList<Vector2> points)
    {
        int n = points.Count;
        if (n < 3)
            return Array.Empty<int>();

        var all = new List<Vector2>(points);
        Rect2 bounds = new(points[0], Vector2.Zero);
        foreach (Vector2 point in points)
            bounds = bounds.Expand(point);
        float size = Math.Max(bounds.Size.X, bounds.Size.Y) * 20f + 10f;
        Vector2 centre = bounds.GetCenter();
        all.Add(centre + new Vector2(-size, -size));
        all.Add(centre + new Vector2(size, -size));
        all.Add(centre + new Vector2(0, size));

        var triangles = new List<(int A, int B, int C)> { (n, n + 1, n + 2) };
        for (int p = 0; p < n; p++)
        {
            Vector2 point = all[p];
            var bad = new List<int>();
            for (int t = 0; t < triangles.Count; t++)
            {
                if (InCircle(all[triangles[t].A], all[triangles[t].B], all[triangles[t].C], point))
                    bad.Add(t);
            }

            // The hole's rim: sides of the removed triangles that only one of them has.
            var edges = new Dictionary<(int, int), int>();
            foreach (int t in bad)
            {
                var (a, b, c) = triangles[t];
                foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
                {
                    var key = u < v ? (u, v) : (v, u);
                    edges[key] = edges.TryGetValue(key, out int seen) ? seen + 1 : 1;
                }
            }
            for (int i = bad.Count - 1; i >= 0; i--)
                triangles.RemoveAt(bad[i]);
            foreach (var ((u, v), count) in edges)
            {
                if (count == 1)
                    triangles.Add(Wound(all, u, v, p));
            }
        }

        var result = new List<int>();
        foreach (var (a, b, c) in triangles)
        {
            if (a < n && b < n && c < n)
                result.AddRange(new[] { a, b, c });
        }
        return result.ToArray();
    }

    /// <summary>A triangle's corners in a fixed winding, so the in-circle test reads the same way for all.</summary>
    private static (int, int, int) Wound(List<Vector2> points, int a, int b, int c) =>
        (points[b] - points[a]).Cross(points[c] - points[a]) > 0f ? (a, b, c) : (a, c, b);

    private static bool InCircle(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
    {
        double ax = a.X - p.X, ay = a.Y - p.Y;
        double bx = b.X - p.X, by = b.Y - p.Y;
        double cx = c.X - p.X, cy = c.Y - p.Y;
        double det = (ax * ax + ay * ay) * (bx * cy - cx * by) -
                     (bx * bx + by * by) * (ax * cy - cx * ay) +
                     (cx * cx + cy * cy) * (ax * by - bx * ay);
        return det > 0;
    }
}
