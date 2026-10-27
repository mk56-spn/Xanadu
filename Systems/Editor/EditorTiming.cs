using System;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>BPM/offset-aware note and scrub snapping.</summary>
public static class EditorTiming
{
    public static double StepMs(Playhead playhead)
    {
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        return beatMs * 4.0 / Math.Max(1, playhead.SnapDivision);
    }

    public static long Snap(long rawMs, Playhead playhead)
    {
        double step = StepMs(playhead);
        double relative = rawMs - playhead.OffsetMs;
        double snapped = Math.Round(relative / step) * step + playhead.OffsetMs;
        return Math.Max(0, (long)snapped);
    }
}
