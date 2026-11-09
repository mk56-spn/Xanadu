using System;
using System.Collections.Generic;

namespace Xanadu.Timeline;

/// <summary>Which part of a span a press takes: its body moves it whole, an end retimes just that end.</summary>
public enum TimelineGrabPart
{
    Body,
    Start,
    End,
}

/// <summary>
/// Picking things on a timeline the same way in every tool: the nearest point
/// (a key, a pin, an event) within reach of a press, and a span's body or one
/// of its ends (an animator override, a group effect).
/// </summary>
public static class TimelineGrab
{
    /// <summary>The nearest of <paramref name="timesMs"/> within <paramref name="radius"/> pixels of <paramref name="x"/>, or -1.</summary>
    public static int Nearest(TimelineAxis axis, IReadOnlyList<double> timesMs, float x, float radius)
    {
        int best = -1;
        float closest = radius;
        for (int i = 0; i < timesMs.Count; i++)
        {
            float distance = Math.Abs(x - axis.X(timesMs[i]));
            if (distance <= closest)
            {
                closest = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// The part of a span under <paramref name="x"/>, or null when the press
    /// misses it. Anything within <paramref name="grip"/> pixels of the span
    /// counts; within that of an end takes the nearer end, unless the span is
    /// narrower than <paramref name="minGripWidth"/>, when it is only moved whole.
    /// </summary>
    public static TimelineGrabPart? Span(
        TimelineAxis axis,
        double fromMs,
        double toMs,
        float x,
        float grip,
        float minGripWidth = 0f)
    {
        float left = axis.X(fromMs);
        float right = axis.X(toMs);
        if (x < left - grip || x > right + grip)
            return null;

        float toLeft = Math.Abs(x - left);
        float toRight = Math.Abs(x - right);
        if (right - left < minGripWidth || Math.Min(toLeft, toRight) > grip)
            return TimelineGrabPart.Body;
        return toLeft <= toRight ? TimelineGrabPart.Start : TimelineGrabPart.End;
    }
}

/// <summary>
/// A span being dragged: which part was taken, where the span was and where in
/// time the press landed. Every move is measured from these, so the span never
/// drifts from the pointer.
/// </summary>
public readonly record struct TimelineSpanDrag(TimelineGrabPart Part, double FromMs, double ToMs, double GrabbedAtMs)
{
    /// <summary>
    /// The span with the taken part moved to the pointer's time, snapped by
    /// <paramref name="snap"/>, kept between <paramref name="lowMs"/> and
    /// <paramref name="highMs"/> and at least <paramref name="minLengthMs"/>
    /// long. A body keeps its length.
    /// </summary>
    public (double From, double To) Apply(
        double pointerMs,
        Func<double, double> snap,
        double lowMs = 0,
        double highMs = double.MaxValue,
        double minLengthMs = 0)
    {
        switch (Part)
        {
            case TimelineGrabPart.Start:
                return (Math.Clamp(snap(pointerMs), lowMs, Math.Max(lowMs, ToMs - minLengthMs)), ToMs);
            case TimelineGrabPart.End:
                return (FromMs, Math.Clamp(snap(pointerMs), Math.Min(highMs, FromMs + minLengthMs), highMs));
            default:
                double length = ToMs - FromMs;
                double from = Math.Clamp(snap(FromMs + pointerMs - GrabbedAtMs), lowMs, Math.Max(lowMs, highMs - length));
                return (from, from + length);
        }
    }
}
