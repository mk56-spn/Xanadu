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
    public void TheDirectionKeyPicksTheWay()
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));

        Entity movement = _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash));
        Press(KeybindAction.Left);

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(250));
        Assert.That(movement.IsNull, Is.True);
        Assert.That(_store.Query<DashTrail>().Count, Is.Zero, "a dash leaves a streak, not a line");
    }

    [TestCase(KeybindAction.Up, 0f, -1f)]
    [TestCase(KeybindAction.Right, 1f, 0f)]
    [TestCase(KeybindAction.Down, 0f, 1f)]
    [TestCase(KeybindAction.Left, -1f, 0f)]
    public void EachDirectionKeySendsItsOwnWay(KeybindAction key, float x, float y)
    {
        PlaceAt(new Vector2(500, 500));
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash));
        Press(key);

        Vector2 moved = _player.GetComponent<Position>().Value - new Vector2(500, 500);
        Assert.That(moved.X, Is.EqualTo(x * PlayerMovementSystem.SingleDashDistance).Within(0.01f));
        Assert.That(moved.Y, Is.EqualTo(y * PlayerMovementSystem.SingleDashDistance).Within(0.01f));
    }

    [TestCase(KeybindAction.Up, DashDirection.UpRight)]
    [TestCase(KeybindAction.Right, DashDirection.DownRight)]
    [TestCase(KeybindAction.Down, DashDirection.DownLeft)]
    [TestCase(KeybindAction.Left, DashDirection.UpLeft)]
    public void ARhombusTurnsThePressedWayClockwise(KeybindAction key, DashDirection expected)
    {
        var note = new Note(0, Vector2.Zero, kind: NoteKind.Dash, diagonal: true);
        Assert.That(NoteRules.Heading(note, key), Is.EqualTo(expected));
    }

    [Test]
    public void ARhombusDashGoesDiagonally()
    {
        PlaceAt(new Vector2(500, 500));
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash, diagonal: true));
        Press(KeybindAction.Up);

        float step = PlayerMovementSystem.SingleDashDistance / Mathf.Sqrt(2f);
        Vector2 position = _player.GetComponent<Position>().Value;
        Assert.That(position.X, Is.EqualTo(500 + step).Within(0.01f));
        Assert.That(position.Y, Is.EqualTo(500 - step).Within(0.01f));
    }

    [Test]
    public void ADashLeavesAStreakAlongItsPath()
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash));
        Press(KeybindAction.Left);

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
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash));
        Press(KeybindAction.Left);

        // The note's own sparks pop where it stood; the spray is the one on the body.
        Entity burst = _store.Query<ParticleBurst>().ToEntityList()
            .Single(entity => entity.GetComponent<ParticleBurst>().Kind == ParticleKind.Dash);
        Assert.That(burst.GetComponent<ParticleBurst>().Area, Is.EqualTo(_player.GetComponent<Player>().Size));
        Assert.That(burst.GetComponent<ParticleBurst>().Position, Is.EqualTo(_player.GetComponent<Position>().Value));
    }

    [Test]
    public void TheKeyUsedIsRememberedForHoldToMove()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Momentum));
        Press(KeybindAction.Right);

        Assert.That(_player.GetComponent<MoveKey>().Key, Is.EqualTo(KeybindAction.Right));
    }

    private ref Position PlaceAt(Vector2 at)
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = at;
        _player.GetComponent<PhysicsBody>().Body.SetTransform(Box2DPhysicsWorld.ToPhysics(at), 0f);
        return ref position;
    }

    private void Press(KeybindAction key)
    {
        _keyboard.Press(KeybindSettings.DefaultFor(key));
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private void Dash(Vector2 at, KeybindAction key, long timeMs = 0)
    {
        _store.CreateEntity(new Note(timeMs, at, kind: NoteKind.Dash));
        Press(key);
    }

    [Test]
    public void InTheAirADashIsMeasuredFromTheNote()
    {
        PlaceAt(new Vector2(940, 480));

        Dash(new Vector2(950, 500), KeybindAction.Left);

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(700).Within(0.01f));
        Assert.That(_player.GetComponent<Position>().Value.Y, Is.EqualTo(500).Within(0.01f));
    }

    [Test]
    public void OnTheFloorADashIsMeasuredFromStandingHeight()
    {
        // The note sits below where the player's centre can go; measured from
        // it, a short dash up would end inside the floor and do nothing.
        PlaceAt(new Vector2(500, 950));

        Dash(new Vector2(510, 990), KeybindAction.Up);

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(510).Within(0.01f));
        Assert.That(_player.GetComponent<Position>().Value.Y, Is.EqualTo(700).Within(0.01f));
    }

    [Test]
    public void OnABlockADashIsMeasuredFromTheBlockTop()
    {
        _store.CreateEntity(new Block(new Vector2(400, 800), new Vector2(200, 50)));
        PlaceAt(new Vector2(500, 750));

        Dash(new Vector2(500, 790), KeybindAction.Up);

        Assert.That(_player.GetComponent<Position>().Value.Y, Is.EqualTo(500).Within(0.01f));
    }

    [Test]
    public void ALateHitStartsThatFarIntoTheDashesFollowThrough()
    {
        _player.GetComponent<Playhead>().TimeMs = 40;
        _player.GetComponent<PhysicsBody>().Body.SetLinearVelocity(new System.Numerics.Vector2(4f, 0f));
        PlaceAt(new Vector2(500, 500));

        Dash(new Vector2(500, 500), KeybindAction.Up);

        // 4 m/s for 40 ms is 16 pixels; the hang means gravity has not had that time.
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(516).Within(0.01f));
        Assert.That(_player.GetComponent<Position>().Value.Y, Is.EqualTo(250).Within(0.01f));
    }

    [Test]
    public void ADashHangsSoThePlayerDoesNotDropLikeARock()
    {
        PlaceAt(new Vector2(500, 500));

        Dash(new Vector2(500, 500), KeybindAction.Up);

        Body body = _player.GetComponent<PhysicsBody>().Body;
        Assert.That(_player.HasComponent<DashHang>(), Is.True);
        Assert.That(Box2DPhysicsWorld.GravityScale(body), Is.Zero);
    }

    [Test]
    public void TopIsCapped()
    {
        PlaceAt(new Vector2(500, 50));

        Dash(new Vector2(500, 50), KeybindAction.Up);

        Assert.That(_player.GetComponent<Position>().Value.Y, Is.EqualTo(50));
    }

    [Test]
    public void AnActionKeyDoesNotMove()
    {
        Entity note = _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash));
        Press(KeybindAction.Action2);

        Assert.That(note.IsNull, Is.False);
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));
    }

    [Test]
    public void ADirectionKeyDashesEvenOverAnActionNote()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Neutral));
        Dash(new Vector2(500, 500), KeybindAction.Left);

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(250));
    }

    [TestCase(12f)]
    [TestCase(-12f)]
    public void ADashClearsVerticalVelocityAndKeepsHorizontal(float vertical)
    {
        Body body = _player.GetComponent<PhysicsBody>().Body;
        body.SetLinearVelocity(new System.Numerics.Vector2(4f, vertical));

        Dash(new Vector2(500, 500), KeybindAction.Up);

        Assert.That(body.LinearVelocity.Y, Is.Zero);
        Assert.That(body.LinearVelocity.X, Is.EqualTo(4f));
    }

    [Test]
    public void AMomentumNotePushesTheWayPressed()
    {
        Entity note = _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Momentum, momentum: 600f));
        Press(KeybindAction.Right);

        Assert.That(note.IsNull, Is.True);
        Assert.That(_player.GetComponent<PhysicsBody>().Body.LinearVelocity.X, Is.GreaterThan(0f));
    }

    [TestCase(-8f, 0f, KeybindAction.Right, 1f, 0f)]
    [TestCase(8f, 0f, KeybindAction.Right, 1f, 0f)]
    [TestCase(-8f, 5f, KeybindAction.Right, 1f, 0f)]
    [TestCase(3f, 12f, KeybindAction.Up, 0f, -1f)]
    public void AMomentumNoteImposesItsSpeedOnBothAxes(
        float startX,
        float startY,
        KeybindAction key,
        float expectedX,
        float expectedY)
    {
        Body body = _player.GetComponent<PhysicsBody>().Body;
        body.SetLinearVelocity(new System.Numerics.Vector2(startX, startY));
        _store.CreateEntity(new Note(
            0,
            new Vector2(500, 500),
            kind: NoteKind.Momentum,
            momentum: 100f / Box2DPhysicsWorld.MomentumScale));
        Press(key);

        Assert.That(body.LinearVelocity.X, Is.EqualTo(expectedX).Within(0.001f));
        Assert.That(body.LinearVelocity.Y, Is.EqualTo(expectedY).Within(0.001f));
    }

    [Test]
    public void ADiagonalMomentumNoteSetsBothAxes()
    {
        var note = new Note(0, Vector2.Zero, kind: NoteKind.Momentum, momentum: 100f / Box2DPhysicsWorld.MomentumScale);
        float axis = new Vector2(1f, -1f).Normalized().X;

        System.Numerics.Vector2 velocity = PlayerMovementSystem.MomentumVelocity(note, DashDirection.UpRight);

        Assert.That(velocity.X, Is.EqualTo(axis).Within(0.0001f));
        Assert.That(velocity.Y, Is.EqualTo(-axis).Within(0.0001f));
    }

    [Test]
    public void MomentumNotesDoNotStack()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Momentum, momentum: 200f));
        Press(KeybindAction.Right);
        System.Numerics.Vector2 once = _player.GetComponent<PhysicsBody>().Body.LinearVelocity;

        _keyboard.Release(KeybindSettings.DefaultFor(KeybindAction.Right));
        _root.Update(new UpdateTick(1f / 60f, 0));
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Momentum, momentum: 200f));
        Press(KeybindAction.Right);

        Assert.That(_player.GetComponent<PhysicsBody>().Body.LinearVelocity.X, Is.EqualTo(once.X).Within(0.001f));
    }

    [Test]
    public void MomentumSpeedScalesWithTheAuthoredMagnitude()
    {
        var note = new Note(0, Vector2.Zero, kind: NoteKind.Momentum, momentum: 600f);

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
        _store.CreateEntity(new Note(0, new Vector2(500, 500), kind: NoteKind.Dash, dashStrength: strength));
        Press(KeybindAction.Left);

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(expectedX));
    }
}
