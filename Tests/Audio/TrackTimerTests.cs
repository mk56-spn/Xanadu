using NUnit.Framework;
using Writer.Systems.Audio;

namespace Writer.Tests.Audio;

[TestFixture]
public sealed class TrackTimerTests
{
    [Test]
    public void NormalSpeedTracksRealTime()
    {
        var timer = new TrackTimer();
        timer.Start(1_000_000);
        timer.Update(2_000_000, 100);

        Assert.That(timer.NowMs, Is.EqualTo(1000));
    }

    [Test]
    public void StartOffsetIsApplied()
    {
        var timer = new TrackTimer();
        timer.Start(1_000_000, offsetMicros: 250_000);

        Assert.That(timer.NowMs, Is.EqualTo(250));
    }

    [Test]
    public void HalfSpeedAdvancesHalfTime()
    {
        var timer = new TrackTimer();
        timer.Start(0);
        timer.Update(1_000_000, 50);

        Assert.That(timer.NowMs, Is.EqualTo(500));
    }

    [Test]
    public void DoubleSpeedAdvancesDoubleTime()
    {
        var timer = new TrackTimer();
        timer.Start(0);
        timer.Update(1_000_000, 200);

        Assert.That(timer.NowMs, Is.EqualTo(2000));
    }

    [Test]
    public void StoppedTimerReportsZero()
    {
        var timer = new TrackTimer();
        timer.Start(0);
        timer.Update(1_000_000, 100);
        timer.Stop();

        Assert.That(timer.NowMs, Is.EqualTo(0));
    }
}
