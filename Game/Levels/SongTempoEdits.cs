using System;
using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Levels;

/// <summary>
/// Editing a song's tempo map on the song timing screen (songs are timed
/// globally; the level editor only reads timing). Every edit ends with
/// <see cref="Normalize"/>, so what is stored is exactly what the map uses:
/// changes in time order, each on a whole beat of the section before it.
/// </summary>
public static class SongTempoEdits
{
    /// <summary>How far after the last section a new change goes, in bars.</summary>
    public const int NewChangeBars = 4;

    /// <summary>Adds a change four bars after the last section, at its tempo and bar length. Returns its index.</summary>
    public static int Add(SongDocument song)
    {
        TempoMap map = song.Tempo();
        TempoPoint last = map.Points[^1];
        double at = map.TimeAt(map.BeatAt(last.TimeMs) + NewChangeBars * last.BeatsPerBar);
        song.TempoChanges.Add(new TempoChangeDocument { TimeMs = at, Bpm = last.Bpm, BeatsPerBar = last.BeatsPerBar });
        Normalize(song);
        return song.TempoChanges.FindIndex(change => Math.Abs(change.TimeMs - at) < 0.5);
    }

    public static bool Remove(SongDocument song, int index)
    {
        if (index < 0 || index >= song.TempoChanges.Count)
            return false;
        song.TempoChanges.RemoveAt(index);
        Normalize(song);
        return true;
    }

    /// <summary>Sets one change's moment, tempo or bar length (null leaves it). False when nothing changed.</summary>
    public static bool Set(SongDocument song, int index, double? timeMs = null, double? bpm = null, int? beatsPerBar = null)
    {
        if (index < 0 || index >= song.TempoChanges.Count)
            return false;

        TempoChangeDocument change = song.TempoChanges[index];
        string before = Describe(song);
        if (timeMs is { } time)
            change.TimeMs = time;
        if (bpm is { } tempo)
            change.Bpm = tempo;
        if (beatsPerBar is { } bar)
            change.BeatsPerBar = bar;
        Normalize(song);
        return Describe(song) != before;
    }

    /// <summary>Sets the first section: its tempo, bar length or offset (null leaves it). False when nothing changed.</summary>
    public static bool SetBase(SongDocument song, double? bpm = null, int? beatsPerBar = null, long? offsetMs = null)
    {
        string before = Describe(song);
        if (bpm is { } tempo)
            song.Bpm = tempo;
        if (beatsPerBar is { } bar)
            song.BeatsPerBar = bar;
        if (offsetMs is { } offset)
            song.OffsetMs = Math.Clamp(offset, -600_000, 600_000);
        Normalize(song);
        return Describe(song) != before;
    }

    /// <summary>
    /// Rewrites the song's timing as the map reads it: tempos and bar lengths
    /// clamped, changes sorted, each moved onto a whole beat, duplicates and
    /// changes before the offset dropped.
    /// </summary>
    public static void Normalize(SongDocument song)
    {
        song.TempoChanges ??= new List<TempoChangeDocument>();
        TempoMap map = song.Tempo();
        TempoPoint first = map.Points[0];
        song.Bpm = first.Bpm;
        song.BeatsPerBar = first.BeatsPerBar;
        song.TempoChanges = map.Points.Skip(1)
            .Select(point => new TempoChangeDocument
            {
                TimeMs = Math.Round(point.TimeMs, 3),
                Bpm = point.Bpm,
                BeatsPerBar = point.BeatsPerBar,
            })
            .ToList();
    }

    /// <summary>Where a moment falls, as the screen shows it: beat count and bar (both from 1).</summary>
    public static string Position(TempoMap map, double timeMs)
    {
        double beat = map.BeatAt(timeMs);
        long whole = (long)Math.Round(beat);
        long bars = 0;
        long beatInBar = 0;
        for (int i = 0; i < map.Points.Count; i++)
        {
            double sectionStart = map.BeatAt(map.Points[i].TimeMs);
            double sectionEnd = i + 1 < map.Points.Count ? map.BeatAt(map.Points[i + 1].TimeMs) : double.MaxValue;
            int perBar = map.Points[i].BeatsPerBar;
            if (whole < sectionEnd)
            {
                double into = whole - sectionStart;
                bars += (long)Math.Floor(into / perBar);
                beatInBar = (long)(into - Math.Floor(into / perBar) * perBar);
                break;
            }

            // A section that ends mid-bar still counts its last, short bar.
            bars += (long)Math.Ceiling((sectionEnd - sectionStart) / perBar);
        }

        return $"BEAT {whole + 1} · BAR {bars + 1}.{beatInBar + 1}";
    }

    private static string Describe(SongDocument song) =>
        $"{song.Bpm}|{song.BeatsPerBar}|{song.OffsetMs}|" +
        string.Join(";", song.TempoChanges.Select(change => $"{change.TimeMs}:{change.Bpm}:{change.BeatsPerBar}"));
}
