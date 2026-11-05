using NUnit.Framework;
using Xanadu.Layering;

namespace Xanadu.Tests.Layering;

[TestFixture]
public sealed class DrawOrderTests
{
    [Test]
    public void AHigherLayerDrawsOverAnythingOnTheLayerBelow()
    {
        Assert.That(DrawOrder.Banded(1, 0), Is.GreaterThan(DrawOrder.Banded(0, 99_999)));
        Assert.That(DrawOrder.Banded(0, 0), Is.GreaterThan(DrawOrder.Banded(-1, 99_999)));
    }

    [Test]
    public void TheOrderInsideALayerStillApplies()
    {
        Assert.That(DrawOrder.Banded(3, 20_000), Is.GreaterThan(DrawOrder.Banded(3, 10_000)));
    }

    [Test]
    public void TheWholeLayerRangeFitsADrawIndex()
    {
        Assert.That(DrawOrder.Banded(DrawOrder.MinLayer, 0), Is.EqualTo(0));
        Assert.That(DrawOrder.Banded(DrawOrder.MaxLayer, 99_999), Is.LessThan(int.MaxValue));
    }

    [Test]
    public void LayersOutsideTheRangeAreClamped()
    {
        Assert.That(DrawOrder.ClampLayer(5000), Is.EqualTo(DrawOrder.MaxLayer));
        Assert.That(DrawOrder.ClampLayer(-5000), Is.EqualTo(DrawOrder.MinLayer));
        Assert.That(DrawOrder.Banded(5000, 0), Is.EqualTo(DrawOrder.Banded(DrawOrder.MaxLayer, 0)));
    }

    [Test]
    public void SlotsOrderByLayerThenZThenIndex()
    {
        Assert.That(new DrawSlot(1, -50, 0), Is.GreaterThan(new DrawSlot(0, 50, 9)));
        Assert.That(new DrawSlot(0, 2, 0), Is.GreaterThan(new DrawSlot(0, 1, 9)));
        Assert.That(new DrawSlot(0, 1, 3), Is.GreaterThan(new DrawSlot(0, 1, 2)));
    }
}
