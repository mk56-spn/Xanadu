using Godot;
using NUnit.Framework;
using Writer.Animation;

namespace Writer.Tests.Animation;

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
