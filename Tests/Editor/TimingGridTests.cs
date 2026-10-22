using NUnit.Framework;
using Writer.Levels;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class TimingGridTests
{
    [Test]
    public void BeatAndSubdivisionUseBpm()
    {
        Assert.That(TimingGrid.BeatMs(120), Is.EqualTo(500));
        Assert.That(TimingGrid.TickMs(120, 4), Is.EqualTo(125));
        Assert.That(TimingGrid.TickMs(120, 0), Is.EqualTo(500));
        Assert.That(TimingGrid.BeatMs(0), Is.EqualTo(60_000));
    }

    [Test]
    public void WindowTicksAccountForOffset()
    {
        double tickMs = TimingGrid.TickMs(120, 4);
        Assert.That(TimingGrid.FirstTick(1_000, 250, tickMs), Is.EqualTo(6));
        Assert.That(TimingGrid.LastTick(1_000, 250, tickMs), Is.EqualTo(6));
        Assert.That(TimingGrid.TickTime(6, 250, tickMs), Is.EqualTo(1_000));
    }
}
