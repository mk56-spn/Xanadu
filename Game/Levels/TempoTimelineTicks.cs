using System;
using System.Collections.Generic;
using Xanadu.Timeline;

namespace Xanadu.Levels;

/// <summary>
/// A song timeline's grid from the tempo map. How much of it shows depends
/// only on the zoom, never on the snap: beats always (thinned when zoomed far
/// out), then halves, quarters, eighths and sixteenths of a beat as each gets
/// room. The note timeline's ruler and the group panel's lines both draw from
/// it, so they agree on where the beats are.
/// </summary>
public static class TempoTimelineTicks
{
    /// <summary>The least room in pixels between two drawn beats.</summary>
    public const float MinBeatSpacing = 16f;

    /// <summary>The least room in pixels between two drawn lines inside a beat.</summary>
    public const float MinSubSpacing = 10f;

    /// <summary>The finest split of a beat ever drawn.</summary>
    public const int MaxDivision = 16;

    /// <summary>The ticks in the axis's window.</summary>
    public static List<TimelineTick> For(TimelineAxis axis, TempoMap tempo)
    {
        var ticks = new List<TimelineTick>();
        double firstBeat = tempo.BeatAt(axis.StartMs);
        double lastBeat = tempo.BeatAt(axis.EndMs);
        double beats = Math.Max(1e-6, lastBeat - firstBeat);

        // The window's average beat, for choosing what fits; the ticks themselves follow the tempo map.
        float pixelsPerBeat = axis.Area.Size.X / (float)beats;

        // A timeline with no width (hidden, or not laid out yet) has nothing to draw.
        if (!(pixelsPerBeat > 0f) || float.IsInfinity(pixelsPerBeat))
            return ticks;

        int division = Division(pixelsPerBeat);
        int beatSkip = 1;
        while (pixelsPerBeat * beatSkip < MinBeatSpacing)
            beatSkip *= 2;

        long firstTick = (long)Math.Floor(firstBeat * division);
        long lastTick = (long)Math.Ceiling(lastBeat * division);
        if (lastTick - firstTick > TimelineTicks.MaxTicks * 4L)
            return ticks;

        for (long tick = firstTick; tick <= lastTick; tick++)
        {
            long sub = ((tick % division) + division) % division;
            if (sub == 0 && FloorDiv(tick, division) % beatSkip != 0)
                continue;

            double time = tempo.TimeAt(tick / (double)division);
            if (axis.Shows(time))
                ticks.Add(new TimelineTick(time, Level(sub, division)));
        }

        return ticks;
    }

    /// <summary>The finest power-of-two split of a beat whose lines are at least <see cref="MinSubSpacing"/> apart.</summary>
    public static int Division(float pixelsPerBeat)
    {
        int division = 1;
        while (division < MaxDivision && pixelsPerBeat / (division * 2) >= MinSubSpacing)
            division *= 2;
        return division;
    }

    /// <summary>A beat is major, then a half, the quarters, and every finer line minor.</summary>
    private static TimelineTickLevel Level(long sub, int division)
    {
        if (sub == 0)
            return TimelineTickLevel.Major;
        if (sub * 2 == division)
            return TimelineTickLevel.Half;
        if (sub * 4 == division || sub * 4 == division * 3)
            return TimelineTickLevel.Quarter;
        return TimelineTickLevel.Minor;
    }

    private static long FloorDiv(long value, long divisor) => (long)Math.Floor(value / (double)divisor);
}
