using System;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// A box turned about its own centre, the way spikes, text, grass and polygons
/// turn: the geometry the editor picks, outlines and resizes them by.
/// </summary>
public static class TurnedBox
{
    /// <summary>The four corners, clockwise from the top left, turned by <paramref name="degrees"/>.</summary>
    public static Vector2[] Corners(Rect2 box, float degrees)
    {
        Vector2 center = box.GetCenter();
        float angle = Mathf.DegToRad(degrees);
        Vector2 half = box.Size / 2;
        return new[]
        {
            center + new Vector2(-half.X, -half.Y).Rotated(angle),
            center + new Vector2(half.X, -half.Y).Rotated(angle),
            center + new Vector2(half.X, half.Y).Rotated(angle),
            center + new Vector2(-half.X, half.Y).Rotated(angle),
        };
    }

    /// <summary>Whether a point lies inside the turned box.</summary>
    public static bool Contains(Rect2 box, float degrees, Vector2 point)
    {
        Vector2 center = box.GetCenter();
        Vector2 local = center + (point - center).Rotated(-Mathf.DegToRad(degrees));
        return box.HasPoint(local);
    }

    /// <summary>The smallest upright box around the turned one.</summary>
    public static Rect2 Hull(Rect2 box, float degrees)
    {
        if (degrees == 0f)
            return box;

        Vector2[] corners = Corners(box, degrees);
        var hull = new Rect2(corners[0], Vector2.Zero);
        for (int i = 1; i < corners.Length; i++)
            hull = hull.Expand(corners[i]);
        return hull;
    }

    /// <summary>An angle kept in 0 to 360 degrees.</summary>
    public static float Normalize(float degrees) => float.IsFinite(degrees) ? Mathf.PosMod(degrees, 360f) : 0f;

    /// <summary>The angle after mirroring: left-right negates it, up-down turns it the other way about the horizontal.</summary>
    public static float Flipped(float degrees, bool horizontal) => Normalize(horizontal ? -degrees : 180f - degrees);

    /// <summary>Whether a point lies inside a closed outline, by counting edge crossings.</summary>
    public static bool InsideOutline(ReadOnlySpan<Vector2> outline, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            Vector2 a = outline[i];
            Vector2 b = outline[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
