using Godot;
using NUnit.Framework;
using Xanadu.Timeline;

namespace Xanadu.Tests.Timeline;

[TestFixture]
public sealed class TimeWindowTests
{
    private static readonly Rect2 Area = new(100, 0, 400, 20);

    [Test]
    public void ZoomOneShowsTheWholeTimeline()
    {
        TimeWindow window = TimeWindow.Following(5_000, 10_000, 1f);

        Assert.That(window, Is.EqualTo(new TimeWindow(0, 10_000)));
    }

    [Test]
    public void ZoomedWindowFollowsThePlayheadAndStaysInsideTheTimeline()
    {
        Assert.That(TimeWindow.Following(5_000, 10_000, 4f),
            Is.EqualTo(new TimeWindow(3_750, 6_250)));
        Assert.That(TimeWindow.Following(0, 10_000, 4f),
            Is.EqualTo(new TimeWindow(0, 2_500)));
        Assert.That(TimeWindow.Following(10_000, 10_000, 4f),
            Is.EqualTo(new TimeWindow(7_500, 10_000)));
    }

    [Test]
    public void TimeAndXMapBothWaysAndClampToTheArea()
    {
        var window = new TimeWindow(1_000, 3_000);

        Assert.That(window.XForTime(Area, 1_000), Is.EqualTo(100f));
        Assert.That(window.XForTime(Area, 2_000), Is.EqualTo(300f));
        Assert.That(window.XForTime(Area, 3_000), Is.EqualTo(500f));
        Assert.That(window.XForTime(Area, 9_000), Is.EqualTo(500f));
        Assert.That(window.XForTime(Area, -5), Is.EqualTo(100f));
        Assert.That(window.TimeForX(Area, 300f), Is.EqualTo(2_000).Within(0.001));
        Assert.That(window.TimeForX(Area, 0f), Is.EqualTo(1_000).Within(0.001));
        Assert.That(window.TimeForX(Area, 900f), Is.EqualTo(3_000).Within(0.001));
    }

    [Test]
    public void AnEmptyTimelineDoesNotDivideByZero()
    {
        TimeWindow window = TimeWindow.Following(0, 0, 8f);

        Assert.That(window.Span, Is.GreaterThan(0));
        Assert.That(float.IsFinite(window.XForTime(Area, 0)), Is.True);
    }

    [Test]
    public void NiceTickStepsAreTheSmallestRoundStepThatFits()
    {
        // 500 px per second: 0.1 s is 50 px, 0.2 s is 100 px.
        Assert.That(TimelineTicks.NiceStepSeconds(500, 60), Is.EqualTo(0.2));
        Assert.That(TimelineTicks.NiceStepSeconds(500, 12), Is.EqualTo(0.05));
        Assert.That(TimelineTicks.NiceStepSeconds(10, 60), Is.EqualTo(10));
        Assert.That(TimelineTicks.NiceStepSeconds(0.0001, 60), Is.EqualTo(60));
    }

    [Test]
    public void ZoomLevelsStepAndClamp()
    {
        Assert.That(TimelineZoom.Step(1f, 1), Is.EqualTo(2f));
        Assert.That(TimelineZoom.Step(1f, -1), Is.EqualTo(1f));
        Assert.That(TimelineZoom.Step(32f, 1), Is.EqualTo(32f));
        Assert.That(TimelineZoom.ClosestForDuration(0), Is.EqualTo(1f));
    }
}
