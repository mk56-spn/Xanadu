using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Timeline;

namespace Xanadu.Tests.UI;

/// <summary>
/// The timeline input every tool shares: picking points and span parts,
/// dragging spans, and the held, edge-panned window while dragging.
/// </summary>
[TestFixture]
public sealed class TimelineInputTests
{
    // 1000 px over 10 s: 100 px a second, starting at x = 100.
    private static readonly TimelineAxis Axis = new(new Rect2(100, 0, 1000, 20), 0, 10_000);

    private static double Free(double ms) => ms;

    private static double ToSecond(double ms) => System.Math.Round(ms / 1000.0) * 1000.0;

    [Test]
    public void NearestPicksTheClosestPointInReach()
    {
        double[] times = { 1000, 1100, 5000 };
        Assert.That(TimelineGrab.Nearest(Axis, times, 208f, 9f), Is.EqualTo(1));
        Assert.That(TimelineGrab.Nearest(Axis, times, 203f, 9f), Is.EqualTo(0));
        Assert.That(TimelineGrab.Nearest(Axis, times, 400f, 9f), Is.EqualTo(-1));
    }

    [Test]
    public void ASpanTakesItsEndsNearThemAndItsBodyBetween()
    {
        // 2 s to 6 s: x 300 to 700.
        Assert.That(TimelineGrab.Span(Axis, 2000, 6000, 303f, 6f), Is.EqualTo(TimelineGrabPart.Start));
        Assert.That(TimelineGrab.Span(Axis, 2000, 6000, 696f, 6f), Is.EqualTo(TimelineGrabPart.End));
        Assert.That(TimelineGrab.Span(Axis, 2000, 6000, 500f, 6f), Is.EqualTo(TimelineGrabPart.Body));
        Assert.That(TimelineGrab.Span(Axis, 2000, 6000, 295f, 6f), Is.EqualTo(TimelineGrabPart.Start), "just outside still grabs");
        Assert.That(TimelineGrab.Span(Axis, 2000, 6000, 280f, 6f), Is.Null);
    }

    [Test]
    public void ASpanTooNarrowForGripsIsOnlyMovedWhole()
    {
        // 2 s to 2.1 s: 10 px wide.
        Assert.That(TimelineGrab.Span(Axis, 2000, 2100, 301f, 7f, 21f), Is.EqualTo(TimelineGrabPart.Body));
        Assert.That(TimelineGrab.Span(Axis, 2000, 2000, 300f, 7f, 21f), Is.EqualTo(TimelineGrabPart.Body), "an instant span can still be grabbed");
    }

    [Test]
    public void DraggingTheBodyKeepsTheLengthAndFollowsThePointer()
    {
        var drag = new TimelineSpanDrag(TimelineGrabPart.Body, 2000, 6000, 3000);
        Assert.That(drag.Apply(4400, Free), Is.EqualTo((3400d, 7400d)));
        Assert.That(drag.Apply(4400, ToSecond), Is.EqualTo((3000d, 7000d)), "snaps where the span starts");
        Assert.That(drag.Apply(500, Free), Is.EqualTo((0d, 4000d)), "kept inside the low end");
        Assert.That(drag.Apply(9000, Free, highMs: 8000), Is.EqualTo((4000d, 8000d)), "kept inside the high end");
    }

    [Test]
    public void DraggingAnEndNeverPassesTheOther()
    {
        var start = new TimelineSpanDrag(TimelineGrabPart.Start, 2000, 6000, 2000);
        Assert.That(start.Apply(3000, Free), Is.EqualTo((3000d, 6000d)));
        Assert.That(start.Apply(9000, Free, minLengthMs: 10), Is.EqualTo((5990d, 6000d)));

        var end = new TimelineSpanDrag(TimelineGrabPart.End, 2000, 6000, 6000);
        Assert.That(end.Apply(1000, Free), Is.EqualTo((2000d, 2000d)));
    }

    [Test]
    public void AHeldWindowPansOnlyPastTheDeadZone()
    {
        var view = new TimelineView(4f);
        TimelineScrub.Hold(ref view, new TimeWindow(2000, 4000));
        var area = new Rect2(0, 0, 1000, 20);

        Assert.That(TimelineScrub.Pan(ref view, area, 600f, 0.1f, 10_000), Is.False, "inside the dead zone");
        Assert.That(view.ScrubStartMs, Is.EqualTo(2000));

        Assert.That(TimelineScrub.Pan(ref view, area, 1000f, 0.5f, 10_000), Is.True);
        Assert.That(view.ScrubStartMs, Is.GreaterThan(2000));
        Assert.That(view.ScrubEndMs - view.ScrubStartMs, Is.EqualTo(2000), "the window keeps its span");

        TimelineScrub.Release(ref view);
        Assert.That(view.Scrubbing, Is.False);
        Assert.That(TimelineScrub.Pan(ref view, area, 1000f, 0.5f, 10_000), Is.False, "a released window does not pan");
    }
}
