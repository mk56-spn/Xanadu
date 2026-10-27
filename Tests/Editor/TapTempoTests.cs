using NUnit.Framework;
using Xanadu.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class TapTempoTests
{
    [Test]
    public void ComputesBpmFromTapIntervals()
    {
        var tap = new TapTempo();

        Assert.That(tap.Tap(0), Is.Null);
        Assert.That(tap.Tap(500), Is.EqualTo(120));
        Assert.That(tap.Tap(1_000), Is.EqualTo(120));
    }

    [Test]
    public void LongGapResetsTapSequence()
    {
        var tap = new TapTempo();
        tap.Tap(0);
        tap.Tap(500);

        Assert.That(tap.Tap(3_000), Is.Null);
    }
}
