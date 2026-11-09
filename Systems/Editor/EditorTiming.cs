using System;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Note, scrub and group snapping on the song's tempo map. <see cref="Playhead.SnapDivision"/>
/// is the beat snap divisor, as in osu! and most rhythm games: 1 snaps to whole
/// beats (one beat is one tick of the BPM), 2 to half beats, 4 to quarter beats.
/// </summary>
public static class EditorTiming
{
    /// <summary>The grid step in beats.</summary>
    public static double StepBeats(Playhead playhead) => 1.0 / Math.Max(1, playhead.SnapDivision);

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
