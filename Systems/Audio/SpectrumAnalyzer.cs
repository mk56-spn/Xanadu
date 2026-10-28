using System;
using System.Collections.Generic;

namespace Xanadu.Systems.Audio;

/// <summary>Band levels, 0 to 1, for every analysis frame of a track.</summary>
public sealed class SpectrumData(float[] levels, int bands, float framesPerSecond)
{
    public int Bands { get; } = bands;

    public float FramesPerSecond { get; } = framesPerSecond;

    public int FrameCount => levels.Length / Bands;

    /// <summary>Fills <paramref name="destination"/> with the levels at a playback time.</summary>
    public void Sample(double seconds, Span<float> destination)
    {
        int frames = FrameCount;
        if (frames == 0 || seconds < 0)
        {
            destination.Clear();
            return;
        }

        double position = seconds * FramesPerSecond;
        int low = (int)Math.Min(position, frames - 1);
        int high = Math.Min(low + 1, frames - 1);
        float blend = (float)Math.Clamp(position - low, 0, 1);
        for (int band = 0; band < destination.Length; band++)
        {
            int source = band * Bands / destination.Length;
            float a = levels[low * Bands + source];
            float b = levels[high * Bands + source];
            destination[band] = a + (b - a) * blend;
        }
    }
}

/// <summary>
/// Turns decoded audio into the band levels a visualizer draws: a Hann-windowed
/// FFT per frame, grouped into log-spaced bands, scaled to decibels and then
/// normalised so quiet and loud tracks fill the bars alike.
/// </summary>
public static class SpectrumAnalyzer
{
    public const int FftSize = 1024;
    public const int DefaultBands = 48;
    public const int FramesPerSecondTarget = 50;

    private const float LowestHz = 50;
    private const float HighestHz = 10000;
    private const float FloorDb = -60;

    public static SpectrumData Analyze(float[] samples, int sampleRate, int bands = DefaultBands)
    {
        int hop = Math.Max(1, sampleRate / FramesPerSecondTarget);
        int frames = samples.Length < FftSize ? 0 : (samples.Length - FftSize) / hop + 1;
        var levels = new float[frames * bands];
        if (frames == 0)
            return new SpectrumData(levels, bands, (float)sampleRate / hop);

        float[] window = new float[FftSize];
        for (int i = 0; i < FftSize; i++)
            window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (FftSize - 1));

        (int Low, int High, float TiltDb)[] ranges = BandRanges(sampleRate, bands);
        float[] real = new float[FftSize];
        float[] imaginary = new float[FftSize];
        float[] magnitude = new float[FftSize / 2];
        const float reference = FftSize / 4f;

        for (int frame = 0; frame < frames; frame++)
        {
            int start = frame * hop;
            for (int i = 0; i < FftSize; i++)
            {
                real[i] = samples[start + i] * window[i];
                imaginary[i] = 0;
            }

            Fft(real, imaginary);
            for (int i = 0; i < magnitude.Length; i++)
                magnitude[i] = MathF.Sqrt(real[i] * real[i] + imaginary[i] * imaginary[i]);

            for (int band = 0; band < bands; band++)
            {
                (int low, int high, float tilt) = ranges[band];
                float sum = 0;
                for (int bin = low; bin <= high; bin++)
                    sum += magnitude[bin];

                float level = sum / (high - low + 1) / reference;
                float db = 20 * MathF.Log10(level + 1e-6f) + tilt;
                levels[frame * bands + band] = Math.Clamp((db - FloorDb) / -FloorDb, 0f, 1f);
            }
        }

        Normalise(levels, frames, bands);
        return new SpectrumData(levels, bands, (float)sampleRate / hop);
    }

    /// <summary>The FFT bins each band covers, and a tilt that lifts the quieter high bands.</summary>
    private static (int Low, int High, float TiltDb)[] BandRanges(int sampleRate, int bands)
    {
        float binWidth = (float)sampleRate / FftSize;
        float top = Math.Min(HighestHz, sampleRate / 2f - binWidth);
        var ranges = new (int, int, float)[bands];
        for (int band = 0; band < bands; band++)
        {
            float lowHz = LowestHz * MathF.Pow(top / LowestHz, (float)band / bands);
            float highHz = LowestHz * MathF.Pow(top / LowestHz, (float)(band + 1) / bands);
            int low = Math.Clamp((int)(lowHz / binWidth), 1, FftSize / 2 - 1);
            int high = Math.Clamp((int)(highHz / binWidth), low, FftSize / 2 - 1);
            float centre = MathF.Sqrt(lowHz * highHz);
            ranges[band] = (low, high, 3f * MathF.Log2(Math.Max(centre, 200f) / 200f));
        }

        return ranges;
    }

    /// <summary>Scales so a track's loud passages sit near the top of the bars.</summary>
    private static void Normalise(float[] levels, int frames, int bands)
    {
        var peaks = new List<float>(frames);
        for (int frame = 0; frame < frames; frame++)
        {
            float peak = 0;
            for (int band = 0; band < bands; band++)
                peak = Math.Max(peak, levels[frame * bands + band]);

            peaks.Add(peak);
        }

        peaks.Sort();
        float loud = peaks[Math.Min(frames - 1, (int)(frames * 0.98f))];
        if (loud <= 0.01f)
            return;

        float scale = Math.Clamp(0.95f / loud, 0.5f, 4f);
        for (int i = 0; i < levels.Length; i++)
            levels[i] = Math.Min(1f, levels[i] * scale);
    }

    /// <summary>In-place iterative radix-2 FFT.</summary>
    private static void Fft(float[] real, float[] imaginary)
    {
        int n = real.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;

            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        for (int length = 2; length <= n; length <<= 1)
        {
            float angle = -2 * MathF.PI / length;
            float stepReal = MathF.Cos(angle);
            float stepImaginary = MathF.Sin(angle);
            for (int start = 0; start < n; start += length)
            {
                float wr = 1;
                float wi = 0;
                for (int k = 0; k < length / 2; k++)
                {
                    int even = start + k;
                    int odd = even + length / 2;
                    float tr = real[odd] * wr - imaginary[odd] * wi;
                    float ti = real[odd] * wi + imaginary[odd] * wr;
                    real[odd] = real[even] - tr;
                    imaginary[odd] = imaginary[even] - ti;
                    real[even] += tr;
                    imaginary[even] += ti;
                    (wr, wi) = (wr * stepReal - wi * stepImaginary, wr * stepImaginary + wi * stepReal);
                }
            }
        }
    }
}
