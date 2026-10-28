using System;
using System.Linq;
using NUnit.Framework;
using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class SpectrumAnalyzerTests
{
    private const int Rate = 22050;

    [TestCase(200f)]
    [TestCase(1000f)]
    [TestCase(5000f)]
    public void ASineLightsTheBandItsFrequencyFallsIn(float hertz)
    {
        float[] samples = Sine(hertz, seconds: 1);

        SpectrumData data = SpectrumAnalyzer.Analyze(samples, Rate, bands: 48);

        var levels = new float[48];
        data.Sample(0.5, levels);
        int loudest = Array.IndexOf(levels, levels.Max());

        // Log-spaced bands from 50 Hz to the top: where the tone should land.
        float top = 10000f;
        int expected = (int)(48 * MathF.Log(hertz / 50f) / MathF.Log(top / 50f));
        Assert.That(loudest, Is.EqualTo(expected).Within(1));
        Assert.That(levels[loudest], Is.GreaterThan(0.5f));
    }

    [Test]
    public void SilenceStaysAtZero()
    {
        SpectrumData data = SpectrumAnalyzer.Analyze(new float[Rate], Rate);

        var levels = new float[48];
        data.Sample(0.3, levels);

        Assert.That(levels, Is.All.EqualTo(0f));
    }

    [Test]
    public void ShortAudioHasNoFramesAndSamplingIsSafe()
    {
        SpectrumData data = SpectrumAnalyzer.Analyze(new float[100], Rate);

        var levels = new float[48];
        data.Sample(1, levels);

        Assert.That(data.FrameCount, Is.Zero);
        Assert.That(levels, Is.All.EqualTo(0f));
    }

    [Test]
    public void SamplingPastTheEndHoldsTheLastFrame()
    {
        SpectrumData data = SpectrumAnalyzer.Analyze(Sine(1000f, seconds: 1), Rate);

        var last = new float[48];
        var beyond = new float[48];
        data.Sample(0.99, last);
        data.Sample(500, beyond);

        Assert.That(beyond, Is.EqualTo(last).Within(0.2f));
    }

    private static float[] Sine(float hertz, float seconds)
    {
        var samples = new float[(int)(Rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.8f * MathF.Sin(2 * MathF.PI * hertz * i / Rate);

        return samples;
    }
}
