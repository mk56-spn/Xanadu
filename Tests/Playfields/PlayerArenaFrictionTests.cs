using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

/// <summary>The arena floor slows a sliding player, as a block does.</summary>
[TestFixture]
public sealed class PlayerArenaFrictionTests
{
    private const float Floor = 950f;

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Box2DPhysicsWorld _physics = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _physics = new Box2DPhysicsWorld();
        _root.Add(new PlayerArenaSystem());
    }

    [TearDown]
    public void TearDown() => _physics.Dispose();

    private Body Player(float y, float speed)
    {
        Body body = _physics.CreateDynamicBox(new Vector2(500, y), new Vector2(30, 100));
        body.SetLinearVelocity(new NVector2(speed, 0f));
        _store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, y)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new PhysicsBody(body));
        return body;
    }

    private void Run(int frames, float delta)
    {
        for (int i = 0; i < frames; i++)
            _root.Update(new UpdateTick(delta, 0));
    }

    [TestCase(30, 1f / 60f)]
    [TestCase(120, 1f / 240f)]
    public void TheFloorSlowsASlideTheSameAtAnyFrameRate(int frames, float delta)
    {
        Body body = Player(Floor, 5f);

        Run(frames, delta);

        float expected = 5f - PlayerArenaSystem.GroundFriction * Box2DPhysicsWorld.DefaultGravity * 0.5f;
        Assert.That(body.LinearVelocity.X, Is.EqualTo(expected).Within(0.01f));
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void ASlideStopsWithoutReversing(float speed)
    {
        Body body = Player(Floor, speed);

        Run(120, 1f / 60f);

        Assert.That(body.LinearVelocity.X, Is.Zero);
    }

    [Test]
    public void ThePlayerKeepsItsSpeedInTheAir()
    {
        Body body = Player(500f, 5f);

        Run(30, 1f / 60f);

        Assert.That(body.LinearVelocity.X, Is.EqualTo(5f));
    }

    [Test]
    public void TheFloorGripsLikeABlock()
    {
        _physics.CreateStaticBox(new Vector2(500, 2_000), new Vector2(100_000, 100));
        Body onBlock = _physics.CreateDynamicBox(
            new Vector2(0, 1_900), new Vector2(30, 100), friction: 0.15f);
        for (int i = 0; i < 120; i++)
            _physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        onBlock.SetLinearVelocity(new NVector2(5f, 0f));
        for (int i = 0; i < 120; i++)
            _physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        float floor = 5f - PlayerArenaSystem.GroundFriction * Box2DPhysicsWorld.DefaultGravity * 0.5f;
        Assert.That(onBlock.LinearVelocity.X, Is.EqualTo(floor).Within(0.25f));
    }
}
