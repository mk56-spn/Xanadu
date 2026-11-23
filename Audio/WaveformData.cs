namespace Xanadu.Audio;

/// <summary>A frequency band of a <see cref="WaveformData"/>.</summary>
public enum WaveformBand
{
    /// <summary>Kicks and bass.</summary>
    Low,

    /// <summary>Vocals, snares and most instruments.</summary>
    Mid,

    /// <summary>Hats, cymbals and the attack of a hit.</summary>
    High,
}

/// <summary>
/// How loud each frequency band is over a track, 0 to 1 in fixed steps of time,
/// and able to average any stretch of it in constant time, so a timeline can
/// draw it per column on every frame at any zoom.
/// </summary>
public sealed class WaveformData
{
    private readonly float[][] _levels;
    private readonly double[][] _running;

    public WaveformData(float[] low, float[] mid, float[] high, long durationMs)
    {
        _levels = [low, mid, high];
        _running = new double[3][];
        for (int band = 0; band < 3; band++)
        {
            float[] levels = _levels[band];
            var running = new double[levels.Length + 1];
            for (int i = 0; i < levels.Length; i++)
                running[i + 1] = running[i] + levels[i];

            _running[band] = running;
        }

        DurationMs = durationMs;
    }

    public long DurationMs { get; }

    public int Steps => _levels[0].Length;

    public float[] Levels(WaveformBand band) => _levels[(int)band];

    /// <summary>The band's running sum: entry i is the sum of the first i levels, <see cref="Steps"/> + 1 entries.</summary>
    public double[] RunningSums(WaveformBand band) => _running[(int)band];

    /// <summary>
    /// The band's average level from one time to another, counting partly covered
    /// steps by how much is covered, so the result slides smoothly with the times.
    /// </summary>
    public float Average(WaveformBand band, double startMs, double endMs)
    {
        if (DurationMs <= 0 || endMs <= startMs)
            return 0f;

        double[] running = _running[(int)band];
        double from = RunningAt(running, startMs);
        double to = RunningAt(running, endMs);
        double steps = (endMs - startMs) * Steps / DurationMs;
        return (float)((to - from) / steps);
    }

    private double RunningAt(double[] running, double timeMs)
    {
        double position = Math.Clamp(timeMs * Steps / DurationMs, 0d, Steps);
        int step = Math.Min((int)position, Steps - 1);
        return running[step] + (running[step + 1] - running[step]) * (position - step);
    }
}
