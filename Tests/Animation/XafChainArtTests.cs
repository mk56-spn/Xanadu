using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core.Layering;
using Xanadu.Core.Maths;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class RigChainArtTests
{
    private static readonly Vector2 Art = new(14, 26);

    [Test]
    public void APieceIsCentredOnItsSegment()
    {
        Transform2D transform = RigChainArt.Segment(new Vector2(10, 20), new Vector2(10, 28), 0, 10, 9f, Art);

        Assert.That(transform.Origin, Is.EqualTo(new Vector2(10, 24)));
    }

    [Test]
    public void ALongSideRunsAlongTheSegmentAndIsStretchedToItsLengthTimesTheOverlap()
    {
        Vector2 from = new(0, 0);
        Vector2 to = new(6, 8);

        Transform2D transform = RigChainArt.Segment(from, to, 0, 10, 9f, Art);

        // Art's Y axis (long side) is the segment's direction, 10 long, stretched to 15.
        Assert.That(transform.Y.Normalized().DistanceTo(new Vector2(0.6f, 0.8f)), Is.LessThan(0.0001f));
        Assert.That(transform.Y.Length() * Art.Y, Is.EqualTo(10f * RigChainArt.Overlap).Within(0.001f));
        Assert.That(transform.X.Dot(transform.Y), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ThePieceIsNeverMirrored()
    {
        foreach (Vector2 to in new[]
                 {
                     new Vector2(0, 8), new Vector2(8, 0), new Vector2(0, -8), new Vector2(-8, 0), new Vector2(5, -5)
                 })
        {
            Transform2D transform = RigChainArt.Segment(Vector2.Zero, to, 0, 4, 9f, Art);
            Assert.That(transform.Determinant(), Is.GreaterThan(0f), $"{to}");
        }
    }

    [Test]
    public void TheStrandNarrowsFromTheRootToTheTip()
    {
        float root = RigChainArt.WidthAt(10f, 0, 10);
        float middle = RigChainArt.WidthAt(10f, 5, 10);
        float tip = RigChainArt.WidthAt(10f, 9, 10);

        Assert.That(root, Is.LessThan(10f));
        Assert.That(root, Is.GreaterThan(middle));
        Assert.That(middle, Is.GreaterThan(tip));
        Assert.That(tip, Is.GreaterThan(10f * RigChainArt.TipWidth - 0.01f));

        Transform2D first = RigChainArt.Segment(Vector2.Zero, new Vector2(0, 8), 0, 10, 10f, Art);
        Transform2D last = RigChainArt.Segment(Vector2.Zero, new Vector2(0, 8), 9, 10, 10f, Art);
        Assert.That(first.X.Length(), Is.GreaterThan(last.X.Length()));
    }

    [Test]
    public void ChainsDrawOneLayerAboveTheHighestAttachmentSoTheyAlwaysCoverTheBody()
    {
        var package = new RigPackage();
        Assert.That(RigChainArt.Layer(package), Is.EqualTo(1));

        package.Attachments.Add(new RigAttachment { ZOrder = -3 });
        Assert.That(RigChainArt.Layer(package), Is.EqualTo(1), "never below the default layer");

        package.Attachments.Add(new RigAttachment { ZOrder = 4 });
        package.Attachments.Add(new RigAttachment { ZOrder = 2 });
        Assert.That(RigChainArt.Layer(package), Is.EqualTo(5));

        package.Attachments.Add(new RigAttachment { ZOrder = DrawOrder.MaxLayer });
        Assert.That(RigChainArt.Layer(package), Is.EqualTo(DrawOrder.MaxLayer));
    }

    [Test]
    public void AZeroLengthSegmentStaysFiniteInsteadOfBreakingTheTransform()
    {
        Transform2D transform = RigChainArt.Segment(new Vector2(3, 3), new Vector2(3, 3), 0, 1, 9f, Art);

        Assert.That(float.IsFinite(transform.X.X) && float.IsFinite(transform.Y.Y), Is.True);
        Assert.That(transform.Origin, Is.EqualTo(new Vector2(3, 3)));
    }
}
