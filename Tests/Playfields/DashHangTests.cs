using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

/// <summary>The hang after a dash: held, then easing gravity back in, counted from the note.</summary>
[TestFixture]
public sealed class DashHangTests
{
    [Test]
    public void TheHangIsAQuarterBeatHeldAndAQuarterBeatEasing()
    {
        DashHang hang = DashHangSystem.For(TempoMap.Single(120, 0), 0, 0);

        Assert.That(hang.HoldMs, Is.EqualTo(125f));
        Assert.That(hang.EaseMs, Is.EqualTo(125f));
    }

    [Test]
    public void VeryFastAndSlowSongsAreHeldToSaneLengths()
    {
        Assert.That(DashHangSystem.For(TempoMap.Single(400, 0), 0, 0).HoldMs, Is.EqualTo(DashHangSystem.MinHoldMs));
        Assert.That(DashHangSystem.For(TempoMap.Single(40, 0), 0, 0).HoldMs, Is.EqualTo(DashHangSystem.MaxHoldMs));
    }

    [TestCase(-30f, 0f)]
    [TestCase(0f, 0f)]
    [TestCase(250f, 0f)]
    [TestCase(375f, 0.5f)]
    public void GravityIsOffWhileHeldThenEasesBack(float elapsed, float expected)
    {
        var hang = new DashHang(0, 250f, 250f);

        Assert.That(DashHangSystem.GravityScale(hang, elapsed), Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase(500f)]
    [TestCase(-5_000f)]
    public void TheHangEndsOnceOverOrWhenThePlayheadJumpsBack(float elapsed)
    {
        Assert.That(DashHangSystem.GravityScale(new DashHang(0, 250f, 250f), elapsed), Is.Null);
    }
}
