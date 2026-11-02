using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class SpikeRulesTests
{
    private static readonly Playfield Field = new(1000, 1000);

    [Test]
    public void ScreenBoundsFollowTheView()
    {
        var view = new PlayfieldView(
            new Vector2(100, 50),
            0.5f,
            new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100));

        Assert.That(
            SpikeRules.ScreenBounds(spike, view),
            Is.EqualTo(new Rect2(new Vector2(200, 200), new Vector2(50, 50))));
    }

    [Test]
    public void TheTipSitsMidwayAlongTheTopOfTheTile()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100));

        SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);

        Assert.That(triangle.Tip, Is.EqualTo(new Vector2(250, 300)));
        Assert.That(triangle.BaseLeft, Is.EqualTo(new Vector2(200, 400)));
        Assert.That(triangle.BaseRight, Is.EqualTo(new Vector2(300, 400)));
    }

    [TestCase(90f, 300, 350, TestName = "A quarter turn points right")]
    [TestCase(180f, 250, 400, TestName = "A half turn points down")]
    [TestCase(270f, 200, 350, TestName = "Three quarters point left")]
    public void TurningMovesTheTipToThatEdge(float degrees, float tipX, float tipY)
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100), degrees);

        SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);

        Assert.That(triangle.Tip.IsEqualApprox(new Vector2(tipX, tipY)), Is.True);
    }

    [Test]
    public void ASpikeTakesAnyAngle()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100), 45f);

        SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);

        float reach = 50f * Mathf.Sqrt2 / 2f;
        Assert.That(triangle.Tip.IsEqualApprox(new Vector2(250 + reach, 350 - reach)), Is.True);
    }

    [Test]
    public void ATurnedSpikeIsPickedByItsTurnedBox()
    {
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 20), 90f);

        Assert.That(spike.Contains(new Vector2(250, 270)), Is.True, "stands upright now");
        Assert.That(spike.Contains(new Vector2(205, 310)), Is.False, "was inside before the turn");
    }

    [Test]
    public void TheHitboxSitsInsideTheDrawnSpike()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100));

        SpikeTriangle hitbox = SpikeRules.ScreenHitbox(spike, view);

        Assert.That(hitbox.Tip.Y, Is.GreaterThan(300));
        Assert.That(hitbox.Tip.X, Is.EqualTo(250).Within(0.01f));
        Assert.That(hitbox.BaseLeft.X, Is.GreaterThan(200));
        Assert.That(hitbox.BaseRight.X, Is.LessThan(300));
        Assert.That(hitbox.BaseLeft.Y, Is.LessThan(400));
    }

    [TestCase(45, 45, 10, 10, true, TestName = "Box around the tip")]
    [TestCase(45, 90, 10, 5, true, TestName = "Box wholly inside")]
    [TestCase(0, 0, 20, 20, false, TestName = "Bounding box only, by the slanted edge")]
    [TestCase(80, 0, 20, 20, false, TestName = "Other top corner")]
    [TestCase(120, 50, 10, 10, false, TestName = "Beside it")]
    [TestCase(45, 100, 10, 10, false, TestName = "Only touching the base")]
    public void TriangleOverlap(float x, float y, float width, float height, bool expected)
    {
        var triangle = new SpikeTriangle(
            new Vector2(50, 0),
            new Vector2(0, 100),
            new Vector2(100, 100));

        Assert.That(
            triangle.Overlaps(new Rect2(x, y, width, height)),
            Is.EqualTo(expected));
    }
}
