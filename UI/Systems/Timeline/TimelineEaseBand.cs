using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Timeline;

/// <summary>One filled piece of an ease band; <see cref="Reverse"/> marks where the value runs backwards.</summary>
public readonly record struct EaseBandPiece(Vector2[] Polygon, bool Reverse);

/// <summary>
/// The band drawn across a timeline row for a timed change: between two keys in
/// the animator, across an effect in the level editor's group timeline, so both
/// read the same way. Its thickness follows how fast the value travels: an ease
/// in is a wedge that widens, an ease out one that narrows, a plain ease a lens,
/// linear an even bar. Where the value runs backwards (overshoot, bounce) it
/// comes out as its own pieces. Spans too narrow to read a shape in, or where
/// nothing changes, are a hairline through the row's middle.
/// </summary>
public static class TimelineEaseBand
{
    /// <summary>Space kept above and below the band inside the row.</summary>
    public const float RowPadding = 4f;

    /// <summary>Narrower than this a shape is unreadable, as in a baked run of keys.</summary>
    public const float MinShapeWidth = 12f;

    /// <summary>Half the thickness of a hairline, and of the band where it is still.</summary>
    public const float HairHalf = 0.75f;

    private const int Steps = 48;

    /// <summary>
    /// The band for any curve: <paramref name="progress"/> maps a share of the
    /// span (0 to 1) to how far the value has got.
    /// </summary>
    public static List<EaseBandPiece> Pieces(
        Func<float, float> progress, float fromX, float toX, float left, float right, Rect2 row)
    {
        float middle = row.Position.Y + row.Size.Y / 2f;
        float maxHalf = Mathf.Max(row.Size.Y / 2f - RowPadding, HairHalf);
        var pieces = new List<EaseBandPiece>();
        float start = Mathf.Max(fromX, left);
        float end = Mathf.Min(toX, right);
        if (end <= start)
            return pieces;

        if (toX - fromX < MinShapeWidth)
        {
            pieces.Add(new(Bar(start, end, middle, HairHalf), false));
            return pieces;
        }

        // Scaled by the whole span's peak, so scrolling part of it off does not rescale what shows.
        float peak = 0f;
        for (int step = 0; step <= Steps; step++)
            peak = Mathf.Max(peak, Mathf.Abs(Speed(progress, step / (float)Steps)));
        if (peak <= 0f)
        {
            pieces.Add(new(Bar(start, end, middle, HairHalf), false));
            return pieces;
        }

        var top = new List<Vector2>();
        var bottom = new List<Vector2>();
        bool reverse = false;
        float width = toX - fromX;
        foreach (float x in SampleXs(fromX, width, start, end))
        {
            float speed = Speed(progress, (x - fromX) / width);
            bool backwards = speed < 0f;
            if (top.Count > 0 && backwards != reverse)
            {
                // The direction turns: pinch this piece shut and open the next at the same x.
                top.Add(new Vector2(x, middle - HairHalf));
                bottom.Add(new Vector2(x, middle + HairHalf));
                pieces.Add(new(Close(top, bottom), reverse));
                top.Clear();
                bottom.Clear();
                top.Add(new Vector2(x, middle - HairHalf));
                bottom.Add(new Vector2(x, middle + HairHalf));
            }
            reverse = backwards;
            float half = Mathf.Max(HairHalf, maxHalf * Mathf.Abs(speed) / peak);
            top.Add(new Vector2(x, middle - half));
            bottom.Add(new Vector2(x, middle + half));
        }
        if (top.Count >= 2)
            pieces.Add(new(Close(top, bottom), reverse));
        return pieces;
    }

    /// <summary>How fast a curve moves at a share of the span, 1 being linear.</summary>
    public static float Speed(Func<float, float> progress, float t)
    {
        const float h = 0.5f / Steps;
        float a = Mathf.Max(t - h, 0f);
        float b = Mathf.Min(t + h, 1f);
        return (progress(b) - progress(a)) / (b - a);
    }

    /// <summary>The visible ends, and every sample of the whole span between them.</summary>
    private static IEnumerable<float> SampleXs(float fromX, float width, float start, float end)
    {
        yield return start;
        for (int step = 1; step < Steps; step++)
        {
            float x = fromX + width * step / Steps;
            if (x > start && x < end)
                yield return x;
        }
        yield return end;
    }

    public static Vector2[] Bar(float from, float to, float middle, float half) => new[]
    {
        new Vector2(from, middle - half), new Vector2(to, middle - half),
        new Vector2(to, middle + half), new Vector2(from, middle + half),
    };

    private static Vector2[] Close(List<Vector2> top, List<Vector2> bottom)
    {
        var polygon = new Vector2[top.Count + bottom.Count];
        top.CopyTo(polygon);
        for (int i = 0; i < bottom.Count; i++)
            polygon[top.Count + i] = bottom[bottom.Count - 1 - i];
        return polygon;
    }
}
