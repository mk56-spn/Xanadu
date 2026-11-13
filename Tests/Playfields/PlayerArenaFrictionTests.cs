using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
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

    /// <summary>A player at <paramref name="y"/> moving sideways; <paramref name="grip"/> is whether a friction bar has turned the ground's grip on.</summary>
    private Body Player(float y, float speed, bool grip = false)
    {
        Body body = _physics.CreateDynamicBox(new Vector2(500, y), new Vector2(30, 100));
        body.SetLinearVelocity(new Vector2(speed, 0f));
        _store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, y)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new PhysicsBody(body),
            new PlayerFriction { On = grip });
        return body;
    }

    private void Run(int frames, float delta)
    {
        for (int i = 0; i < frames; i++)
            _root.Update(new UpdateTick(delta, 0));
    }

    [Test]
    public void AnUpwardLaunchLeavesTheFloor()
    {
        Body body = Player(Floor, 0f);
        body.SetLinearVelocity(new Vector2(3f, -10f));

        Run(1, 1f / 60f);

        Assert.That(body.LinearVelocity.Y, Is.EqualTo(-10f));
        Assert.That(body.LinearVelocity.X, Is.EqualTo(3f));

        for (int i = 0; i < 10; i++)
        {
            _physics.Step(1f / 60f);
            Run(1, 1f / 60f);
        }

        Assert.That(
            Box2DPhysicsWorld.ToScreen(body.GetPosition()).Y,
            Is.LessThan(Floor - 50f));
    }

    [Test]
    public void TheFloorStopsAFall()
    {
        Body body = Player(Floor, 0f);
        body.SetLinearVelocity(new Vector2(0f, 10f));

        Run(1, 1f / 60f);

        Assert.That(body.LinearVelocity.Y, Is.Zero);
    }

    [Test]
    public void TheCeilingStopsARiseButLetsADownwardPushThrough()
    {
        Body body = Player(50f, 0f);
        body.SetLinearVelocity(new Vector2(0f, -10f));

        Run(1, 1f / 60f);

        Assert.That(body.LinearVelocity.Y, Is.Zero);

        body.SetLinearVelocity(new Vector2(0f, 10f));

        Run(1, 1f / 60f);

        Assert.That(body.LinearVelocity.Y, Is.EqualTo(10f));
    }

    [TestCase(30, 1f / 60f)]
    [TestCase(120, 1f / 240f)]
    public void TheFloorSlowsASlideTheSameAtAnyFrameRate(int frames, float delta)
    {
        Body body = Player(Floor, 5f, grip: true);

        Run(frames, delta);

        float expected = 5f - PlayerArenaSystem.GroundFriction * Box2DPhysicsWorld.DefaultGravity * 0.5f;
        Assert.That(body.LinearVelocity.X, Is.EqualTo(expected).Within(0.01f));
    }

    [TestCase(1f)]
    [TestCase(-1f)]
    public void ASlideStopsWithoutReversing(float speed)
    {
        Body body = Player(Floor, speed, grip: true);

        Run(120, 1f / 60f);

        Assert.That(body.LinearVelocity.X, Is.Zero);
    }

    [Test]
    public void WithoutGripASlideKeepsItsSpeed()
    {
        Body body = Player(Floor, 5f);

        Run(120, 1f / 60f);

        Assert.That(body.LinearVelocity.X, Is.EqualTo(5f));
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

        onBlock.SetLinearVelocity(new Vector2(5f, 0f));
        for (int i = 0; i < 120; i++)
            _physics.Step(Box2DPhysicsWorld.FixedTimeStep);

        float floor = 5f - PlayerArenaSystem.GroundFriction * Box2DPhysicsWorld.DefaultGravity * 0.5f;
        Assert.That(onBlock.LinearVelocity.X, Is.EqualTo(floor).Within(0.25f));
    }
}
