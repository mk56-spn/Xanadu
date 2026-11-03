using System.Linq;
using System.Collections.Generic;
using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayerMovementSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Box2DPhysicsWorld _physics = null!;
    private FakeKeyboard _keyboard = null!;
    private Entity _player;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _physics = new Box2DPhysicsWorld();
        _keyboard = new FakeKeyboard();

        var keybinds = new KeybindSettings(persist: false);
        _root.Add(new PlayerMovementSystem(_store, keybinds, _keyboard));
        _root.Add(new PlayerArenaSystem());

        Body body = _physics.CreateDynamicBox(
            new Vector2(500, 500),
            new Vector2(30, 100));

        _player = _store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(500, 500)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playfield(1_000, 1_000),
            new Playhead(0, 120, 0, true),
            new PhysicsBody(body),
            new Momentum(System.Numerics.Vector2.Zero));
    }

    [TearDown]
    public void TearDown() => _physics.Dispose();

    [Test]
    public void DashesLeftFromThePlayer()
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));

        Entity movement = _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left));
        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(250));
        Assert.That(movement.IsNull, Is.True);
        Entity trail = _store.Query<DashTrail>().ToEntityList()[0];
        Assert.That(trail.GetComponent<DashTrail>().From.X, Is.EqualTo(500));
        Assert.That(trail.GetComponent<DashTrail>().To.X, Is.EqualTo(250));
    }

    [Test]
    public void ADashSetsOffASprayWhereItEnds()
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left));
        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        // The note's own sparks pop where it stood; the spray is the one on the body.
        Entity burst = _store.Query<ParticleBurst>().ToEntityList()
            .Single(entity => entity.GetComponent<ParticleBurst>().Kind == ParticleKind.Dash);
        Assert.That(burst.GetComponent<ParticleBurst>().Area, Is.EqualTo(_player.GetComponent<Player>().Size));
        Assert.That(burst.GetComponent<ParticleBurst>().Kind, Is.EqualTo(ParticleKind.Dash));
        Assert.That(burst.GetComponent<ParticleBurst>().Position, Is.EqualTo(_player.GetComponent<Position>().Value));
    }

    [Test]
    public void DashesFromThePlayerPositionInsteadOfTheNoteCenter()
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = new Vector2(940, 480);
        _player.GetComponent<PhysicsBody>().Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(position.Value),
            0f);
        _store.CreateEntity(new Note(
            0,
            new Vector2(950, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(position.Value.X, Is.EqualTo(690).Within(0.01f));
        Assert.That(position.Value.Y, Is.EqualTo(480).Within(0.01f));
    }

    [Test]
    public void TopIsCapped()
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = new Vector2(500, 50);
        _player.GetComponent<PhysicsBody>().Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(position.Value),
            0f);
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 50),
            target: KeybindAction.M,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Up));

        _keyboard.Press(Key.Space);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(position.Value.Y, Is.EqualTo(50));
    }

    [Test]
    public void AKeyThatCanHitATrueNoteDoesNotDash()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500)));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left));

        _keyboard.Press(Key.Space);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));
    }

    [TestCase(12f)]
    [TestCase(-12f)]
    public void ADashClearsVerticalVelocityAndKeepsHorizontal(float vertical)
    {
        Box2DSharp.Dynamics.Body body = _player.GetComponent<PhysicsBody>().Body;
        body.SetLinearVelocity(new System.Numerics.Vector2(4f, vertical));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Up));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(body.LinearVelocity.Y, Is.Zero);
        Assert.That(body.LinearVelocity.X, Is.EqualTo(4f));
    }

    [Test]
    public void AKeyThatCannotHitTheNoteStillDashes()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500)));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(250));
    }

    [Test]
    public void TheWrongKeyLeavesADashNoteAlone([Values(NoteKind.Dash, NoteKind.Momentum)] NoteKind kind)
    {
        Entity note = _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.M,
            kind: kind,
            dashDirection: DashDirection.Left));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(note.IsNull, Is.False);
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));
    }

    [Test]
    public void AMomentumNotePushesTheBodyInItsDirection()
    {
        Entity note = _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Momentum,
            dashDirection: DashDirection.Right,
            momentum: 600f));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(note.IsNull, Is.True);
        Assert.That(
            _player.GetComponent<PhysicsBody>().Body.LinearVelocity.X,
            Is.GreaterThan(0f));
    }

    [TestCase(-8f, 0f, DashDirection.Right, 1f, 0f)]
    [TestCase(8f, 0f, DashDirection.Right, 9f, 0f)]
    [TestCase(-8f, 5f, DashDirection.Right, 1f, 5f)]
    [TestCase(3f, 12f, DashDirection.Up, 3f, -1f)]
    public void AMomentumNoteZeroesOpposingSpeedThenPushes(
        float startX,
        float startY,
        DashDirection direction,
        float expectedX,
        float expectedY)
    {
        Box2DSharp.Dynamics.Body body = _player.GetComponent<PhysicsBody>().Body;
        body.SetLinearVelocity(new System.Numerics.Vector2(startX, startY));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Momentum,
            dashDirection: direction,
            momentum: 100f / Box2DPhysicsWorld.MomentumScale));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(body.LinearVelocity.X, Is.EqualTo(expectedX).Within(0.001f));
        Assert.That(body.LinearVelocity.Y, Is.EqualTo(expectedY).Within(0.001f));
    }

    [Test]
    public void ADiagonalMomentumNoteZeroesEachOpposingAxis()
    {
        Assert.That(
            PlayerMovementSystem.WithoutOpposingSpeed(
                new System.Numerics.Vector2(-8f, 12f),
                new Vector2(1f, -1f).Normalized()),
            Is.EqualTo(System.Numerics.Vector2.Zero));
    }

    [Test]
    public void MomentumImpulseScalesWithTheAuthoredMagnitude()
    {
        var note = new Note(
            0,
            Vector2.Zero,
            kind: NoteKind.Momentum,
            dashDirection: DashDirection.Right,
            momentum: 600f);

        System.Numerics.Vector2 impulse =
            PlayerMovementSystem.MomentumImpulse(note, mass: 1f);

        Assert.That(impulse.X, Is.EqualTo(6f * Box2DPhysicsWorld.MomentumScale).Within(0.0001f));
        Assert.That(impulse.Y, Is.EqualTo(0f).Within(0.0001f));
    }

    [TestCase(DashStrength.Single, 250f)]
    [TestCase(DashStrength.Double, 125f)]
    [TestCase(DashStrength.Triple, 0f)]
    public void DashStrengthSetsTheDistanceTravelled(
        DashStrength strength,
        float expectedX)
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Left,
            dashStrength: strength));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(expectedX));
    }
}
