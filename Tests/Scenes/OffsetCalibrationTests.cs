using System.Linq;
using NUnit.Framework;
using Xanadu.Systems.Scenes;
using Xanadu.Timeline;

namespace Xanadu.Tests.Scenes;

[TestFixture]
public sealed class OffsetCalibrationTests
{
    private static double ClickAt(int beat) =>
        OffsetCalibration.FirstClickSeconds + beat * OffsetCalibration.BeatSeconds;

    /// <summary>A run with a tap on every counted click, this many milliseconds after it.</summary>
    private static OffsetCalibration Run(System.Func<int, double> lateMs)
    {
        var run = new OffsetCalibration();
        for (int beat = OffsetCalibration.CountInBeats; beat < OffsetCalibration.TotalBeats; beat++)
            run.Tap(ClickAt(beat) + lateMs(beat) / 1000d);
        return run;
    }

    [Test]
    public void TappingDeadOnTheClickStillAllowsForTheLatencyChartsHaveBakedIn()
    {
        OffsetMeasurement result = Run(_ => 0d).Measure()!.Value;

        Assert.That(result.OffsetMs, Is.EqualTo(ChartTiming.AudioLagMs).Within(0.001d));
        Assert.That(result.SpreadMs, Is.EqualTo(0d).Within(0.001d));
    }

    [TestCase(35d)]
    [TestCase(-40d)]
    public void TheOffsetIsHowLateTheTapsAre(double late) =>
        Assert.That(
            Run(_ => late).Measure()!.Value.OffsetMs,
            Is.EqualTo(late + ChartTiming.AudioLagMs).Within(0.001d));

    [Test]
    public void StrayTapsDoNotMoveTheResult()
    {
        OffsetMeasurement result = Run(beat => beat % 8 == 0 ? 180d : 30d + beat % 3).Measure()!.Value;

        Assert.That(result.OffsetMs, Is.EqualTo(31d + ChartTiming.AudioLagMs).Within(1.01d));
        Assert.That(result.Rejected, Is.EqualTo(4));
        Assert.That(result.Used, Is.EqualTo(OffsetCalibration.MeasuredBeats - 4));
    }

    [Test]
    public void AVerySteadyRunKeepsItsSmallWobbles()
    {
        OffsetMeasurement result = Run(beat => beat % 2 == 0 ? 10d : 18d).Measure()!.Value;

        Assert.That(result.Rejected, Is.Zero);
    }

    [Test]
    public void UncertaintyFallsWithMoreTaps()
    {
        OffsetMeasurement result = Run(beat => beat % 2 == 0 ? 0d : 20d).Measure()!.Value;

        Assert.That(result.SpreadMs, Is.EqualTo(10d).Within(0.01d));
        Assert.That(result.UncertaintyMs, Is.EqualTo(10d / System.Math.Sqrt(32)).Within(0.01d));
    }

    [Test]
    public void TheCountInDoesNotCount()
    {
        var run = new OffsetCalibration();

        Assert.That(run.Tap(ClickAt(0)), Is.False);
        Assert.That(run.Tap(ClickAt(OffsetCalibration.CountInBeats - 1)), Is.False);
        Assert.That(run.Tap(ClickAt(OffsetCalibration.CountInBeats)), Is.True);
    }

    [Test]
    public void OnlyTheFirstTapOnAClickCountsAndNothingPastTheEnd()
    {
        var run = new OffsetCalibration();
        double click = ClickAt(OffsetCalibration.CountInBeats);

        Assert.That(run.Tap(click), Is.True);
        Assert.That(run.Tap(click + 0.05d), Is.False);
        Assert.That(run.Tap(ClickAt(OffsetCalibration.TotalBeats)), Is.False);
        Assert.That(run.TapCount, Is.EqualTo(1));
    }

    [Test]
    public void ATapBetweenClicksGoesToTheNearestOne()
    {
        var run = new OffsetCalibration();
        run.Tap(ClickAt(OffsetCalibration.CountInBeats + 1) - 0.1d);

        Assert.That(run.Taps.Single(), Is.EqualTo(-100d + ChartTiming.AudioLagMs).Within(0.001d));
    }

    [Test]
    public void NoTapsNoResult() =>
        Assert.That(new OffsetCalibration().Measure(), Is.Null);

    [Test]
    public void TheClickTrackAssetHasAClickWhereTheTimingSaysAndIsLongEnough()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            TestContext.CurrentContext.TestDirectory, "../../../../"));
        byte[] wav = System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "Audio", "offset-calibration.wav"));

        const int rate = 44100;
        const int data = 44;
        short Sample(double seconds) =>
            System.BitConverter.ToInt16(wav, data + (int)System.Math.Round(seconds * rate) * 2);
        double Loudest(double seconds) => Enumerable.Range(0, 40)
            .Max(i => System.Math.Abs((int)Sample(seconds + i / (double)rate)));

        Assert.That((wav.Length - data) / 2 / (double)rate,
            Is.GreaterThan(ClickAt(OffsetCalibration.TotalBeats - 1)));
        for (int beat = 0; beat < OffsetCalibration.TotalBeats; beat++)
        {
            Assert.That(Loudest(ClickAt(beat)), Is.GreaterThan(10_000), $"click {beat}");
            Assert.That(Loudest(ClickAt(beat) - 0.002d), Is.LessThan(10_000), $"silent before click {beat}");
        }
    }
}
