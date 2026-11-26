using System.Diagnostics;

namespace Xanadu.Rendering;

/// <summary>
/// Times an exe's tick in named stretches for the F3 frame breakdown: each <see cref="Lap"/>
/// charges the time since the last one (or <see cref="Start"/>) to its part under "exe tick"
/// (<see cref="HostFramePhases.AddTick"/>). What a tick doesn't lap stays in "exe tick" alone.
/// </summary>
internal struct TickLaps
{
    private long _since;

    public static TickLaps Start() => new() { _since = Stopwatch.GetTimestamp() };

    public void Lap(string part)
    {
        long now = Stopwatch.GetTimestamp();
        HostFramePhases.Current?.AddTick(part, Stopwatch.GetElapsedTime(_since, now).TotalMilliseconds);
        _since = now;
    }
}
