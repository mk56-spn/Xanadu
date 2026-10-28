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

        Assert.That(spike.Size, Is.EqualTo(new Vector2(100, 100)));
        Assert.That(spike.Position, Is.EqualTo(new Vector2(300, 600)));
        Assert.That(spike.Bounds, Is.EqualTo(new Rect2(300, 600, 100, 100)));
    }

    [Test]
    public void TilesAreKeptInsideTheField()
    {
        Spike spike = SpikeRules.TileAt(new Vector2(1000, 1000), Field);

        Assert.That(spike.Position, Is.EqualTo(new Vector2(900, 900)));
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
}
