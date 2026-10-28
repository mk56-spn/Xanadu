using Godot;
using NUnit.Framework;
using Xanadu.Animation;

namespace Xanadu.Tests.Animation;

[TestFixture]
public sealed class XafVerletTests
{
    [Test]
    public void ChainKeepsSegmentLengthsWhileFalling()
    {
        var chain = new XafVerletChain(
            4,
            new Vector2(0, 0),
            Vector2.Right,
            10);

        for (int i = 0; i < 120; i++)
            chain.Step(1f / 60f);

        Assert.That(chain.Points[0], Is.EqualTo(Vector2.Zero));
        for (int i = 1; i < chain.Points.Length; i++)
        {
            Assert.That(
                chain.Points[i].DistanceTo(chain.Points[i - 1]),
                Is.EqualTo(10f).Within(0.05f));
        }
        Assert.That(chain.Points[^1].Y, Is.GreaterThan(0f));
    }

    [Test]
    public void AChainLandingOnAColliderRestsOnItAndNeverEntersIt()
    {
        // Slightly off-centre, as a hairline above a shoulder is: dead centre has no side to fall to.
        var chain = new XafVerletChain(6, new Vector2(4, -40), Vector2.Down, 10)
        {
            ColliderCenter = new Vector2(0, 0),
            ColliderRadius = 12,
        };

        for (int i = 0; i < 600; i++)
        {
            chain.Step(1f / 60f);
            for (int p = 1; p < chain.Points.Length; p++)
                Assert.That(chain.Points[p].Length(), Is.GreaterThanOrEqualTo(12f - 0.001f), $"step {i} point {p}");
        }

        // It hangs from above the collider and falls off one side, so the tip ends up below the top of it.
        Assert.That(chain.Points[^1].Y, Is.GreaterThan(-12f));
    }

    [Test]
    public void ADroppedPointSlidesOffAColliderInsteadOfBeingThrown()
    {
        var chain = new XafVerletChain(1, new Vector2(3, -20), Vector2.Down, 20)
        {
            ColliderCenter = Vector2.Zero,
            ColliderRadius = 6,
            Damping = 1f,
        };

        for (int i = 0; i < 240; i++)
            chain.Step(1f / 60f);

        // Pushed out of the circle it ends up resting by it, not flung far away.
        Assert.That(chain.Points[1].DistanceTo(chain.Points[0]), Is.EqualTo(20f).Within(0.05f));
        Assert.That(chain.Points[1].Length(), Is.GreaterThanOrEqualTo(6f - 0.001f));
        Assert.That(chain.Points[1].Length(), Is.LessThan(40f));
    }

    [Test]
    public void ARadiusOfZeroLeavesThePointsAlone()
    {
        var withCollider = new XafVerletChain(4, Vector2.Zero, Vector2.Down, 10) { ColliderRadius = 0 };
        var without = new XafVerletChain(4, Vector2.Zero, Vector2.Down, 10);

        for (int i = 0; i < 60; i++)
        {
            withCollider.Step(1f / 60f);
            without.Step(1f / 60f);
        }

        Assert.That(withCollider.Points[^1], Is.EqualTo(without.Points[^1]));
    }

    [Test]
    public void PendulumStaysOnItsConstraintCircle()
    {
        var pendulum = new XafPendulum(
            new Vector2(0, 0),
            new Vector2(10, 0),
            10);

        for (int i = 0; i < 120; i++)
            pendulum.Step(1f / 60f);

        Assert.That(pendulum.Position.Length(),
            Is.EqualTo(10f).Within(0.05f));
        Assert.That(pendulum.Position.Y, Is.GreaterThan(0f));
    }
}
