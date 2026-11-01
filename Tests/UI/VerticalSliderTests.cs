using NUnit.Framework;
using Xanadu.Ecs;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class VerticalSliderTests
{
    private static readonly VerticalSlider Slider = new(100, 300, 0.25f, 10);

    [Test]
    public void TheThumbShowsTheShareOnScreen() =>
        Assert.That(Slider.ThumbLength, Is.EqualTo(50f));

    [Test]
    public void TheThumbNeverShrinksPastItsMinimumOrOutgrowsTheTrack()
    {
        Assert.That((Slider with { Shown = 0.001f }).ThumbLength, Is.EqualTo(10f));
        Assert.That((Slider with { Shown = 3f }).ThumbLength, Is.EqualTo(200f));
    }

    [Test]
    public void ValueRunsTheThumbFromTopToBottom()
    {
        Assert.That(Slider.ThumbTop(0f), Is.EqualTo(100f));
        Assert.That(Slider.ThumbTop(1f), Is.EqualTo(250f));
        Assert.That(Slider.ThumbTop(2f), Is.EqualTo(250f));
    }

    [Test]
    public void GrabbingTheThumbKeepsItWhereItIs()
    {
        float grab = Slider.Grab(120f, 0f);

        Assert.That(grab, Is.EqualTo(20f));
        Assert.That(Slider.ValueAt(120f, grab), Is.EqualTo(0f));
    }

    [Test]
    public void PressingTheBareTrackCentresTheThumbOnThePointer()
    {
        float grab = Slider.Grab(200f, 0f);

        Assert.That(grab, Is.EqualTo(25f));
        Assert.That(Slider.ThumbTop(Slider.ValueAt(200f, grab)), Is.EqualTo(175f).Within(0.001f));
    }

    [Test]
    public void DraggingPastEitherEndStops()
    {
        Assert.That(Slider.ValueAt(-500f, 0f), Is.EqualTo(0f));
        Assert.That(Slider.ValueAt(900f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void AThumbFillingTheTrackCannotSlide()
    {
        VerticalSlider full = Slider with { Shown = 1f };

        Assert.That(full.CanSlide, Is.False);
        Assert.That(full.ValueAt(200f, 0f), Is.EqualTo(0f));
    }
}
