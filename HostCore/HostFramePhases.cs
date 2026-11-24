using System.Collections.Generic;
using System.Diagnostics;
using Xanadu.Ui.Systems.Debug;

namespace Xanadu.Host;

/// <summary>
/// Times every part of the host's frame for the F3 overlay, always on (a
/// timestamp read per part). Parts add up their milliseconds; the overlay
/// averages them over its windows. What is left of the frame's wall time
/// after our update and draw is MoonWorks: its pacing sleep and event pumping.
/// </summary>
internal sealed class HostFramePhases : IFramePhaseSource
{
    public enum Phase
    {
        Update,
        Input,
        UiAndSystems,
        ExeTick,
        Draw,
        AcquireCommands,
        WaitSwapchain,
        AtlasFlush,
        Offscreen,
        Canvas,
        Collect,
        CollectWalk,
        CollectCanvas,
        CollectUiRestructure,
        CollectUiScan,
        CollectOtherMounts,
        CollectSort,
        Build,
        Upload,
        ShaderStates,
        DrawPasses,
        Submit,
        Outside,
        Count,
    }

    private static readonly FramePhase[] List =
    [
        new("update", 0),
        new("input, window, pointer", 1),
        new("ui + systems (F3 tree)", 1),
        new("exe tick", 1),
        new("draw", 0),
        new("acquire command buffer", 1),
        new("wait for swapchain (vsync)", 1),
        new("atlas flush, gpu poll", 1),
        new("offscreen stages", 1),
        new("canvas", 1),
        new("collect items", 2),
        new("walk tree, place items", 3),
        new("canvas items: place, children", 4),
        new("ui: restructure", 4),
        new("ui: scan values", 4),
        new("other mounts", 4),
        new("sort entries by z", 3),
        new("build vertices", 2),
        new("upload", 2),
        new("shader states, particles", 2),
        new("record draw passes", 2),
        new("blit, submit, shot", 1),
        new("outside our code (pacing, events)", 0),
    ];

    private readonly double[] _totals = new double[(int)Phase.Count];
    private long _lastFrameEnd = Stopwatch.GetTimestamp();
    private double _frameOurs;

    public IReadOnlyList<FramePhase> Phases => List;

    public long Frames { get; private set; }

    public double TotalMs(int phase) => _totals[phase];

    public static long Now => Stopwatch.GetTimestamp();

    /// <summary>Adds the time since <paramref name="since"/> to a part, and moves <paramref name="since"/> on to now, so parts can be timed one after another.</summary>
    public void Lap(Phase phase, ref long since)
    {
        long now = Stopwatch.GetTimestamp();
        Add(phase, Stopwatch.GetElapsedTime(since, now).TotalMilliseconds);
        since = now;
    }

    public void Add(Phase phase, double ms)
    {
        _totals[(int)phase] += ms;
        if (phase is Phase.Update or Phase.Draw)
            _frameOurs += ms;
    }

    /// <summary>Closes a drawn frame: whatever of its wall time our update and draw did not use was spent outside our code.</summary>
    public void EndFrame()
    {
        long now = Stopwatch.GetTimestamp();
        double wall = Stopwatch.GetElapsedTime(_lastFrameEnd, now).TotalMilliseconds;
        _lastFrameEnd = now;
        _totals[(int)Phase.Outside] += System.Math.Max(0, wall - _frameOurs);
        _frameOurs = 0;
        Frames++;
    }
}
