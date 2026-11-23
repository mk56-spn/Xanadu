using System.Collections.Generic;

namespace Xanadu.Core.Geometry;

/// <summary>
/// Smooth lines through points, for drawn lines (decoration and the like): a
/// Catmull-Rom spline run as one cubic bezier per span, so it passes through
/// every point given, cut into short straight steps.
/// </summary>
public static class Splines
{
    /// <summary>
    /// The smooth line through <paramref name="anchors"/>, as
    /// <paramref name="stepsPerSpan"/> straight steps between each pair; the
    /// first and last points are the first and last anchors.
    /// </summary>
    public static Vector2[] CatmullRom(IReadOnlyList<Vector2> anchors, int stepsPerSpan = 24)
    {
        if (anchors is not { Count: > 0 })
            return [];
        if (anchors.Count == 1)
            return [anchors[0]];

        stepsPerSpan = Math.Max(1, stepsPerSpan);
        int spans = anchors.Count - 1;
        var points = new Vector2[spans * stepsPerSpan + 1];
        points[0] = anchors[0];
        int at = 1;
        for (int span = 0; span < spans; span++)
        {
            Vector2 p0 = anchors[Math.Max(0, span - 1)];
            Vector2 p1 = anchors[span];
            Vector2 p2 = anchors[span + 1];
            Vector2 p3 = anchors[Math.Min(anchors.Count - 1, span + 2)];
            Vector2 c1 = p1 + (p2 - p0) / 6f;
            Vector2 c2 = p2 - (p3 - p1) / 6f;
            for (int step = 1; step <= stepsPerSpan; step++, at++)
                points[at] = Bezier(p1, c1, c2, p2, step / (float)stepsPerSpan);
        }

        return points;
    }

    /// <summary>A cubic bezier from <paramref name="a"/> to <paramref name="d"/> with controls <paramref name="b"/> and <paramref name="c"/>, at <paramref name="t"/> (0 to 1).</summary>
    public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    /// <summary>How close a point comes to a line of straight steps; to the point itself when the line is one point.</summary>
    public static float DistanceTo(IReadOnlyList<Vector2> line, Vector2 point)
    {
        if (line.Count == 0)
            return float.MaxValue;
        if (line.Count == 1)
            return Vector2.Distance(point, line[0]);

        float best = float.MaxValue;
        for (int i = 1; i < line.Count; i++)
            best = MathF.Min(best, SegmentDistance(point, line[i - 1], line[i]));
        return best;
    }

    /// <summary>How close a point comes to the segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }
}
