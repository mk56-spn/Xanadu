using System;

namespace Xanadu.Timeline;

/// <summary>Writes times for the timeline and HUD readouts.</summary>
public static class TimeFormat
{
    /// <summary>A time as <c>mm:ss.fff</c>; anything before zero reads as zero.</summary>
    public static string Clock(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }
}
