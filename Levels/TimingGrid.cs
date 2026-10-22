using System;

namespace Writer.Levels;

/// <summary>Shared beat/subdivision math for editor and timeline rendering.</summary>
public static class TimingGrid
{
    public static double BeatMs(int bpm) => 60_000.0 / Math.Max(1, bpm);

    public static double TickMs(int bpm, int subdivisions) =>
        BeatMs(bpm) / Math.Max(1, subdivisions);

    public static int FirstTick(long windowStart, long offsetMs, double tickMs) =>
        (int)Math.Floor((windowStart - offsetMs) / tickMs);

    public static int LastTick(long windowEnd, long offsetMs, double tickMs) =>
        (int)Math.Ceiling((windowEnd - offsetMs) / tickMs);

    public static double TickTime(int tick, long offsetMs, double tickMs) =>
        offsetMs + tick * tickMs;
}
