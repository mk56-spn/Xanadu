using NUnit.Framework;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.UI.Input;

[TestFixture]
public sealed class PointerLatchTests
{
    private static PointerInput Sample(PointerLatch latch) =>
        latch.Sample(Vector2.Zero, primary: false, secondary: false, middle: false, shift: false);

    [Test]
    public void AClickBetweenFramesIsSeenByEveryReadUntilTheFrameEnds()
    {
        var latch = new PointerLatch();
        latch.LatchPrimary();

        Assert.That(Sample(latch).Pressed, Is.True);
        Assert.That(Sample(latch).Pressed, Is.True);

        latch.EndFrame();
        Assert.That(Sample(latch).Pressed, Is.False);
    }

    [Test]
    public void SecondaryAndMiddleClicksAreLatchedToo()
    {
        var latch = new PointerLatch();
        latch.LatchSecondary();
        latch.LatchMiddle();

        Assert.That(Sample(latch).SecondaryPressed, Is.True);
        Assert.That(Sample(latch).Middle, Is.True);
    }

    [Test]
    public void BlockingHidesEveryButtonAndDropsTheWheel()
    {
        var latch = new PointerLatch { Blocked = true };
        latch.AddWheel(1, control: false, shift: false);

        PointerInput input = latch.Sample(
            Vector2.Zero, primary: true, secondary: true, middle: true, shift: false);

        Assert.That(input.Pressed, Is.False);
        Assert.That(input.SecondaryPressed, Is.False);
        Assert.That(input.Middle, Is.False);
        Assert.That(latch.Notches, Is.Zero);
    }

    [Test]
    public void NotchesAddUpAndTakingLeavesNone()
    {
        var latch = new PointerLatch();
        latch.AddWheel(1, control: false, shift: false);
        latch.AddWheel(1, control: true, shift: true);

        Assert.That(latch.Control, Is.True);
        Assert.That(latch.Shift, Is.True);
        Assert.That(latch.TakeWheel(), Is.EqualTo(2));
        Assert.That(latch.Notches, Is.Zero);
    }

    [Test]
    public void AnUntakenNotchDoesNotCarryIntoTheNextFrame()
    {
        var latch = new PointerLatch();
        latch.AddWheel(-1, control: false, shift: false);

        latch.EndFrame();

        Assert.That(latch.Notches, Is.Zero);
    }
}
