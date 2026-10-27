using System;
using System.Collections.Generic;

namespace Xanadu.Editor;

/// <summary>Computes BPM from a short sequence of tap timestamps.</summary>
public sealed class TapTempo
{
    private const long ResetGapMs = 2_000;
    private const int MaxIntervals = 4;

    private readonly Queue<long> _intervals = new();
    private long _lastTapMs = -1;

    public int? Tap(long nowMs)
    {
        if (_lastTapMs < 0 || nowMs - _lastTapMs > ResetGapMs)
        {
            _intervals.Clear();
            _lastTapMs = nowMs;
            return null;
        }

        long interval = nowMs - _lastTapMs;
        _lastTapMs = nowMs;

        if (interval <= 0)
            return null;

        _intervals.Enqueue(interval);
        while (_intervals.Count > MaxIntervals)
            _intervals.Dequeue();

        double average = 0;
        foreach (long value in _intervals)
            average += value;
        average /= _intervals.Count;

        return Math.Clamp((int)Math.Round(60_000.0 / average), 1, 1000);
    }

    public void Reset()
    {
        _intervals.Clear();
        _lastTapMs = -1;
    }
}
