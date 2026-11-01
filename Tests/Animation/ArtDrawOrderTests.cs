using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class ArtDrawOrderTests
{
    [Test]
    public void AHigherLayerDrawsOverAnythingOnTheLayerBelow()
    {
        Assert.That(ArtDrawOrder.Of(1, 0), Is.GreaterThan(ArtDrawOrder.Of(0, 99_999)));
        Assert.That(ArtDrawOrder.Of(0, 0), Is.GreaterThan(ArtDrawOrder.Of(-1, 99_999)));
    }

    [Test]
    public void TheOrderInsideALayerStillApplies()
    {
        Assert.That(ArtDrawOrder.Of(3, 20_000), Is.GreaterThan(ArtDrawOrder.Of(3, 10_000)));
    }

    [Test]
    public void TheWholeLayerRangeFitsADrawIndex()
    {
        Assert.That(ArtDrawOrder.Of(XafAttachment.MinLayer, 0), Is.EqualTo(0));
        Assert.That(ArtDrawOrder.Of(XafAttachment.MaxLayer, 99_999), Is.LessThan(int.MaxValue));
    }
}
