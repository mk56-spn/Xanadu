using System;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Timeline;

namespace Xanadu.Systems.Scenes;

/// <summary>What a calibration run measured.</summary>
/// <param name="OffsetMs">The global offset that would centre these taps on chart notes.</param>
/// <param name="SpreadMs">How far the kept taps scatter around it, one standard deviation.</param>
/// <param name="Used">Taps the result is made from.</param>
/// <param name="Rejected">Stray taps left out of it.</param>
public readonly record struct OffsetMeasurement(double OffsetMs, double SpreadMs, int Used, int Rejected)
{
    /// <summary>How far the true value could be from <see cref="OffsetMs"/>, one standard error.</summary>
    public double UncertaintyMs => Used > 0 ? SpreadMs / Math.Sqrt(Used) : 0d;
}

/// <summary>
/// The click track's timing and the sums on the taps made to it, apart from any
/// screen or audio. Taps are measured with the same clock play judges with, so
/// everything between the sound and the judgement is in the result: output buffer,
/// speakers, the player's ear and hand, keyboard and frame time.
/// </summary>
public sealed class OffsetCalibration
{
    public const string TrackPath = "res://Assets/Audio/offset-calibration.wav";

    /// <summary>When the first click is in the track, in seconds.</summary>
    public const double FirstClickSeconds = 1.0;

    public const double BeatSeconds = 0.5;

    /// <summary>Clicks to settle into the beat with, which do not count.</summary>
    public const int CountInBeats = 4;

    /// <summary>Clicks that count; one bar of count-in plus eight bars.</summary>
    public const int MeasuredBeats = 32;

    /// <summary>Fewer kept taps than this is too little to trust.</summary>
    public const int MinimumTaps = 16;

    /// <summary>A kept tap is at most this many spreads from the middle...</summary>
    private const double OutlierSpreads = 3d;

    /// <summary>...but always this close counts, so a very steady run does not throw out good taps.</summary>
    private const double AlwaysKeptMs = 15d;

    /// <summary>Scales a median absolute deviation to a standard deviation.</summary>
    private const double MadToDeviation = 1.4826d;

    private readonly Dictionary<int, double> _taps = new();

    public static int TotalBeats => CountInBeats + MeasuredBeats;

    public int TapCount => _taps.Count;

    public bool IsComplete => _taps.Count >= MeasuredBeats;

    /// <summary>Every counted tap so far, in beat order, as the offset each one alone would ask for.</summary>
    public IEnumerable<double> Taps => _taps.OrderBy(tap => tap.Key).Select(tap => tap.Value);

    /// <summary>Whether a click has counted-in yet at this point in the track.</summary>
    public static bool InCountIn(double positionSeconds) =>
        positionSeconds < FirstClickSeconds + (CountInBeats - 0.5d) * BeatSeconds;

    /// <summary>Records a tap against the nearest click. Returns whether it counted.</summary>
    public bool Tap(double positionSeconds)
    {
        int beat = (int)Math.Round((positionSeconds - FirstClickSeconds) / BeatSeconds);
        if (beat < CountInBeats || beat >= TotalBeats || _taps.ContainsKey(beat))
            return false;

        double click = FirstClickSeconds + beat * BeatSeconds;
        _taps[beat] = (positionSeconds - click) * 1000d + ChartTiming.AudioLagMs;
        return true;
    }

    /// <summary>The result from the taps so far, or null with none yet.</summary>
    public OffsetMeasurement? Measure()
    {
        if (_taps.Count == 0)
            return null;

        List<double> taps = _taps.Values.ToList();
        double middle = Median(taps);
        double deviation = Median(taps.Select(tap => Math.Abs(tap - middle)).ToList()) * MadToDeviation;
        double keep = Math.Max(deviation * OutlierSpreads, AlwaysKeptMs);
        List<double> kept = taps.Where(tap => Math.Abs(tap - middle) <= keep).ToList();

        // The median again, of what is left, so strays cannot pull it at all.
        double offset = Median(kept);
        double variance = kept.Sum(tap => (tap - offset) * (tap - offset)) / kept.Count;
        return new OffsetMeasurement(offset, Math.Sqrt(variance), kept.Count, taps.Count - kept.Count);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int half = values.Count / 2;
        return values.Count % 2 == 1 ? values[half] : (values[half - 1] + values[half]) / 2d;
    }
}
