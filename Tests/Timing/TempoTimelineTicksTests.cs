using System.Linq;
using NUnit.Framework;
using Xanadu.Levels;
using Xanadu.Timeline;

namespace Xanadu.Tests.Timing;

/// <summary>The song grid shows more of a beat as the view zooms in, whatever the snap is.</summary>
[TestFixture]
public sealed class TempoTimelineTicksTests
{
    // 120 BPM: a beat every 500 ms.
    private static TimelineAxis Axis(double spanMs, float width = 1000) => new(new Rect2(0, 0, width, 20), 0, spanMs);

    [Test]
    public void ZoomedOutItDrawsOnlyBeats()
    {
        // 30 s over 1000 px: about 17 px a beat, so every beat shows but a half beat would be only 8 px.
        var ticks = TempoTimelineTicks.For(Axis(30_000), TempoMap.Default);
        Assert.That(ticks.All(tick => tick.Level == TimelineTickLevel.Major), Is.True);
        Assert.That(ticks.First(tick => tick.TimeMs > 0).TimeMs, Is.EqualTo(500).Within(0.01));
    }

    [Test]
    public void ZoomingInAddsHalvesThenQuartersThenFinerLines()
    {
        // 5 s: 100 px a beat, so halves (50 px), quarters (25 px) and eighths (12.5 px) fit.
        var ticks = TempoTimelineTicks.For(Axis(5_000), TempoMap.Default);
        Assert.That(ticks.Any(tick => tick.Level == TimelineTickLevel.Half), Is.True);
        Assert.That(ticks.Any(tick => tick.Level == TimelineTickLevel.Quarter), Is.True);
        Assert.That(ticks.Any(tick => tick.Level == TimelineTickLevel.Minor), Is.True);
        Assert.That(TempoTimelineTicks.Division(100f), Is.EqualTo(8));
    }

    [Test]
    public void FarOutTheBeatsThinSoTheyNeverBecomeAWall()
    {
        // 120 s: about 4 px a beat, so only every fourth beat is drawn.
        var ticks = TempoTimelineTicks.For(Axis(120_000), TempoMap.Default);
        Assert.That(ticks[1].TimeMs - ticks[0].TimeMs, Is.GreaterThanOrEqualTo(2_000 - 0.01));
    }

    [Test]
    public void ATimelineWithNoWidthDrawsNothing() =>
        Assert.That(TempoTimelineTicks.For(Axis(5_000, width: 0), TempoMap.Default), Is.Empty);
}
