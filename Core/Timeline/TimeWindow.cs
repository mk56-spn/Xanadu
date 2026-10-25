using System;
using Godot;

namespace Writer.Timeline;

/// <summary>
/// The visible span of a timeline, in milliseconds, and its mapping to screen
/// x. Shared by the level editor's ruler and the animator's key timeline.
/// </summary>
public readonly record struct TimeWindow(long Start, long End)
{
    public long Span => Math.Max(1, End - Start);

    /// <summary>
    /// The window for a zoom level, centred on the playhead and kept inside
    /// the timeline. Zoom 1 shows everything.
    /// </summary>
    public static TimeWindow Following(long timeMs, long durationMs, float zoom)
    {
        long duration = Math.Max(1, durationMs);
        long window = Math.Max(1, (long)(duration / Math.Max(1f, zoom)));
        long start = Math.Clamp(
            timeMs - window / 2,
            0,
            Math.Max(0, duration - window));

        return new TimeWindow(start, Math.Min(duration, start + window));
    }

    /// <summary>Screen x of a time, clamped to the ends of <paramref name="area"/>.</summary>
    public float XForTime(Rect2 area, double timeMs)
    {
        float t = Math.Clamp((float)((timeMs - Start) / Span), 0f, 1f);
        return area.Position.X + t * area.Size.X;
    }

    /// <summary>Time under a screen x, clamped to the window.</summary>
    public double TimeForX(Rect2 area, float x)
    {
        float t = Math.Clamp((x - area.Position.X) / Math.Max(1f, area.Size.X), 0f, 1f);
        return Start + t * Span;
    }
}
