using System;

namespace Xanadu.Audio;

/// <summary>
/// Turns decoded audio into <see cref="WaveformData"/>. Peak amplitude is useless
/// for this: a mastered song sits at full scale almost everywhere, so every column
/// reads the same. Instead each band is measured as loudness over a few
/// milliseconds, in decibels against that band's own loud level in this song, which
/// leaves the kicks and snares standing clear of what is between them.
/// </summary>
public static class WaveformAnalyzer
{
    public const double StepMs = 5d;

    /// <summary>Below this the bass is measured as bass, and above it as the middle.</summary>
    public const float LowCrossoverHz = 180f;

    /// <summary>Above this the sound is measured as highs, and below it as the middle.</summary>
    public const float HighCrossoverHz = 2800f;

    /// <summary>How far below a band's loud level it reads as silence.</summary>
    public const float RangeDb = 30f;

    /// <summary>The share of a band's steps quieter than its loud level, which reads as full height.</summary>
    private const float LoudPercentile = 0.995f;

    /// <summary>How far a band may be lifted over the loudest band, so a nearly empty one is not blown up to full height.</summary>
    private const float MaxBandLiftDb = 18f;

    /// <summary>How long a stretch counts as the sound around a moment, either side of it.</summary>
    private const double SurroundMs = 200d;

    /// <summary>How much of the sound around a moment is taken off it, so that hits stand out of sustained sound.</summary>
    private const float SurroundWeight = 0.65f;

    public static WaveformData Analyze(float[] samples, int sampleRate)
    {
        long durationMs = samples.Length * 1000L / sampleRate;
        int stepSamples = Math.Max(1, (int)(sampleRate * StepMs / 1000d));
        int steps = Math.Max(1, (int)Math.Ceiling(samples.Length / (double)stepSamples));

        var low = new float[steps];
        var mid = new float[steps];
        var high = new float[steps];
        var lowPass = Biquad.LowPass(LowCrossoverHz, sampleRate);
        var highPass = Biquad.HighPass(HighCrossoverHz, sampleRate);

        for (int step = 0; step < steps; step++)
        {
            int start = step * stepSamples;
            int end = Math.Min(samples.Length, start + stepSamples);
            double lowPower = 0, midPower = 0, highPower = 0;
            for (int i = start; i < end; i++)
            {
                float sample = samples[i];
                float l = lowPass.Next(sample);
                float h = highPass.Next(sample);
                float m = sample - l - h;
                lowPower += l * l;
                midPower += m * m;
                highPower += h * h;
            }

            int count = Math.Max(1, end - start);
            low[step] = (float)(lowPower / count);
            mid[step] = (float)(midPower / count);
            high[step] = (float)(highPower / count);
        }

        // A bass wave is about as long as a step, so unsmoothed it would ripple with its own pitch.
        Smooth(low);
        float loudest = Math.Max(Loud(low), Math.Max(Loud(mid), Loud(high)));
        float floor = loudest * MathF.Pow(10f, -MaxBandLiftDb / 10f);
        ToLevels(low, floor);
        ToLevels(mid, floor);
        ToLevels(high, floor);
        return new WaveformData(low, mid, high, durationMs);
    }

    private static void Smooth(float[] values)
    {
        float previous = values[0];
        for (int i = 0; i < values.Length; i++)
        {
            float current = values[i];
            float next = i + 1 < values.Length ? values[i + 1] : current;
            values[i] = previous * 0.25f + current * 0.5f + next * 0.25f;
            previous = current;
        }
    }

    private static float Loud(float[] powers)
    {
        var sorted = (float[])powers.Clone();
        Array.Sort(sorted);
        float loud = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * LoudPercentile))];

        // Sound too sparse to reach the percentile, such as a few clicks, is measured against its loudest.
        return loud > 0f ? loud : sorted[^1];
    }

    /// <summary>Turns powers into 0 to 1 levels against the band's own loud level, no lower than <paramref name="floor"/>.</summary>
    private static void ToLevels(float[] powers, float floor)
    {
        float loud = Math.Max(Loud(powers), floor);
        if (loud <= 0f)
        {
            Array.Clear(powers);
            return;
        }

        double total = 0;
        for (int i = 0; i < powers.Length; i++)
        {
            float db = 10f * MathF.Log10(Math.Max(powers[i] / loud, 1e-9f));
            powers[i] = Math.Clamp(1f + db / RangeDb, 0f, 1f);
            total += powers[i];
        }

        TakeOffSurround(powers, (float)(total / powers.Length));
    }

    /// <summary>
    /// Lowers each level by the sound around it. A steady level sinks and a hit keeps
    /// its height, while a quiet passage stays quieter than a loud one because the
    /// divisor is the same for the whole track.
    /// </summary>
    private static void TakeOffSurround(float[] levels, float average)
    {
        var running = new double[levels.Length + 1];
        for (int i = 0; i < levels.Length; i++)
            running[i + 1] = running[i] + levels[i];

        int reach = (int)(SurroundMs / StepMs);
        float scale = Math.Max(0.05f, 1f - SurroundWeight * average);
        for (int i = 0; i < levels.Length; i++)
        {
            int start = Math.Max(0, i - reach);
            int end = Math.Min(levels.Length, i + reach + 1);
            float surround = (float)((running[end] - running[start]) / (end - start));
            levels[i] = Math.Clamp((levels[i] - SurroundWeight * surround) / scale, 0f, 1f);
        }
    }

    /// <summary>A second-order Butterworth filter.</summary>
    private struct Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public static Biquad LowPass(float hz, int sampleRate)
        {
            (float cos, float alpha) = Terms(hz, sampleRate);
            float a0 = 1f + alpha;
            return new Biquad
            {
                _b0 = (1f - cos) / 2f / a0,
                _b1 = (1f - cos) / a0,
                _b2 = (1f - cos) / 2f / a0,
                _a1 = -2f * cos / a0,
                _a2 = (1f - alpha) / a0,
            };
        }

        public static Biquad HighPass(float hz, int sampleRate)
        {
            (float cos, float alpha) = Terms(hz, sampleRate);
            float a0 = 1f + alpha;
            return new Biquad
            {
                _b0 = (1f + cos) / 2f / a0,
                _b1 = -(1f + cos) / a0,
                _b2 = (1f + cos) / 2f / a0,
                _a1 = -2f * cos / a0,
                _a2 = (1f - alpha) / a0,
            };
        }

        private static (float Cos, float Alpha) Terms(float hz, int sampleRate)
        {
            float omega = 2f * MathF.PI * Math.Min(hz, sampleRate * 0.45f) / sampleRate;
            return (MathF.Cos(omega), MathF.Sin(omega) / MathF.Sqrt(2f));
        }

        public float Next(float input)
        {
            float output = _b0 * input + _z1;
            _z1 = _b1 * input - _a1 * output + _z2;
            _z2 = _b2 * input - _a2 * output;
            return output;
        }
    }
}
