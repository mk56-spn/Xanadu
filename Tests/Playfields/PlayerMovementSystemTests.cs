using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;
using Writer.Systems.Physics;
using Writer.Systems.Playfields;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Playfields;

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

        var body = _physics.CreateDynamicBox(
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
    public void DashesLeftOnlyWhileAMovementQueueIsActive()
    {
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));

        Entity movement = _store.CreateEntity(new MovementEvent(0));
        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(350));
        Assert.That(movement.Tags.Has<MovementEventUsed>(), Is.True);
    }

    [Test]
    public void LateralBorderWraps()
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = new Vector2(950, 500);
        _player.GetComponent<PhysicsBody>().Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(position.Value),
            0f);
        _store.CreateEntity(new MovementEvent(0));

        _keyboard.Press(Key.L);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(position.Value.X, Is.EqualTo(100));
    }

    [Test]
    public void TopIsCapped()
    {
        ref Position position = ref _player.GetComponent<Position>();
        position.Value = new Vector2(500, 50);
        _player.GetComponent<PhysicsBody>().Body.SetTransform(
            Box2DPhysicsWorld.ToPhysics(position.Value),
            0f);
        _store.CreateEntity(new MovementEvent(0));

        _keyboard.Press(Key.Space);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(position.Value.Y, Is.EqualTo(50));
    }

    [Test]
    public void TrueNoteBlocksMovement()
    {
        _store.CreateEntity(new Note(0, new Vector2(500, 500)));
        _store.CreateEntity(new MovementEvent(0));

        _keyboard.Press(Key.F);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_player.GetComponent<Position>().Value.X, Is.EqualTo(500));
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        private readonly HashSet<Key> _down = new();

        public bool IsPressed(Key key) => _down.Contains(key);

        public void Press(Key key) => _down.Add(key);
    }
}
