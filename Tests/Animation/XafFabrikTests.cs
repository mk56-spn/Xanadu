using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafFabrikTests
{
    [Test]
    public void ReachesATargetWithinChainLength()
    {
        Vector2[] joints =
        {
            new(0, 0),
            new(10, 0),
            new(20, 0),
        };

        Vector2[] result = XafFabrik.Solve(
            joints,
            new Vector2(10, 10),
            bendPositive: true);

        Assert.That(result[^1].DistanceTo(new Vector2(10, 10)),
            Is.LessThan(0.05f));
        Assert.That(result[0], Is.EqualTo(joints[0]));
    }

    [Test]
    public void PreservesSegmentLengths()
    {
        Vector2[] joints =
        {
            new(0, 0),
            new(10, 0),
            new(20, 0),
        };
        float first = joints[0].DistanceTo(joints[1]);
        float second = joints[1].DistanceTo(joints[2]);

        Vector2[] result = XafFabrik.Solve(
            joints,
            new Vector2(5, 8),
            bendPositive: false);

        Assert.That(result[0].DistanceTo(result[1]),
            Is.EqualTo(first).Within(0.02f));
        Assert.That(result[1].DistanceTo(result[2]),
            Is.EqualTo(second).Within(0.02f));
    }

    [Test]
    public void StretchesTowardUnreachableTarget()
    {
        Vector2[] joints =
        {
            new(0, 0),
            new(10, 0),
        };

        Vector2[] result = XafFabrik.Solve(
            joints,
            new Vector2(100, 0),
            bendPositive: true);

        Assert.That(result[^1], Is.EqualTo(new Vector2(10, 0)));
    }

    [Test]
    public void BendDirectionChoosesTheOppositeElbow()
    {
        Vector2[] joints =
        {
            new(0, 0),
            new(10, 0),
            new(20, 0),
        };
        Vector2 target = new(10, 8);

        Vector2 positive = XafFabrik.Solve(joints, target, true)[1];
        Vector2 negative = XafFabrik.Solve(joints, target, false)[1];

        Assert.That(positive.Y, Is.GreaterThan(negative.Y));
    }
}
