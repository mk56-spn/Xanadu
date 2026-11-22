using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xanadu.Audio;

namespace Xanadu.Tools.DevTools.Clock;

/// <summary>One look at both clocks: wall time, the raw cursor and the filled-in position, ms.</summary>
internal readonly record struct ClockSample(double WallMs, double RawMs, double FilledMs, long Chunks);

/// <summary>A spread of values, ms.</summary>
internal readonly record struct Spread(int Count, double Min, double P1, double P50, double P99, double Max, double Stdev)
{
    /// <summary>Half the p1–p99 range: the ± a judgement is off by from this clock alone.</summary>
    public double Plus => (P99 - P1) / 2;

    public static Spread Of(List<double> values)
    {
        if (values.Count == 0)
            return default;
        values.Sort();
        double mean = 0;
        foreach (double v in values)
            mean += v;
        mean /= values.Count;
        double square = 0;
        foreach (double v in values)
            square += (v - mean) * (v - mean);
        double At(double q) => values[Math.Clamp((int)(q * (values.Count - 1)), 0, values.Count - 1)];
        return new Spread(values.Count, values[0], At(0.01), At(0.5), At(0.99), values[^1], Math.Sqrt(square / values.Count));
    }
}

/// <summary>What a stretch of samples says about the track clock.</summary>
internal readonly record struct ClockReport(
    double Seconds,
    double ChunkMs,
    Spread RawSteps,
    Spread ChunkGaps,
    Spread RawJitter,
    Spread FilledJitter,
    int Backwards,
    double DriftPpm);

/// <summary>
/// Plays a track through the game's own <see cref="MiniaudioTrackAudio"/> and
/// reads both its clocks about once a millisecond on a thread of its own. The
/// jitter of a clock is how far it sits from a straight line fitted through it
/// against the wall clock: what is left is error a judgement would carry.
/// </summary>
internal sealed class ClockProbe : IDisposable
{
    public const string Track = "Assets/Audio/test-track.wav";

    /// <summary>The device fills its buffer as a song starts, in one long step; that is not the steady clock.</summary>
    public const double SettleMs = 500;

    private const int Capacity = 60_000;

    private readonly MiniaudioTrackAudio _audio = new();
    private readonly List<ClockSample> _samples = new(Capacity);
    private readonly object _lock = new();
    private Thread? _thread;
    private volatile bool _running;

    public bool Running => _running;

    public string? Error { get; private set; }

    public bool Start(float volume)
    {
        Stop();
        if (!_audio.Play(Track, volume))
        {
            Error = $"could not play {Track}";
            return false;
        }

        Error = null;
        lock (_lock)
            _samples.Clear();
        _running = true;
        _thread = new Thread(Sample) { IsBackground = true, Name = "clock probe", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        return true;
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join();
        _thread = null;
        _audio.Stop();
    }

    public void SetVolume(float volume) => _audio.SetVolume(volume);

    private void Sample()
    {
        var wall = Stopwatch.StartNew();
        while (_running)
        {
            if (!_audio.IsPlaying)
            {
                _running = false;
                break;
            }

            var sample = new ClockSample(
                wall.Elapsed.TotalMilliseconds,
                _audio.RawPositionSeconds * 1000d,
                _audio.PositionSeconds * 1000d,
                _audio.Chunks);
            lock (_lock)
            {
                if (_samples.Count == Capacity)
                    _samples.RemoveRange(0, Capacity / 4);
                _samples.Add(sample);
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>The samples from the last <paramref name="seconds"/> (all of them when 0), past the start's settling.</summary>
    public List<ClockSample> Recent(double seconds)
    {
        var into = new List<ClockSample>();
        lock (_lock)
        {
            if (_samples.Count == 0)
                return into;
            double last = _samples[^1].WallMs;
            double from = Math.Max(_samples[0].WallMs + SettleMs, seconds > 0 ? last - seconds * 1000d : double.NegativeInfinity);
            foreach (ClockSample sample in _samples)
            {
                if (sample.WallMs >= from)
                    into.Add(sample);
            }
        }

        return into;
    }

    public ClockReport Report(List<ClockSample> samples)
    {
        if (samples.Count < 3)
            return default;

        var steps = new List<double>();
        var gaps = new List<double>();
        int backwards = 0;
        double lastChunkWall = double.NaN;
        for (int i = 1; i < samples.Count; i++)
        {
            ClockSample a = samples[i - 1], b = samples[i];
            if (b.RawMs != a.RawMs)
                steps.Add(b.RawMs - a.RawMs);
            if (b.FilledMs < a.FilledMs)
                backwards++;
            if (b.Chunks != a.Chunks)
            {
                if (!double.IsNaN(lastChunkWall))
                    gaps.Add(b.WallMs - lastChunkWall);
                lastChunkWall = b.WallMs;
            }
        }

        (List<double> raw, double slope) = Residuals(samples, static s => s.RawMs);
        (List<double> filled, _) = Residuals(samples, static s => s.FilledMs);
        return new ClockReport(
            (samples[^1].WallMs - samples[0].WallMs) / 1000d,
            _audio.SampleRate == 0 ? 0 : _audio.ChunkFrames * 1000d / _audio.SampleRate,
            Spread.Of(steps),
            Spread.Of(gaps),
            Spread.Of(raw),
            Spread.Of(filled),
            backwards,
            (slope - 1d) * 1e6);
    }

    /// <summary>How far each sample of one clock sits from the straight line fitted through it against the wall, ms, in sample order.</summary>
    public static (List<double> Residuals, double Slope) Residuals(List<ClockSample> samples, Func<ClockSample, double> clock)
    {
        double n = samples.Count, sx = 0, sy = 0, sxx = 0, sxy = 0;
        double x0 = samples[0].WallMs, y0 = clock(samples[0]);
        foreach (ClockSample s in samples)
        {
            double x = s.WallMs - x0, y = clock(s) - y0;
            sx += x;
            sy += y;
            sxx += x * x;
            sxy += x * y;
        }

        double slope = (n * sxy - sx * sy) / Math.Max(1e-9, n * sxx - sx * sx);
        double intercept = (sy - slope * sx) / n;
        var into = new List<double>(samples.Count);
        foreach (ClockSample s in samples)
            into.Add(clock(s) - y0 - (intercept + slope * (s.WallMs - x0)));
        return (into, slope);
    }

    public void Dispose()
    {
        Stop();
        _audio.Dispose();
    }
}
