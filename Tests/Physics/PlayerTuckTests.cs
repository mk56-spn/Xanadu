using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using NVector2 = System.Numerics.Vector2;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class PlayerTuckTests
{
    private static readonly Vector2 Size = new(60, 180);

    /// <summary>A 1:1 playfield with a block whose top is at y = 500, spanning x 0..400.</summary>
    private static (Entity Player, PlayfieldView View, Playfield Field) Setup()
    {
        var store = new EntityStore();
        store.CreateEntity(new Block(new Vector2(0, 500), new Vector2(400, 100)));
        var view = new PlayfieldView(Vector2.Zero, 1f, new Vector2(2000, 2000));
        var field = new Playfield(2000, 2000);
        Entity player = store.CreateEntity(new Player(Size), view, field);
        return (player, view, field);
    }

    [Test]
    public void FullLengthFeetReachingTheTopLand()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup();
        // Full feet went from 490 to 520: past the top, still within a 40 px tuck.
        var centre = new Vector2(200, 520 - Size.Y / 2);
        Assert.That(PlayerGround.TryLanding(player, view, field, centre, 490, 40, out float standing), Is.True);
        Assert.That(standing, Is.EqualTo(500 - Size.Y / 2));
    }

    [Test]
    public void ComingFromBesideTheBlockDoesNotLand()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup();
        var centre = new Vector2(200, 520 - Size.Y / 2);
        Assert.That(PlayerGround.TryLanding(player, view, field, centre, 515, 40, out _), Is.False);
    }

    [Test]
    public void PassingFurtherThanTheTuckDoesNotLand()
    {
        (Entity player, PlayfieldView view, Playfield field) = Setup();
        var centre = new Vector2(200, 560 - Size.Y / 2);
        Assert.That(PlayerGround.TryLanding(player, view, field, centre, 490, 40, out _), Is.False);
    }

    [Test]
    public void ARaisedBottomKeepsTheTopAndTheMass()
    {
        using var physics = new Box2DPhysicsWorld();
        var body = physics.CreateDynamicBox(new Vector2(100, 100), Size, mass: 1f);
        Box2DPhysicsWorld.RaiseBottom(body, Size, 40);

        var box = body.FixtureList[0].GetAABB(0);
        Assert.That(box.LowerBound.Y * Box2DPhysicsWorld.PixelsPerMeter, Is.EqualTo(100 - Size.Y / 2).Within(1.5f)); // Box2D pads the AABB by its 1 px polygon skin.
        Assert.That(box.UpperBound.Y * Box2DPhysicsWorld.PixelsPerMeter, Is.EqualTo(100 + Size.Y / 2 - 40).Within(1.5f)); // Box2D pads the AABB by its 1 px polygon skin.
        Assert.That(body.Mass, Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void TheOneWayCheckUsesTheRaisedBox()
    {
        var surface = new OneWaySurface(BlockOneWay.Up, new NVector2(2f, 0.5f));
        // Raised bottom at 4.5 m: the full box would reach 4.9 m, inside the block whose top is 4.5 m.
        var mover = new OneWayMover(new NVector2(0.3f, 0.7f), new NVector2(0, -0.2f));
        Assert.That(OneWayContacts.Collides(surface, new NVector2(0, 5f), mover, new NVector2(0, 4f)), Is.True);
    }
}
