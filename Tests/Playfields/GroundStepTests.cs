using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Playfields;

/// <summary>A ground hit carries the player a short step towards the note's side.</summary>
[TestFixture]
public sealed class GroundStepTests
{
    private const float Standing = 950f;

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Box2DPhysicsWorld _physics = null!;
    private Entity _player;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _physics = new Box2DPhysicsWorld();
        _root.Add(new GroundStepSystem());
    }

    [TearDown]
    public void TearDown() => _physics.Dispose();

    private void Player(float y, float verticalSpeed = 0f)
    {
        Body body = _physics.CreateDynamicBox(new Vector2(500, y), new Vector2(30, 100));
        body.SetLinearVelocity(new Vector2(0f, verticalSpeed));
        _player = _store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, y)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new Playhead(0, 120, 0, true),
            new PhysicsBody(body));
    }

    private void Hit(float noteX, long noteTimeMs = 0) =>
        GroundStepSystem.OnHit(_player, noteTimeMs, noteTimeMs, noteX, 500f);

    private float RunStep()
    {
        _player.GetComponent<Playhead>().TimeMs = 1_000;
        _root.Update(new UpdateTick(1f / 60f, 0));
        return _player.GetComponent<Position>().Value.X;
    }

    [Test]
    public void AGroundHitStepsTowardsTheNotesSide()
    {
        Player(Standing);
        Hit(620f);

        Assert.That(RunStep(), Is.EqualTo(500f + GroundStepSystem.Distance).Within(0.01f));
        Assert.That(_player.HasComponent<GroundStep>(), Is.False, "the step is done");
    }

    [Test]
    public void NotesOnOneSideAtOneTimeStepOnce()
    {
        Player(Standing);
        Hit(380f);
        Hit(420f);

        Assert.That(RunStep(), Is.EqualTo(500f - GroundStepSystem.Distance).Within(0.01f));
    }

    [Test]
    public void NotesOnBothSidesAtOneTimeCancelTheStep()
    {
        Player(Standing);
        Hit(380f);
        Hit(620f);

        Assert.That(RunStep(), Is.EqualTo(500f).Within(0.01f));
    }

    [Test]
    public void InTheAirAHitDoesNotStep()
    {
        Player(500f);
        Hit(620f);

        Assert.That(_player.HasComponent<GroundStep>(), Is.False);
    }

    [Test]
    public void ANoteStraightAboveDoesNotStep()
    {
        Player(Standing);
        Hit(500f);

        Assert.That(_player.HasComponent<GroundStep>(), Is.False);
    }
}
