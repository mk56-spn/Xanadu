using System;
using System.Collections.Generic;
using Xanadu.Core.Geometry;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>
/// A route note laid out on the playfield for a run: from the head, straight
/// stretches at the route's one speed, turning a right angle at each turn the
/// player took (<see cref="RouteRun"/>) and carrying straight on at the rest.
/// Every corner sits at speed × time from the last, so where the route goes is
/// fixed by the chart and the presses, never by how the body is moving. The
/// body chases the point at the song time (<see cref="CurveAttractor"/>).
/// </summary>
public sealed class RoutePath
{
    private readonly Vector2[] _corners;
    private readonly long[] _cornerMs;
    private readonly Vector2[] _headings;

    private RoutePath(Vector2[] corners, long[] cornerMs, Vector2[] headings, float speed, int turns)
    {
        _corners = corners;
        _cornerMs = cornerMs;
        _headings = headings;
        Speed = speed;
        Turns = turns;
    }

    /// <summary>The head, every corner, then the end, on the playfield.</summary>
    public IReadOnlyList<Vector2> Points => _corners;

    public long StartMs => _cornerMs[0];

    /// <summary>When the body leaves the route.</summary>
    public long EndMs => _cornerMs[^1];

    /// <summary>The route's one speed, playfield units a second; also the speed the body settles to after it.</summary>
    public float Speed { get; }

    public float ExitSpeed => Speed;

    /// <summary>How many turns the route offers (every corner but the end).</summary>
    public int Turns { get; }

    /// <summary>The way the last stretch runs; zero for a route with no heading yet.</summary>
    public Vector2 ExitHeading => _headings[^1];

    /// <summary>The song time of point <paramref name="index"/> of <see cref="Points"/> (0 the head).</summary>
    public long TimeAt(int index) => _cornerMs[Math.Clamp(index, 0, _cornerMs.Length - 1)];

    /// <summary>The way stretch <paramref name="index"/> runs (0 from the head to the first corner).</summary>
    public Vector2 HeadingOf(int index) => _headings[Math.Clamp(index, 0, _headings.Length - 1)];

    /// <summary>The route as the editor and a not-yet-hit head show it: heading right, every turn carried straight on.</summary>
    public static RoutePath Of(in Note note) => Of(note, RouteHeading.Right, []);

    /// <summary>The route as a run took it: the head's heading, then the heading pressed at each turn (none, or one not at a right angle, goes straight).</summary>
    public static RoutePath Of(in Note note, RouteHeading start, ReadOnlySpan<RouteHeading> turns)
    {
        if (note.Route is not { } route)
            return new RoutePath([note.Position], [note.TimeMs], [Vector2.Zero], 0f, 0);

        ReadOnlySpan<long> times = route.TimesMs;
        var corners = new Vector2[times.Length + 1];
        var cornerMs = new long[times.Length + 1];
        var headings = new Vector2[times.Length];
        corners[0] = note.Position;
        cornerMs[0] = note.TimeMs;
        RouteHeading heading = start == RouteHeading.None ? RouteHeading.Right : start;
        long last = 0;
        for (int i = 0; i < times.Length; i++)
        {
            if (i > 0 && i - 1 < turns.Length && RouteHeadings.Perpendicular(heading, turns[i - 1]))
                heading = turns[i - 1];
            Vector2 step = RouteHeadings.Step(heading);
            headings[i] = step;
            corners[i + 1] = corners[i] + step * (route.Speed * (times[i] - last) / 1000f);
            cornerMs[i + 1] = note.TimeMs + times[i];
            last = times[i];
        }

        return new RoutePath(corners, cornerMs, headings, route.Speed, route.Turns);
    }

    /// <summary>Whether the song is somewhere on the route.</summary>
    public bool Covers(double timeMs) => timeMs >= StartMs && timeMs <= EndMs;

    /// <summary>
    /// Where the route's point is at a song time, and its velocity there
    /// (playfield units a second); clamped to the ends outside the route.
    /// </summary>
    public (Vector2 Position, Vector2 Velocity) At(double timeMs)
    {
        if (_corners.Length == 1)
            return (_corners[0], Vector2.Zero);
        if (timeMs <= StartMs)
            return (_corners[0], _headings[0] * Speed);
        if (timeMs >= EndMs)
            return (_corners[^1], _headings[^1] * Speed);

        int stretch = 0;
        while (stretch < _headings.Length - 1 && timeMs > _cornerMs[stretch + 1])
            stretch++;
        float seconds = (float)((timeMs - _cornerMs[stretch]) / 1000d);
        return (_corners[stretch] + _headings[stretch] * (Speed * seconds), _headings[stretch] * Speed);
    }

    /// <summary>The stretch the song is on (0 from the head), or -1 outside the route.</summary>
    public int StretchAt(double timeMs)
    {
        if (!Covers(timeMs) || _headings.Length == 0)
            return -1;
        int stretch = 0;
        while (stretch < _headings.Length - 1 && timeMs > _cornerMs[stretch + 1])
            stretch++;
        return stretch;
    }

    /// <summary>How long stretch <paramref name="index"/> is, playfield units.</summary>
    public float LengthOf(int index) =>
        index >= 0 && index < _headings.Length ? Vector2.Distance(_corners[index], _corners[index + 1]) : 0f;

    /// <summary>How close a playfield point comes to the route.</summary>
    public float DistanceTo(Vector2 point) => Splines.DistanceTo(_corners, point);

    /// <summary>The upright box around the route.</summary>
    public Core.Maths.Rect2 Bounds
    {
        get
        {
            var bounds = new Core.Maths.Rect2(_corners[0], Vector2.Zero);
            foreach (Vector2 corner in _corners)
                bounds = bounds.Expand(corner);
            return bounds;
        }
    }
}

/// <summary>
/// How visible a route note is at the playhead: it fades in like a note before
/// its start, stays while the body runs it, and fades out after its end.
/// </summary>
public static class RouteFade
{
    public static float Alpha(RoutePath path, long playheadMs)
    {
        if (playheadMs < path.StartMs)
            return NoteFade.Alpha(path.StartMs, playheadMs);
        if (playheadMs > path.EndMs)
            return NoteFade.Alpha(path.EndMs, playheadMs);
        return 1f;
    }
}
