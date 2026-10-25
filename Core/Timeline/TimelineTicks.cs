using System;

namespace Writer.Timeline;

/// <summary>Picks readable tick spacing for a time ruler.</summary>
public static class TimelineTicks
{
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
}
