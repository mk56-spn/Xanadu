using System.Diagnostics;

namespace Xanadu.Systems.Audio;

/// <summary>The monotonic high-resolution timer, in microseconds; the same under any host.</summary>
public sealed class StopwatchTrackClock : ITrackClock
{
    public long NowMicros => Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;
}
