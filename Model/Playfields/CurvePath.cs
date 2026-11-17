using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// A curve note laid out for drawing and for moving along: the smooth path
/// through its points (a Catmull-Rom spline, run as one cubic bezier per span,
/// so it passes through every point the mapper clicked) cut into short steps,
/// each with its length along the path and the song time the body reaches it.
/// Speeds ease linearly along each span's length, so the time at a step is
/// fixed by the chart and never by the run.
/// </summary>
public sealed class CurvePath
{
    /// <summary>Steps per span between two points; enough to read as smooth at editor zoom.</summary>
    public const int StepsPerSpan = 24;

    private readonly Vector2[] _points;
    private readonly float[] _lengths;
    private readonly double[] _timesMs;
    private readonly float[] _speeds;

    private CurvePath(Vector2[] points, float[] lengths, double[] timesMs, float[] speeds, long startMs)
    {
        _points = points;
        _lengths = lengths;
        _timesMs = timesMs;
        _speeds = speeds;
        StartMs = startMs;
    }

    /// <summary>The path's steps, on the playfield, from the first point to the last.</summary>
    public IReadOnlyList<Vector2> Points => _points;

    public long StartMs { get; }

    /// <summary>When the body leaves the last point.</summary>
    public long EndMs => StartMs + (long)Math.Round(_timesMs[^1]);

    public float Length => _lengths[^1];

    /// <summary>A curve note's path, setting off at the note's time; a note without one gives a path that stays on its head.</summary>
    public static CurvePath Of(in Note note) =>
        note.Path is { } path
            ? Of(note.TimeMs, path.Points.ToArray(), path.Speeds.ToArray())
            : Of(note.TimeMs, [note.Position], [NotePath.MinSpeed]);

    public static CurvePath Of(long startMs, IReadOnlyList<Vector2> anchors, IReadOnlyList<float> anchorSpeeds)
    {
        if (anchors is not { Count: > 0 })
            return new CurvePath([Vector2.Zero], [0f], [0d], [NotePath.MinSpeed], startMs);

        int spans = anchors.Count - 1;
        int count = spans * StepsPerSpan + 1;
        var points = new Vector2[count];
        var speeds = new float[count];
        var lengths = new float[count];
        var times = new double[count];
        points[0] = anchors[0];
        speeds[0] = SpeedAt(anchorSpeeds, 0);

        int at = 1;
        for (int span = 0; span < spans; span++)
        {
            Vector2 p0 = anchors[Math.Max(0, span - 1)];
            Vector2 p1 = anchors[span];
            Vector2 p2 = anchors[span + 1];
            Vector2 p3 = anchors[Math.Min(anchors.Count - 1, span + 2)];
            Vector2 c1 = p1 + (p2 - p0) / 6f;
            Vector2 c2 = p2 - (p3 - p1) / 6f;

            // The span's own steps first, so its speed can ease by length along it.
            int first = at;
            for (int step = 1; step <= StepsPerSpan; step++, at++)
            {
                points[at] = Bezier(p1, c1, c2, p2, step / (float)StepsPerSpan);
                lengths[at] = lengths[at - 1] + Vector2.Distance(points[at - 1], points[at]);
            }

            float spanStart = lengths[first - 1];
            float spanLength = lengths[at - 1] - spanStart;
            float from = SpeedAt(anchorSpeeds, span);
            float to = SpeedAt(anchorSpeeds, span + 1);
            for (int i = first; i < at; i++)
            {
                float t = spanLength > 0f ? (lengths[i] - spanStart) / spanLength : 1f;
                speeds[i] = from + (to - from) * t;
                float step = lengths[i] - lengths[i - 1];
                times[i] = times[i - 1] + step / ((speeds[i - 1] + speeds[i]) / 2f) * 1000d;
            }
        }

        return new CurvePath(points, lengths, times, speeds, startMs);
    }

    /// <summary>Whether the song is somewhere on the curve.</summary>
    public bool Covers(double timeMs) => timeMs >= StartMs && timeMs <= EndMs;

    /// <summary>
    /// Where the body is along the curve at a song time, and its velocity there
    /// (playfield units a second); clamped to the ends outside the curve.
    /// </summary>
    public (Vector2 Position, Vector2 Velocity) At(double timeMs)
    {
        double local = timeMs - StartMs;
        if (_points.Length == 1 || local <= 0)
            return (_points[0], Heading(1) * _speeds[0]);
        if (local >= _timesMs[^1])
            return (_points[^1], Heading(_points.Length - 1) * _speeds[^1]);

        int high = Array.BinarySearch(_timesMs, local);
        if (high < 0)
            high = ~high;
        high = Math.Clamp(high, 1, _points.Length - 1);
        int low = high - 1;
        double span = _timesMs[high] - _timesMs[low];
        float t = span > 0 ? (float)((local - _timesMs[low]) / span) : 1f;
        Vector2 position = Vector2.Lerp(_points[low], _points[high], t);
        float speed = _speeds[low] + (_speeds[high] - _speeds[low]) * t;
        return (position, Heading(high) * speed);
    }

    /// <summary>The song time the body reaches each of the curve's own points, for ticks and labels.</summary>
    public long TimeAtAnchor(int anchor) =>
        StartMs + (long)Math.Round(_timesMs[Math.Clamp(anchor * StepsPerSpan, 0, _timesMs.Length - 1)]);

    /// <summary>How close a playfield point comes to the path.</summary>
    public float DistanceTo(Vector2 point)
    {
        if (_points.Length == 1)
            return Vector2.Distance(point, _points[0]);

        float best = float.MaxValue;
        for (int i = 1; i < _points.Length; i++)
            best = MathF.Min(best, SegmentDistance(point, _points[i - 1], _points[i]));
        return best;
    }

    /// <summary>
    /// The point on the path nearest a playfield point, the path's heading
    /// there, the curve's speed there, and whether that point is the path's
    /// last one with the given point already beyond it.
    /// </summary>
    public (Vector2 Position, Vector2 Heading, float Speed, bool PastEnd) Nearest(Vector2 point)
    {
        if (_points.Length == 1)
            return (_points[0], Vector2.Zero, _speeds[0], true);

        float best = float.MaxValue;
        int bestStep = 1;
        float bestT = 0f;
        for (int i = 1; i < _points.Length; i++)
        {
            Vector2 a = _points[i - 1];
            Vector2 ab = _points[i] - a;
            float lengthSquared = ab.LengthSquared();
            float t = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
            float distance = Vector2.DistanceSquared(point, a + ab * t);
            if (distance < best)
            {
                best = distance;
                bestStep = i;
                bestT = t;
            }
        }

        Vector2 at = Vector2.Lerp(_points[bestStep - 1], _points[bestStep], bestT);
        float speed = _speeds[bestStep - 1] + (_speeds[bestStep] - _speeds[bestStep - 1]) * bestT;
        Vector2 heading = Heading(bestStep);
        bool pastEnd = bestStep == _points.Length - 1 && bestT >= 1f && Vector2.Dot(point - at, heading) > 0f;
        return (at, heading, speed, pastEnd);
    }

    private Vector2 Heading(int step)
    {
        Vector2 along = _points[step] - _points[step - 1];
        return along.LengthSquared() > 0f ? Vector2.Normalize(along) : Vector2.Zero;
    }

    private static float SpeedAt(IReadOnlyList<float> speeds, int index)
    {
        if (speeds is not { Count: > 0 })
            return NotePath.MinSpeed;
        float speed = speeds[Math.Clamp(index, 0, speeds.Count - 1)];
        return float.IsFinite(speed) ? Math.Clamp(speed, NotePath.MinSpeed, NotePath.MaxSpeed) : NotePath.MinSpeed;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        float t = lengthSquared > 0f ? Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }
}

/// <summary>
/// How visible a curve note is at the playhead: it fades in like a note before
/// its start, stays while the body runs along it, and fades out after its end.
/// </summary>
public static class CurveFade
{
    public static float Alpha(CurvePath path, long playheadMs)
    {
        if (playheadMs < path.StartMs)
            return NoteFade.Alpha(path.StartMs, playheadMs);
        if (playheadMs > path.EndMs)
            return NoteFade.Alpha(path.EndMs, playheadMs);
        return 1f;
    }
}
