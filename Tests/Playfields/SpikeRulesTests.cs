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
    public void ATileIsOneGridCell()
    {
        Spike spike = SpikeRules.TileAt(new Vector2(350, 640), Field);

        Assert.That(spike.Size, Is.EqualTo(new Vector2(50, 50)));
        Assert.That(spike.Position, Is.EqualTo(new Vector2(350, 600)));
        Assert.That(spike.Bounds, Is.EqualTo(new Rect2(350, 600, 50, 50)));
    }

    [Test]
    public void TilesAreKeptInsideTheField()
    {
        Spike spike = SpikeRules.TileAt(new Vector2(1000, 1000), Field);

        Assert.That(spike.Position, Is.EqualTo(new Vector2(950, 950)));
    }

    [Test]
    public void MovingATileSnapsItToTheGrid()
    {
        var spike = new Spike(new Vector2(300, 600), new Vector2(100, 100));

        Spike moved = SpikeRules.Move(spike, new Vector2(140, 25), Field);

        Assert.That(moved.Position, Is.EqualTo(new Vector2(400, 600)));
        Assert.That(moved.Size, Is.EqualTo(new Vector2(100, 100)));
    }

    [Test]
    public void MovingATileOffTheFieldClampsIt()
    {
        var spike = new Spike(Vector2.Zero, new Vector2(100, 100));

        Spike moved = SpikeRules.Move(spike, new Vector2(-400, -400), Field);

        Assert.That(moved.Position, Is.EqualTo(Vector2.Zero));
    }

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

    [TestCase(1, 300, 350, TestName = "A quarter turn points right")]
    [TestCase(2, 250, 400, TestName = "A half turn points down")]
    [TestCase(3, 200, 350, TestName = "Three quarters point left")]
    public void TurningMovesTheTipToThatEdge(int turns, float tipX, float tipY)
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000));
        var spike = new Spike(new Vector2(200, 300), new Vector2(100, 100), turns);

        SpikeTriangle triangle = SpikeRules.ScreenTriangle(spike, view);

        Assert.That(triangle.Tip, Is.EqualTo(new Vector2(tipX, tipY)));
    }

    [Test]
    public void MovingATileKeepsItsTurn()
    {
        var spike = new Spike(new Vector2(300, 600), new Vector2(100, 100), 2);

        Assert.That(SpikeRules.Move(spike, new Vector2(100, 0), Field).QuarterTurns, Is.EqualTo(2));
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
