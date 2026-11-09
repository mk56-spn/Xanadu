using System;
using System.Collections.Generic;
using System.Globalization;

namespace Xanadu.Timeline;

/// <summary>How much a tick marks: a beat or labelled second, half or a quarter of one, or a finer step.</summary>
public enum TimelineTickLevel
{
    Major,
    Half,
    Quarter,
    Minor,
}

/// <summary>One mark on a timeline's grid, with the label a ruler shows at it, if any.</summary>
public readonly record struct TimelineTick(double TimeMs, TimelineTickLevel Level, string? Label = null);

/// <summary>Readable tick spacing for a time ruler, and the plain-seconds grid built from it.</summary>
public static class TimelineTicks
{
    /// <summary>More ticks than this in a window and the grid is not drawn.</summary>
    public const int MaxTicks = 4000;

    private static readonly double[] NiceSeconds =
    {
        0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60,
    };

    /// <summary>
    /// The smallest round step, in seconds, that is at least
    /// <paramref name="minPixels"/> wide at the given scale.
    /// </summary>
    public static double NiceStepSeconds(double pixelsPerSecond, double minPixels)
    {
        foreach (double step in NiceSeconds)
        {
            if (step * Math.Max(0.0001, pixelsPerSecond) >= minPixels)
                return step;
        }

        return NiceSeconds[^1];
    }

    /// <summary>
    /// A grid in plain seconds, for timelines with no tempo (the animator): a
    /// minor tick every round step at least <paramref name="minTickPixels"/>
    /// apart, and a labelled major one every step at least
    /// <paramref name="minLabelPixels"/> apart.
    /// </summary>
    public static List<TimelineTick> Seconds(
        TimelineAxis axis,
        double minLabelPixels,
        double minTickPixels,
        string suffix = "")
    {
        var ticks = new List<TimelineTick>();
        double labelStep = NiceStepSeconds(axis.PixelsPerSecond, minLabelPixels);
        double tickStep = NiceStepSeconds(axis.PixelsPerSecond, minTickPixels);
        double tickMs = tickStep * 1000.0;
        long first = (long)Math.Ceiling(axis.StartMs / tickMs - 1e-9);
        long last = (long)Math.Floor(axis.EndMs / tickMs + 1e-9);
        if (last - first > MaxTicks)
            return ticks;

        for (long tick = first; tick <= last; tick++)
        {
            double seconds = tick * tickStep;
            double ratio = seconds / labelStep;
            bool major = Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
            ticks.Add(new TimelineTick(
                seconds * 1000.0,
                major ? TimelineTickLevel.Major : TimelineTickLevel.Minor,
                major ? seconds.ToString("0.###", CultureInfo.InvariantCulture) + suffix : null));
        }

        return ticks;
    }
}
