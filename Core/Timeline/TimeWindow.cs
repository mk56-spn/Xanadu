using System;
using Godot;

namespace Xanadu.Timeline;

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
        long window = SpanFor(durationMs, zoom);
        return Starting(timeMs - window / 2, durationMs, zoom);
    }

    /// <summary>The window for a zoom level that starts at <paramref name="startMs"/>, kept inside the timeline.</summary>
    public static TimeWindow Starting(long startMs, long durationMs, float zoom)
    {
        long duration = Math.Max(1, durationMs);
        long window = SpanFor(durationMs, zoom);
        long start = Math.Clamp(startMs, 0, Math.Max(0, duration - window));
        return new TimeWindow(start, Math.Min(duration, start + window));
    }

    /// <summary>
    /// What a timeline shows: the window held while scrubbing, else the one a
    /// wheel zoom pinned, else the one centred on the playhead. Every timeline,
    /// whichever tool owns it, draws and hits through this.
    /// </summary>
    public static TimeWindow Of(in Xanadu.Ecs.TimelineView view, long timeMs, long durationMs)
    {
        if (view.Scrubbing)
            return new TimeWindow(view.ScrubStartMs, view.ScrubEndMs);
        return view.Pinned
            ? Starting(view.PinStartMs, durationMs, view.Zoom)
            : Following(timeMs, durationMs, view.Zoom);
    }

    /// <summary>
    /// Where a window zoomed to <paramref name="zoom"/> has to start so the time
    /// now under <paramref name="x"/> stays under it.
    /// </summary>
    public long StartKeeping(Rect2 area, float x, long durationMs, float zoom)
    {
        double under = TimeForX(area, x);
        float t = Math.Clamp((x - area.Position.X) / Math.Max(1f, area.Size.X), 0f, 1f);
        return Starting((long)Math.Round(under - t * SpanFor(durationMs, zoom)), durationMs, zoom).Start;
    }

    public bool Contains(double timeMs) => timeMs >= Start && timeMs <= End;

    private static long SpanFor(long durationMs, float zoom) =>
        Math.Max(1, (long)(Math.Max(1, durationMs) / Math.Max(1f, zoom)));

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
