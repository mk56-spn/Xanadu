using System;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Note and scrub snapping on the song's tempo map. <see cref="Playhead.SnapDivision"/>
/// is grid lines per four beats here (4 = every beat, 16 = every quarter beat).
/// </summary>
public static class EditorTiming
{
    /// <summary>The grid step in beats.</summary>
    public static double StepBeats(Playhead playhead) => 4.0 / Math.Max(1, playhead.SnapDivision);

    /// <summary>The grid step in milliseconds at the playhead (tempo changes make it vary through the song).</summary>
    public static double StepMs(Playhead playhead) =>
        Xanadu.Levels.TempoMap.BeatMs(playhead.Tempo.At(playhead.TimeMs).Bpm) * StepBeats(playhead);

    /// <summary>The grid line at or before the playhead's nearest one: where the local step grid starts.</summary>
    public static double StepOriginMs(Playhead playhead) =>
        playhead.Tempo.Snap(playhead.TimeMs, StepBeats(playhead));

    /// <summary>The nearest beat-grid time, or the time itself while music snap is off.</summary>
    public static long Snap(long rawMs, Playhead playhead)
    {
        if (playhead.SnapOff)
            return Math.Max(0, rawMs);

        return Math.Max(0, (long)Math.Round(playhead.Tempo.Snap(rawMs, StepBeats(playhead))));
    }

    /// <summary>The grid line <paramref name="lines"/> on from a time's nearest one.</summary>
    public static long Step(long fromMs, Playhead playhead, int lines) =>
        Math.Max(0, (long)Math.Round(playhead.Tempo.StepFrom(fromMs, StepBeats(playhead), lines)));
}
