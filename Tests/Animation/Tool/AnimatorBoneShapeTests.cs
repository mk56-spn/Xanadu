using Godot;
using NUnit.Framework;
using Writer.Tools.Animator;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorBoneShapeTests
{
    private static Vector2[] Kite(Vector2 from, Vector2 to)
    {
        var points = new Vector2[4];
        Assert.That(AnimatorBoneShape.Kite(from, to, points), Is.True);
        return points;
    }

    [Test]
    public void TheKiteStartsAtTheParentJointAndEndsAtTheChildJoint()
    {
        Vector2[] kite = Kite(new Vector2(10, 20), new Vector2(10, -60));

        Assert.That(kite[0], Is.EqualTo(new Vector2(10, 20)));
        Assert.That(kite[2], Is.EqualTo(new Vector2(10, -60)));
    }

    [Test]
    public void TheShouldersSitNearTheParentAndAreSymmetricAboutTheBoneAxis()
    {
        Vector2 from = new(0, 0);
        Vector2 to = new(0, -100);

        Vector2[] kite = Kite(from, to);

        // 15% of the way along the bone, equally far to each side.
        Assert.That(kite[1].Y, Is.EqualTo(-15f).Within(0.001f));
        Assert.That(kite[3].Y, Is.EqualTo(-15f).Within(0.001f));
        Assert.That(kite[1].X, Is.EqualTo(-kite[3].X).Within(0.001f));
        Assert.That(System.Math.Abs(kite[1].X), Is.GreaterThan(0f));
    }

    [Test]
    public void TheKiteWorksInAnyDirection()
    {
        Vector2[] kite = Kite(new Vector2(0, 0), new Vector2(60, 80));
        Vector2 axis = new Vector2(60, 80).Normalized();

        Vector2 shoulderMid = (kite[1] + kite[3]) / 2;
        Assert.That(shoulderMid.Normalized().DistanceTo(axis), Is.LessThan(0.001f));
        Assert.That((kite[1] - kite[3]).Dot(axis), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void LongBonesStopGrowingWiderAndShortOnesStayNarrowerThanTheyAreLong()
    {
        float longHalf = System.Math.Abs(Kite(Vector2.Zero, new Vector2(0, -2000))[1].X);
        float midHalf = System.Math.Abs(Kite(Vector2.Zero, new Vector2(0, -15))[1].X);
        float scaled = System.Math.Abs(Kite(Vector2.Zero, new Vector2(0, -40))[1].X);
        float tinyHalf = System.Math.Abs(Kite(Vector2.Zero, new Vector2(0, -4))[1].X);

        Assert.That(longHalf, Is.EqualTo(AnimatorBoneShape.MaxHalfWidth));
        Assert.That(midHalf, Is.EqualTo(AnimatorBoneShape.MinHalfWidth));
        Assert.That(scaled, Is.EqualTo(5.6f).Within(0.001f));
        Assert.That(tinyHalf, Is.EqualTo(2f));
    }

    [Test]
    public void BonesTooShortToSeeProduceNoShape()
    {
        var points = new Vector2[4];

        Assert.That(AnimatorBoneShape.Kite(Vector2.Zero, new Vector2(0.5f, 0), points), Is.False);
        Assert.That(AnimatorBoneShape.Kite(Vector2.Zero, Vector2.Zero, points), Is.False);
        Assert.That(AnimatorBoneShape.Kite(Vector2.Zero, new Vector2(0, 50), new Vector2[3]), Is.False);
    }
}
