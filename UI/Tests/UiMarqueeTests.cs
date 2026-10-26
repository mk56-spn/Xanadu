using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Ui;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiMarqueeTests
{
    private readonly UiMarquee<string> _marquee = new();

    [Test]
    public void AnArmedPressThatNeverMovesPastTheThresholdEndsAsAClick()
    {
        _marquee.Arm(new Vector2(10, 10));

        bool active = _marquee.Update(new Vector2(12, 11), out bool started);

        Assert.That(active, Is.False);
        Assert.That(started, Is.False);
        Assert.That(_marquee.End(), Is.EqualTo(MarqueeEnd.Click));
        Assert.That(_marquee.Start, Is.EqualTo(new Vector2(10, 10)));
    }

    [Test]
    public void DraggingPastTheThresholdTurnsAnArmedPressIntoABoxOnce()
    {
        _marquee.Arm(new Vector2(10, 10));

        bool first = _marquee.Update(new Vector2(30, 40), out bool startedFirst);
        bool second = _marquee.Update(new Vector2(50, 60), out bool startedSecond);

        Assert.That(first, Is.True);
        Assert.That(startedFirst, Is.True);
        Assert.That(second, Is.True);
        Assert.That(startedSecond, Is.False);
        Assert.That(_marquee.End(), Is.EqualTo(MarqueeEnd.Box));
    }

    [Test]
    public void TheThresholdIsConfigurable()
    {
        var wide = new UiMarquee<string>(threshold: 50f);
        wide.Arm(Vector2.Zero);

        Assert.That(wide.Update(new Vector2(30, 0), out _), Is.False);
        Assert.That(wide.Update(new Vector2(60, 0), out _), Is.True);
    }

    [Test]
    public void BoundsAreTheSameBoxWhicheverWayItIsDragged()
    {
        _marquee.Arm(new Vector2(100, 80));
        _marquee.Update(new Vector2(40, 120), out _);

        Assert.That(_marquee.Bounds, Is.EqualTo(new Rect2(40, 80, 60, 40)));
    }

    [Test]
    public void ABeginningBoxIsActiveAtOnceAndNeverStartsLater()
    {
        _marquee.Begin(new Vector2(5, 5), additive: false, new List<string>());

        bool active = _marquee.Update(new Vector2(6, 6), out bool started);

        Assert.That(active, Is.True);
        Assert.That(started, Is.False);
        Assert.That(_marquee.End(), Is.EqualTo(MarqueeEnd.Box));
    }

    [Test]
    public void AnAdditiveBoxKeepsWhatWasSelectedWhenItBegan()
    {
        _marquee.Begin(Vector2.Zero, additive: true, new[] { "a", "b" });

        List<string> selection = _marquee.Selection(new[] { "b", "c" });

        Assert.That(selection, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void APlainBoxReplacesTheSelectionWithWhatItCovers()
    {
        _marquee.Begin(Vector2.Zero, additive: false, new[] { "a" });

        Assert.That(_marquee.Selection(new[] { "c", "c", "d" }), Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public void ABoxDraggedFromAnArmedPressIsNeverAdditive()
    {
        _marquee.Arm(Vector2.Zero);
        _marquee.Update(new Vector2(40, 40), out _);

        Assert.That(_marquee.Additive, Is.False);
        Assert.That(_marquee.Selection(new[] { "x" }), Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void EndingWithoutAPressDoesNothing()
    {
        Assert.That(_marquee.End(), Is.EqualTo(MarqueeEnd.None));
        Assert.That(_marquee.Update(new Vector2(99, 99), out bool started), Is.False);
        Assert.That(started, Is.False);
    }

    [Test]
    public void CancellingDropsThePressWithoutItCountingAsAClick()
    {
        _marquee.Arm(Vector2.Zero);
        _marquee.Cancel();

        Assert.That(_marquee.End(), Is.EqualTo(MarqueeEnd.None));
    }

    [Test]
    public void ABoxCanBeStartedAgainAfterItEnds()
    {
        _marquee.Begin(Vector2.Zero, additive: true, new[] { "old" });
        _marquee.End();

        _marquee.Begin(new Vector2(3, 3), additive: false, new[] { "ignored" });

        Assert.That(_marquee.Selection(new[] { "new" }), Is.EqualTo(new[] { "new" }));
    }

    [Test]
    public void PublishingCopiesTheBoxIntoTheDrawComponent()
    {
        _marquee.Arm(new Vector2(10, 20));
        _marquee.Update(new Vector2(60, 90), out _);
        var box = new UiMarqueeBox();

        _marquee.Publish(ref box);

        Assert.That(box.Active, Is.True);
        Assert.That(box.Bounds, Is.EqualTo(new Rect2(10, 20, 50, 70)));
        _marquee.End();
        _marquee.Publish(ref box);
        Assert.That(box.Active, Is.False);
    }
}
