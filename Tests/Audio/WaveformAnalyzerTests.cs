using System;
using System.Linq;
using NUnit.Framework;
using Xanadu.Systems.Audio;

namespace Xanadu.Tests.Audio;

[TestFixture]
public sealed class WaveformAnalyzerTests
{
    private const int Rate = 22050;

    private static float[] Tone(float hz, float seconds, float gain = 1f) =>
        Enumerable.Range(0, (int)(seconds * Rate))
            .Select(i => gain * MathF.Sin(2f * MathF.PI * hz * i / Rate))
            .ToArray();

    /// <summary>A brickwalled track: a mid-range bed at nearly full scale, with a bass kick every half second.</summary>
    private static float[] LoudTrackWithKicks(float seconds)
    {
        float[] bed = Tone(1000f, seconds, 0.45f);
        float[] kick = Tone(60f, seconds, 0.45f);
        for (int i = 0; i < bed.Length; i++)
        {
            float sinceBeat = i / (float)Rate % 0.5f;
            bed[i] += sinceBeat < 0.06f ? kick[i] : 0f;
        }

        return bed;
    }

    [Test]
    public void AToneLandsInItsOwnBand()
    {
        WaveformData bass = WaveformAnalyzer.Analyze(Tone(60f, 1f), Rate);
        WaveformData treble = WaveformAnalyzer.Analyze(Tone(6000f, 1f), Rate);

        Assert.That(bass.Average(WaveformBand.Low, 300, 700), Is.GreaterThan(0.5f));
        Assert.That(bass.Average(WaveformBand.High, 300, 700), Is.LessThan(0.05f));
        Assert.That(treble.Average(WaveformBand.High, 300, 700), Is.GreaterThan(0.5f));
        Assert.That(treble.Average(WaveformBand.Low, 300, 700), Is.LessThan(0.05f));
    }

    [Test]
    public void KicksInALoudMasterStandClearOfWhatIsBetweenThem()
    {
        WaveformData data = WaveformAnalyzer.Analyze(LoudTrackWithKicks(4f), Rate);

        float kick = data.Average(WaveformBand.Low, 1510, 1550);
        float between = data.Average(WaveformBand.Low, 1700, 1900);

        Assert.That(kick, Is.GreaterThan(0.7f));
        Assert.That(between, Is.LessThan(0.1f));
    }

    [Test]
    public void AQuietTrackFillsTheHeightLikeALoudOne()
    {
        float loud = WaveformAnalyzer.Analyze(Tone(1000f, 1f), Rate).Average(WaveformBand.Mid, 100, 900);
        float quiet = WaveformAnalyzer.Analyze(Tone(1000f, 1f, 0.05f), Rate).Average(WaveformBand.Mid, 100, 900);

        Assert.That(quiet, Is.EqualTo(loud).Within(0.02f));
    }

    [Test]
    public void AHitStandsOutOfASteadySoundOfTheSameLoudness()
    {
        // A steady mid tone with one short burst three times as loud.
        float[] samples = Tone(1000f, 3f, 0.25f);
        for (int i = (int)(1.5f * Rate); i < (int)(1.55f * Rate); i++)
            samples[i] *= 3f;

        WaveformData data = WaveformAnalyzer.Analyze(samples, Rate);

        Assert.That(
            data.Average(WaveformBand.Mid, 1505, 1545),
            Is.GreaterThan(data.Average(WaveformBand.Mid, 500, 1000) * 1.5f));
    }

    [TestCase(1_000)]
    [TestCase(170_000)]
    public void AClickShowsAtItsOwnTimeHoweverFarIntoTheTrack(int clickMs)
    {
        var samples = new float[Rate * 180];
        int start = (int)(clickMs / 1000d * Rate);
        for (int i = 0; i < 44; i++)
            samples[start + i] = 0.9f * MathF.Sin(2f * MathF.PI * 1000f * i / Rate);

        WaveformData data = WaveformAnalyzer.Analyze(samples, Rate);

        Assert.That(data.Average(WaveformBand.Mid, clickMs - 10, clickMs + 10), Is.GreaterThan(0.05f));
        Assert.That(data.Average(WaveformBand.Mid, clickMs - 30, clickMs - 10), Is.Zero);
        Assert.That(data.Average(WaveformBand.Mid, clickMs + 15, clickMs + 35), Is.Zero);
    }

    [Test]
    public void SilenceReadsAsNothing()
    {
        WaveformData data = WaveformAnalyzer.Analyze(new float[Rate], Rate);

        Assert.That(data.DurationMs, Is.EqualTo(1000));
        Assert.That(data.Average(WaveformBand.Low, 0, 1000), Is.Zero);
    }

    [Test]
    public void AveragingIsAccurateAcrossAndWithinSteps()
    {
        var data = new WaveformData(
            new[] { 0f, 1f, 0f, 1f },
            new float[4],
            new float[4],
            durationMs: 40);

        Assert.That(data.Average(WaveformBand.Low, 0, 40), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(data.Average(WaveformBand.Low, 10, 20), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(data.Average(WaveformBand.Low, 12, 14), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(data.Average(WaveformBand.Low, 15, 25), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void AveragingOutsideTheTrackCountsAsSilence()
    {
        var data = new WaveformData(new[] { 1f, 1f }, new float[2], new float[2], durationMs: 20);

        Assert.That(data.Average(WaveformBand.Low, 10, 30), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(data.Average(WaveformBand.Low, 30, 40), Is.Zero);
        Assert.That(data.Average(WaveformBand.Low, 10, 10), Is.Zero);
    }
}
