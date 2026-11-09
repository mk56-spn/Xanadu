using System;
using Godot;

namespace Xanadu.Timeline;

/// <summary>
/// Where time runs on screen: a window of time (in milliseconds) laid across
/// an area. Every timeline maps through one of these, the note timeline, the
/// group panel's and the animator's, so they place things the same way.
/// <see cref="X"/> is not clamped, so a span that starts or ends off screen
/// keeps its true shape and is cut at <see cref="Left"/> and <see cref="Right"/>.
/// </summary>
public readonly record struct TimelineAxis(Rect2 Area, double StartMs, double EndMs)
{
    public static TimelineAxis Of(Rect2 area, TimeWindow window) => new(area, window.Start, window.End);

    /// <summary>An axis from a start and a span in seconds, as the group panel keeps its view.</summary>
    public static TimelineAxis FromSeconds(Rect2 area, double startSeconds, double spanSeconds) =>
        new(area, startSeconds * 1000.0, (startSeconds + spanSeconds) * 1000.0);

    public double SpanMs => Math.Max(1e-6, EndMs - StartMs);

    public float Left => Area.Position.X;
    public float Right => Area.End.X;

    public double PixelsPerSecond => Area.Size.X / (SpanMs / 1000.0);

    /// <summary>Screen x of a time; off the area's ends for times outside the window.</summary>
    public float X(double ms) => Area.Position.X + (float)((ms - StartMs) / SpanMs) * Area.Size.X;

    /// <summary>Screen x of a time, held to the area's ends.</summary>
    public float ClampedX(double ms) => Math.Clamp(X(ms), Left, Right);

    /// <summary>The time under a screen x; outside the window off the area's ends.</summary>
    public double TimeAt(float x) => StartMs + (x - Area.Position.X) / Math.Max(1f, Area.Size.X) * SpanMs;

    /// <summary>Whether a time is in the window, with a little slack for times that round onto its ends.</summary>
    public bool Shows(double ms, double slackMs = 1.0) => ms >= StartMs - slackMs && ms <= EndMs + slackMs;
}
