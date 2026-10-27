using System.Linq;
using Box2DSharp.Collision.Shapes;
using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Physics;

[TestFixture]
public sealed class PlayerBodySystemTests
{
    [Test]
    public void PlayerBodyWaitsForTheCountdownToStart()
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new PlayerBodySystem(physics));
        root.Add(new PhysicsStepSystem(physics));

        Entity status = store.CreateEntity(new Text("GET READY"));
        Entity player = store.CreateEntity(
            new Player(Player.DefaultSize),
            new Position(new Vector2(500, 300)),
            new Character(1f),
            new Momentum(System.Numerics.Vector2.Zero),
            new PlaySession(status));

        root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));

        Assert.That(player.HasComponent<PhysicsBody>(), Is.False);
        Assert.That(
            player.GetComponent<Position>().Value.Y,
            Is.EqualTo(300f));

        ref PlaySession session = ref player.GetComponent<PlaySession>();
        session.Started = true;

        root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));
        Assert.That(player.HasComponent<PhysicsBody>(), Is.True);

        for (int i = 0; i < 30; i++)
            root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));

        Assert.That(
            player.GetComponent<Position>().Value.Y,
            Is.GreaterThan(300f));
    }

    [Test]
    public void PlayerBodyUsesCharacterDimensionsAndFallsUnderGravity()
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var root = new SystemRoot(store);
        root.Add(new PlayerBodySystem(physics));
        root.Add(new PhysicsStepSystem(physics));

        Entity player = store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, 300)),
            new Character(1f),
            new Momentum(System.Numerics.Vector2.Zero));

        root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));
        Assert.That(player.HasComponent<PhysicsBody>(), Is.True);

        Body body = player.GetComponent<PhysicsBody>().Body;
        var shape = (PolygonShape)body.FixtureList[0].Shape;
        float width = (shape.Vertices.Max(v => v.X) - shape.Vertices.Min(v => v.X)) *
                      Box2DPhysicsWorld.PixelsPerMeter;
        float height = (shape.Vertices.Max(v => v.Y) - shape.Vertices.Min(v => v.Y)) *
                       Box2DPhysicsWorld.PixelsPerMeter;

        Assert.That(width, Is.EqualTo(30f).Within(0.1f));
        Assert.That(height, Is.EqualTo(100f).Within(0.1f));

        for (int i = 0; i < 30; i++)
            root.Update(new UpdateTick(Box2DPhysicsWorld.FixedTimeStep, 0));

        Assert.That(player.GetComponent<Position>().Value.Y, Is.GreaterThan(300f));
    }
}
