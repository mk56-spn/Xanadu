using System;
using Writer.Ecs;

namespace Writer.Systems.Editor;

/// <summary>BPM/offset-aware note and scrub snapping.</summary>
public static class EditorTiming
{
    public static long Snap(long rawMs, EditorPlayhead playhead)
    {
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        double step = beatMs * 4.0 / Math.Max(1, playhead.SnapDivision);
        double relative = rawMs - playhead.OffsetMs;
        double snapped = Math.Round(relative / step) * step + playhead.OffsetMs;
        return Math.Max(0, (long)snapped);
    }
}
