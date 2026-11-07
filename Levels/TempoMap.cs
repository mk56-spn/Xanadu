using System;
using System.Collections.Generic;

namespace Xanadu.Levels;

/// <summary>One tempo section: from <see cref="TimeMs"/> on, <see cref="Bpm"/> beats a minute, <see cref="BeatsPerBar"/> to a bar.</summary>
public readonly record struct TempoPoint(double TimeMs, double Bpm, int BeatsPerBar = 4);

/// <summary>
/// A song's timing: where beat 0 is and how fast beats come, section by
/// section. Songs are timed once, globally (the song timing screen); everything
/// that thinks in beats — snapping, rulers, the metronome, group cycles — asks
/// this, so tempo changes are followed everywhere. Immutable; build a new one
/// to change it.
/// <para>
/// The first point is beat 0 (the song's offset). Before it the first tempo
/// runs backwards, so beats there are negative. A section always starts on a
/// whole beat: a change placed between beats is moved to the next one.
/// </para>
/// </summary>
public sealed class TempoMap
{
    public const double MinBpm = 1;
    public const double MaxBpm = 1000;

    private readonly TempoPoint[] _points;

    /// <summary>The beat each point starts on, alongside <see cref="_points"/>.</summary>
    private readonly double[] _beats;

    public static TempoMap Default { get; } = Single(120, 0);

    private TempoMap(TempoPoint[] points)
    {
        _points = points;
        _beats = new double[points.Length];
        for (int i = 1; i < points.Length; i++)
        {
            TempoPoint previous = points[i - 1];
            _beats[i] = _beats[i - 1] + (points[i].TimeMs - previous.TimeMs) / BeatMs(previous.Bpm);
        }
    }

    /// <summary>The sections in time order; the first is beat 0.</summary>
    public IReadOnlyList<TempoPoint> Points => _points;

    /// <summary>One tempo for the whole song.</summary>
    public static TempoMap Single(double bpm, double offsetMs, int beatsPerBar = 4) =>
        new(new[] { new TempoPoint(offsetMs, ClampBpm(bpm), ClampBar(beatsPerBar)) });

    /// <summary>
    /// A map from a first point and later changes. Changes at or before the
    /// first point are dropped, each change is moved onto the next whole beat
    /// of the section before it, and of two changes landing on one beat the
    /// later in the list wins.
    /// </summary>
    public static TempoMap From(TempoPoint first, IEnumerable<TempoPoint>? changes)
    {
        var points = new List<TempoPoint> { Clean(first) };
        var sorted = new List<TempoPoint>();
        if (changes is not null)
        {
            foreach (TempoPoint change in changes)
            {
                if (change.TimeMs > first.TimeMs)
                    sorted.Add(Clean(change));
            }
        }

        sorted.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        foreach (TempoPoint change in sorted)
        {
            TempoPoint previous = points[^1];
            double beats = Math.Ceiling((change.TimeMs - previous.TimeMs) / BeatMs(previous.Bpm) - 1e-6);
            double onBeat = previous.TimeMs + Math.Max(1, beats) * BeatMs(previous.Bpm);
            TempoPoint snapped = change with { TimeMs = onBeat };
            if (Math.Abs(onBeat - previous.TimeMs) < 1e-6)
                points[^1] = snapped;
            else
                points.Add(snapped);
        }

        return new TempoMap(points.ToArray());
    }

    public static double BeatMs(double bpm) => 60_000.0 / ClampBpm(bpm);

    /// <summary>The beat count at a moment (fractional; negative before beat 0).</summary>
    public double BeatAt(double timeMs)
    {
        int i = SectionAtTime(timeMs);
        return _beats[i] + (timeMs - _points[i].TimeMs) / BeatMs(_points[i].Bpm);
    }

    /// <summary>The moment of a beat count.</summary>
    public double TimeAt(double beat)
    {
        int i = SectionAtBeat(beat);
        return _points[i].TimeMs + (beat - _beats[i]) * BeatMs(_points[i].Bpm);
    }

    /// <summary>The tempo section in force at a moment.</summary>
    public TempoPoint At(double timeMs) => _points[SectionAtTime(timeMs)];

    /// <summary>The nearest grid line to a moment, grid lines being <paramref name="stepBeats"/> apart.</summary>
    public double Snap(double timeMs, double stepBeats)
    {
        double step = Step(stepBeats);
        return TimeAt(Math.Round(BeatAt(timeMs) / step) * step);
    }

    /// <summary>The grid line <paramref name="lines"/> lines on from a moment's nearest one (negative is earlier).</summary>
    public double StepFrom(double timeMs, double stepBeats, int lines)
    {
        double step = Step(stepBeats);
        return TimeAt((Math.Round(BeatAt(timeMs) / step) + lines) * step);
    }

    /// <summary>
    /// The grid lines inside a window, <paramref name="stepBeats"/> apart, with
    /// whether each is a beat and whether it starts a bar (bars counted from
    /// each section's start).
    /// </summary>
    public List<TempoTick> Ticks(double fromMs, double toMs, double stepBeats)
    {
        var ticks = new List<TempoTick>();
        double step = Step(stepBeats);
        long first = (long)Math.Floor(BeatAt(fromMs) / step);
        long last = (long)Math.Ceiling(BeatAt(toMs) / step);
        for (long n = first; n <= last && ticks.Count < 100_000; n++)
        {
            double beat = n * step;
            double time = TimeAt(beat);
            if (time < fromMs - 1e-6 || time > toMs + 1e-6)
                continue;

            bool onBeat = Math.Abs(beat - Math.Round(beat)) < 1e-6;
            int section = SectionAtBeat(beat);
            double intoSection = beat - _beats[section];
            int perBar = _points[section].BeatsPerBar;
            bool bar = onBeat && Math.Abs(intoSection - Math.Round(intoSection / perBar) * perBar) < 1e-6;
            ticks.Add(new TempoTick(time, beat, onBeat, bar));
        }

        return ticks;
    }

    private static double Step(double stepBeats) =>
        double.IsFinite(stepBeats) && stepBeats > 1e-4 ? stepBeats : 1;

    private int SectionAtTime(double timeMs)
    {
        int i = _points.Length - 1;
        while (i > 0 && timeMs < _points[i].TimeMs)
            i--;
        return i;
    }

    private int SectionAtBeat(double beat)
    {
        int i = _beats.Length - 1;
        while (i > 0 && beat < _beats[i])
            i--;
        return i;
    }

    private static TempoPoint Clean(TempoPoint point) =>
        point with { Bpm = ClampBpm(point.Bpm), BeatsPerBar = ClampBar(point.BeatsPerBar) };

    private static double ClampBpm(double bpm) =>
        double.IsFinite(bpm) ? Math.Clamp(bpm, MinBpm, MaxBpm) : 120;

    private static int ClampBar(int beats) => Math.Clamp(beats, 1, 32);
}

/// <summary>One grid line: its moment, its beat count, and whether it falls on a beat and on a bar.</summary>
public readonly record struct TempoTick(double TimeMs, double Beat, bool OnBeat, bool OnBar);

/// <summary>How tempos read in the UI.</summary>
public static class TempoText
{
    /// <summary>A BPM with no trailing zeros: 120, 128.5, 97.25.</summary>
    public static string Bpm(double bpm) => bpm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
