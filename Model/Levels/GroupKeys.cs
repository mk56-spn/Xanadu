using System;
using System.Collections.Generic;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Levels;

/// <summary>Where a group's keys play on the song, for a timeline to show them.</summary>
public static class GroupKeys
{
    /// <summary>
    /// The picked group's field rows on the song timeline: each field of
    /// <see cref="LevelItemFields"/> that <paramref name="node"/> keys ("" = the
    /// group's own), in declared order (<see cref="KeyRows.Fields"/>).
    /// </summary>
    public static List<KeyField> FieldRows(GroupDocument group, string node) =>
        KeyRows.Fields(group.Clip, new KeyTarget(node, ""), LevelItemFields.All);

    /// <summary>A looping group shows at most this many repeats of a key, so a short loop on a long window stays cheap.</summary>
    private const int MaxRepeats = 512;

    /// <summary>
    /// Every key time of <paramref name="group"/> with each song beat it plays
    /// on between <paramref name="startMs"/> and <paramref name="endMs"/>: once
    /// for a group that doesn't loop, once a loop for one that does. Only the
    /// keys of <paramref name="node"/> when one is given ("" = the whole group's),
    /// and only its <paramref name="field"/>'s when that is (a field row).
    /// </summary>
    public static IEnumerable<(float ClipTime, double Beat)> Shown(
        GroupDocument group, TempoMap tempo, double startMs, double endMs, string? node = null, string? field = null)
    {
        double from = tempo.BeatAt(startMs);
        double to = tempo.BeatAt(endMs);
        List<float> times = node is not null
            ? KeyRows.KeyTimes(group.Clip, new KeyTarget(node, ""), field)
            : group.Clip.KeyTimes();
        if (!group.Clip.Loop)
        {
            foreach (float time in times)
            {
                if (time >= from && time <= to)
                    yield return (time, time);
            }
            yield break;
        }

        double length = Math.Max(0.0001, group.Clip.LoopEnd - group.Clip.LoopStart);
        foreach (float time in times)
        {
            // The first repeat at or after the window's start, then one a loop on.
            double first = time + Math.Ceiling((from - time) / length) * length;
            int count = 0;
            for (double beat = first; beat <= to && count < MaxRepeats; beat += length, count++)
                yield return (time, beat);
        }
    }
}
