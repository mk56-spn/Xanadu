using System.Collections.Generic;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The line drawn between two keys on a timeline row. It climbs the row in the
/// shape of the easing, always left to right and bottom to top, so it shows how
/// the value travels rather than which way or how far. Spans where nothing changes
/// run flat through the row's middle, as do spans too narrow to read a curve in
/// and spans whose channels ease differently, which have no one shape to show.
/// </summary>
public static class AnimatorEaseLine
{
    /// <summary>Space kept above and below the curve inside the row.</summary>
    public const float RowPadding = 4f;

    /// <summary>Narrower than this a curve is unreadable, as in a baked run of keys.</summary>
    public const float MinCurveWidth = 12f;

    private const int Steps = 16;

    /// <param name="left">Where the visible timeline starts; nothing is drawn past it.</param>
    /// <param name="right">Where the visible timeline ends.</param>
    public static Vector2[] Points(XafEaseSegment segment, float fromX, float toX, float left, float right, Rect2 row)
    {
        float bottom = row.End.Y - RowPadding;
        float top = row.Position.Y + RowPadding;
        float middle = row.Position.Y + row.Size.Y / 2f;
        var line = new List<Vector2>();

        if (segment.Preset is not { } preset || toX - fromX < MinCurveWidth)
        {
            float start = Mathf.Max(fromX, left);
            float end = Mathf.Min(toX, right);
            return end > start ? new[] { new Vector2(start, middle), new Vector2(end, middle) } : new Vector2[0];
        }

        if (preset == XafEasePreset.Step)
        {
            // Holds the old value right up to the next key, then jumps.
            Add(line, new Vector2(fromX, bottom), left, right);
            Add(line, new Vector2(toX, bottom), left, right);
            Add(line, new Vector2(toX, top), left, right);
            return line.ToArray();
        }

        for (int step = 0; step <= Steps; step++)
        {
            float t = step / (float)Steps;
            float rise = XafEasing.Progress(preset, t);
            Add(line, new Vector2(Mathf.Lerp(fromX, toX, t), Mathf.Lerp(bottom, top, rise)), left, right);
        }
        return line.ToArray();
    }

    private static void Add(List<Vector2> line, Vector2 point, float left, float right)
    {
        if (point.X >= left && point.X <= right)
            line.Add(point);
    }
}
