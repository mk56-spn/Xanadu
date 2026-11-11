using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// Automatic skin weights by heat diffusion (Baran and Popović, "Automatic
/// Rigging and Animation of 3D Characters", 2007, the method behind Blender's
/// automatic weights), on a flat mesh. Each bone heats the points it is the
/// nearest visible bone to, every point loses heat in proportion to how far it
/// is from its nearest bone, and heat spreads over the mesh; a bone's weight at
/// a point is the temperature there once it settles. Heat only travels through
/// the mesh, so a point is never pulled by a bone across a gap in the art, and
/// a joint's blend falls where the heat from either side meets.
/// </summary>
public static class XafHeatWeights
{
    /// <summary>The heat a point's nearest bone gives it, over its distance squared: the paper's c.</summary>
    private const double Heat = 1.0;

    private const int MaxIterations = 4000;
    private const double Tolerance = 1e-7;

    /// <summary>
    /// Each point's weights over <paramref name="bones"/>, one row per point. A
    /// bone is visible from a point when the straight line to its nearest spot
    /// on the bone stays inside the art, as <paramref name="inside"/> says.
    /// </summary>
    public static float[][] Compute(Vector2[] points, int[] triangles, IReadOnlyList<(Vector2 From, Vector2 To)> bones,
        Func<Vector2, bool> inside)
    {
        int n = points.Length;
        var weights = new float[n][];
        for (int i = 0; i < n; i++)
            weights[i] = new float[bones.Count];
        if (n == 0 || bones.Count == 0)
            return weights;

        (List<(int, double)>[] laplacian, double[] area) = Laplacian(points, triangles);

        // Each point's nearest visible bones, and its distance from the nearest.
        var reach = new double[n];
        var nearest = new List<int>[n];
        for (int p = 0; p < n; p++)
        {
            nearest[p] = new List<int>();
            double best = double.MaxValue;
            for (int b = 0; b < bones.Count; b++)
            {
                Vector2 spot = Closest(bones[b].From, bones[b].To, points[p]);
                if (!Visible(points[p], spot, inside))
                    continue;
                double distance = Math.Max(points[p].DistanceTo(spot), 1e-3);
                if (distance < best - 1e-6)
                {
                    best = distance;
                    nearest[p].Clear();
                    nearest[p].Add(b);
                }
                else if (Math.Abs(distance - best) <= 1e-6)
                {
                    nearest[p].Add(b);
                }
            }
            reach[p] = nearest[p].Count > 0 ? best : double.PositiveInfinity;
        }

        // The paper's points are all on a surface, a limb's width from its bone;
        // a flat mesh also has points along the bone itself, which would be
        // pinned to it and crease the middle of every joint. No point counts as
        // nearer its bone than the limb's half width, the outline's typical
        // distance from its bones, so the blend spreads across the limb.
        double halfWidth = HalfWidth(triangles, reach);
        var heat = new double[n];
        for (int p = 0; p < n; p++)
        {
            double distance = Math.Max(reach[p], halfWidth);
            heat[p] = double.IsPositiveInfinity(reach[p]) ? 0.0 : Heat / (distance * distance);
        }

        // (L + A H) w = A H p for each bone, with L the cotangent Laplacian and A each point's share of area.
        var diagonal = new double[n];
        for (int p = 0; p < n; p++)
            diagonal[p] = area[p] * heat[p];

        for (int b = 0; b < bones.Count; b++)
        {
            var right = new double[n];
            for (int p = 0; p < n; p++)
            {
                if (nearest[p].Contains(b))
                    right[p] = diagonal[p] / nearest[p].Count;
            }
            double[] solved = Solve(laplacian, diagonal, right);
            for (int p = 0; p < n; p++)
                weights[p][b] = (float)Math.Clamp(solved[p], 0.0, 1.0);
        }

        foreach (float[] row in weights)
        {
            XafSkinMesh.Tidy(row);
            float total = 0f;
            foreach (float weight in row)
                total += weight;
            if (total <= 0f)
                row[0] = 1f;
        }
        return weights;
    }

    /// <summary>The middle distance from their bones of the points on the mesh's outline: those on a side only one triangle has.</summary>
    private static double HalfWidth(int[] triangles, double[] reach)
    {
        var sides = new Dictionary<(int, int), int>();
        for (int t = 0; t < triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = triangles[t + k], b = triangles[t + (k + 1) % 3];
                var key = a < b ? (a, b) : (b, a);
                sides[key] = sides.TryGetValue(key, out int seen) ? seen + 1 : 1;
            }
        }

        var edge = new HashSet<int>();
        foreach (var ((a, b), count) in sides)
        {
            if (count == 1)
            {
                edge.Add(a);
                edge.Add(b);
            }
        }

        var distances = new List<double>();
        foreach (int p in edge)
        {
            if (!double.IsPositiveInfinity(reach[p]))
                distances.Add(reach[p]);
        }
        if (distances.Count == 0)
            return 1e-3;
        distances.Sort();
        return Math.Max(distances[distances.Count / 2], 1e-3);
    }

    /// <summary>The cotangent Laplacian, row by row, and each point's third of its triangles' area.</summary>
    private static (List<(int, double)>[], double[]) Laplacian(Vector2[] points, int[] triangles)
    {
        int n = points.Length;
        var entries = new Dictionary<(int, int), double>();
        var area = new double[n];
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int[] corner = { triangles[t], triangles[t + 1], triangles[t + 2] };
            double triangleArea = Math.Abs((double)(points[corner[1]] - points[corner[0]]).Cross(points[corner[2]] - points[corner[0]])) / 2;
            if (triangleArea < 1e-9)
                continue;
            for (int k = 0; k < 3; k++)
            {
                area[corner[k]] += triangleArea / 3;

                // The side opposite this corner is weighted by half the cotangent of its angle.
                int i = corner[(k + 1) % 3], j = corner[(k + 2) % 3];
                Vector2 u = points[i] - points[corner[k]], v = points[j] - points[corner[k]];
                double cot = u.Dot(v) / Math.Max(Math.Abs((double)u.Cross(v)), 1e-12);
                double w = cot / 2;
                Add(entries, i, j, -w);
                Add(entries, j, i, -w);
                Add(entries, i, i, w);
                Add(entries, j, j, w);
            }
        }

        var rows = new List<(int, double)>[n];
        for (int i = 0; i < n; i++)
            rows[i] = new List<(int, double)>();
        foreach (var ((i, j), value) in entries)
            rows[i].Add((j, value));
        return (rows, area);
    }

    private static void Add(Dictionary<(int, int), double> entries, int i, int j, double value) =>
        entries[(i, j)] = entries.TryGetValue((i, j), out double old) ? old + value : value;

    /// <summary>Conjugate gradients, Jacobi preconditioned, for (L + diag) x = b.</summary>
    private static double[] Solve(List<(int, double)>[] laplacian, double[] diagonal, double[] b)
    {
        int n = b.Length;
        var x = new double[n];
        var r = (double[])b.Clone();
        var precondition = new double[n];
        for (int i = 0; i < n; i++)
        {
            double d = diagonal[i];
            foreach (var (j, value) in laplacian[i])
            {
                if (j == i)
                    d += value;
            }
            precondition[i] = d > 1e-12 ? 1 / d : 1;
        }

        var z = new double[n];
        for (int i = 0; i < n; i++)
            z[i] = precondition[i] * r[i];
        var p = (double[])z.Clone();
        double rz = Dot(r, z);
        double target = Tolerance * Math.Max(Dot(b, b), 1e-30);
        var ap = new double[n];
        for (int iteration = 0; iteration < MaxIterations && Dot(r, r) > target; iteration++)
        {
            for (int i = 0; i < n; i++)
            {
                double sum = diagonal[i] * p[i];
                foreach (var (j, value) in laplacian[i])
                    sum += value * p[j];
                ap[i] = sum;
            }
            double step = rz / Math.Max(Dot(p, ap), 1e-30);
            for (int i = 0; i < n; i++)
            {
                x[i] += step * p[i];
                r[i] -= step * ap[i];
                z[i] = precondition[i] * r[i];
            }
            double next = Dot(r, z);
            double keep = next / Math.Max(rz, 1e-30);
            rz = next;
            for (int i = 0; i < n; i++)
                p[i] = z[i] + keep * p[i];
        }
        return x;
    }

    private static double Dot(double[] a, double[] b)
    {
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    private static Vector2 Closest(Vector2 from, Vector2 to, Vector2 point)
    {
        Vector2 along = to - from;
        float length = along.LengthSquared();
        if (length < 1e-8f)
            return from;
        float t = Math.Clamp((point - from).Dot(along) / length, 0f, 1f);
        return from + along * t;
    }

    /// <summary>Whether the straight line between two spots stays inside the art, checked along it.</summary>
    private static bool Visible(Vector2 from, Vector2 to, Func<Vector2, bool> inside)
    {
        const int Samples = 12;
        for (int i = 1; i < Samples; i++)
        {
            Vector2 spot = from.Lerp(to, i / (float)Samples);
            // The last stretch before the bone may leave the art: a bone on its
            // very edge, as the hips are over the top of a leg, still counts.
            if (spot.DistanceTo(to) > EdgeSlack && !inside(spot))
                return false;
        }
        return true;
    }

    /// <summary>How far from a bone, in rig units, the line to it may run outside the art.</summary>
    private const float EdgeSlack = 6f;
}
