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
        Assert.That(_store.Query<DashTrail>().Count, Is.Zero, "a dash leaves a streak, not a line");
    }

    [Test]
    public void ADashLeavesAStreakAlongItsPath()
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

        ParticleBurst streak = _store.Query<ParticleBurst>().ToEntityList()
            .Single(entity => entity.GetComponent<ParticleBurst>().Kind == ParticleKind.DashStreak)
            .GetComponent<ParticleBurst>();
        Assert.That(streak.Position.X, Is.EqualTo(375).Within(0.01f), "centred between 500 and 250");
        Assert.That(streak.Area.X, Is.EqualTo(250).Within(0.01f), "as long as the dash");
        Assert.That(Mathf.Abs(streak.Angle), Is.EqualTo(Mathf.Pi).Within(0.001f), "lying along a leftward dash");
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

    private ref Position PlaceAt(Vector2 at)
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = at;
        _player.GetComponent<PhysicsBody>().Body.SetTransform(Box2DPhysicsWorld.ToPhysics(at), 0f);
        return ref position;
    }

    private void Dash(Vector2 at, DashDirection direction, long timeMs = 0, DashDirection exit = DashDirection.Up, float exitMomentum = 0f)
    {
        _store.CreateEntity(new Note(
            timeMs,
            at,
            target: KeybindAction.L3,
            kind: NoteKind.Dash,
            dashDirection: direction,
            exitDirection: exit,
            exitMomentum: exitMomentum));
        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    [Test]
    public void InTheAirADashIsMeasuredFromTheNote()
    {
        ref Position position = ref PlaceAt(new Vector2(940, 480));

        Dash(new Vector2(950, 500), DashDirection.Left);

        Assert.That(position.Value.X, Is.EqualTo(700).Within(0.01f));
        Assert.That(position.Value.Y, Is.EqualTo(500).Within(0.01f));
    }

    [Test]
    public void OnTheFloorADashIsMeasuredFromStandingHeight()
    {
        // The note sits below where the player's centre can go; measured from
        // it, a short dash up would end inside the floor and do nothing.
        ref Position position = ref PlaceAt(new Vector2(500, 950));

        Dash(new Vector2(510, 990), DashDirection.Up);

        Assert.That(position.Value.X, Is.EqualTo(510).Within(0.01f));
        Assert.That(position.Value.Y, Is.EqualTo(700).Within(0.01f));
    }

    [Test]
    public void OnABlockADashIsMeasuredFromTheBlockTop()
    {
        _store.CreateEntity(new Block(new Vector2(400, 800), new Vector2(200, 50)));
        ref Position position = ref PlaceAt(new Vector2(500, 750));

        Dash(new Vector2(500, 790), DashDirection.Up);

        Assert.That(position.Value.Y, Is.EqualTo(500).Within(0.01f));
    }

    [Test]
    public void ALateHitStartsThatFarIntoTheDashesFollowThrough()
    {
        _player.GetComponent<Playhead>().TimeMs = 40;
        _player.GetComponent<PhysicsBody>().Body.SetLinearVelocity(new System.Numerics.Vector2(4f, 0f));
        ref Position position = ref PlaceAt(new Vector2(500, 500));

        Dash(new Vector2(500, 500), DashDirection.Up);

        // 4 m/s for 40 ms is 16 pixels; the hang means gravity has not had that time.
        Assert.That(position.Value.X, Is.EqualTo(516).Within(0.01f));
        Assert.That(position.Value.Y, Is.EqualTo(250).Within(0.01f));
    }

    [Test]
    public void ADashWithNoExitHangs()
    {
        PlaceAt(new Vector2(500, 500));

        Dash(new Vector2(500, 500), DashDirection.Up);

        Body body = _player.GetComponent<PhysicsBody>().Body;
        Assert.That(_player.HasComponent<DashHang>(), Is.True);
        Assert.That(Box2DPhysicsWorld.GravityScale(body), Is.Zero);
    }

    [Test]
    public void AnExitPushLaunchesOutOfTheDashInItsOwnDirection()
    {
        PlaceAt(new Vector2(500, 500));

        Dash(new Vector2(500, 500), DashDirection.Up, exit: DashDirection.Right, exitMomentum: 600f);

        Body body = _player.GetComponent<PhysicsBody>().Body;
        float speed = 600f / Box2DPhysicsWorld.PixelsPerMeter * Box2DPhysicsWorld.MomentumScale;
        Assert.That(body.LinearVelocity.X, Is.EqualTo(speed).Within(0.001f));
        Assert.That(body.LinearVelocity.Y, Is.Zero);
        Assert.That(_player.HasComponent<DashHang>(), Is.False);
        Assert.That(Box2DPhysicsWorld.GravityScale(body), Is.EqualTo(1f));
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
    [TestCase(0.5f, 0f, DashDirection.Right, 1f, 0f)]
    [TestCase(8f, 0f, DashDirection.Right, 8f, 0f)]
    [TestCase(-8f, 5f, DashDirection.Right, 1f, 5f)]
    [TestCase(3f, 12f, DashDirection.Up, 3f, -1f)]
    [TestCase(3f, -4f, DashDirection.Up, 3f, -4f)]
    public void AMomentumNoteSetsItsSpeedWithoutStacking(
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
    public void ADiagonalMomentumNoteSetsEachAxisItPushesAlong()
    {
        var note = new Note(
            0,
            Vector2.Zero,
            kind: NoteKind.Momentum,
            dashDirection: DashDirection.UpRight,
            momentum: 100f / Box2DPhysicsWorld.MomentumScale);
        float axis = new Vector2(1f, -1f).Normalized().X;

        System.Numerics.Vector2 velocity = PlayerMovementSystem.MomentumVelocity(
            new System.Numerics.Vector2(-8f, 12f),
            note);

        Assert.That(velocity.X, Is.EqualTo(axis).Within(0.0001f));
        Assert.That(velocity.Y, Is.EqualTo(-axis).Within(0.0001f));
    }

    [Test]
    public void TwoWeakMomentumNotesGiveNoMoreSpeedThanOne()
    {
        var note = new Note(
            0,
            Vector2.Zero,
            kind: NoteKind.Momentum,
            dashDirection: DashDirection.Right,
            momentum: 200f);

        System.Numerics.Vector2 once = PlayerMovementSystem.MomentumVelocity(System.Numerics.Vector2.Zero, note);
        System.Numerics.Vector2 twice = PlayerMovementSystem.MomentumVelocity(once, note);

        Assert.That(twice, Is.EqualTo(once));
    }

    [Test]
    public void MomentumSpeedScalesWithTheAuthoredMagnitude()
    {
        var note = new Note(
            0,
            Vector2.Zero,
            kind: NoteKind.Momentum,
            dashDirection: DashDirection.Right,
            momentum: 600f);

        Assert.That(
            PlayerMovementSystem.MomentumSpeed(note),
            Is.EqualTo(6f * Box2DPhysicsWorld.MomentumScale).Within(0.0001f));
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
