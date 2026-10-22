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
public sealed class CharacterPlaySpatialTests
{
    [TestCase(DashDirection.Up, 0f, -150f)]
    [TestCase(DashDirection.UpRight, 106.066f, -106.066f)]
    [TestCase(DashDirection.Right, 150f, 0f)]
    [TestCase(DashDirection.DownRight, 106.066f, 106.066f)]
    [TestCase(DashDirection.Down, 0f, 150f)]
    [TestCase(DashDirection.DownLeft, -106.066f, 106.066f)]
    [TestCase(DashDirection.Left, -150f, 0f)]
    [TestCase(DashDirection.UpLeft, -106.066f, -106.066f)]
    public void MovementNotePlacesPlayerAtAnchoredDirection(
        DashDirection direction,
        float expectedX,
        float expectedY)
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var keyboard = new FakeKeyboard();
        var keybinds = new KeybindSettings(persist: false);
        var root = new SystemRoot(store);
        root.Add(new PlayerMovementSystem(store, keybinds, keyboard));

        var body = physics.CreateDynamicBox(
            new Vector2(400, 400),
            new Vector2(30, 100));
        Entity player = store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 400)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playhead(1_000, 120, 0, true),
            new PhysicsBody(body),
            new Momentum(System.Numerics.Vector2.Zero));

        Entity note = store.CreateEntity(new Note(
            1_000,
            new Vector2(400, 400),
            target: KeybindAction.L3,
            kind: NoteKind.Movement,
            dashDirection: direction));

        keyboard.Press(keybinds.Get(KeybindAction.L3));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            player.GetComponent<Position>().Value.X,
            Is.EqualTo(400 + expectedX).Within(0.01f));
        Assert.That(
            player.GetComponent<Position>().Value.Y,
            Is.EqualTo(400 + expectedY).Within(0.01f));
        Assert.That(note.IsNull, Is.True);
    }

    [Test]
    public void SequentialMovementNotesCarryThePlayerThroughTime()
    {
        using var physics = new Box2DPhysicsWorld();
        var store = new EntityStore();
        var keyboard = new FakeKeyboard();
        var keybinds = new KeybindSettings(persist: false);
        var root = new SystemRoot(store);
        root.Add(new PlayerMovementSystem(store, keybinds, keyboard));

        var body = physics.CreateDynamicBox(
            new Vector2(400, 400),
            new Vector2(30, 100));
        Entity player = store.CreateEntity(
            new Player(new Vector2(30, 100)),
            new Position(new Vector2(400, 400)),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1_000, 1_000)),
            new Playhead(1_000, 120, 0, true),
            new PhysicsBody(body),
            new Momentum(System.Numerics.Vector2.Zero));

        store.CreateEntity(new Note(
            1_000,
            new Vector2(400, 400),
            target: KeybindAction.L3,
            kind: NoteKind.Movement,
            dashDirection: DashDirection.Right));
        keyboard.Press(keybinds.Get(KeybindAction.L3));
        root.Update(new UpdateTick(1f / 60f, 0));
        keyboard.Release(keybinds.Get(KeybindAction.L3));
        player.GetComponent<Playhead>().TimeMs = 2_000;

        store.CreateEntity(new Note(
            2_000,
            new Vector2(550, 400),
            target: KeybindAction.R3,
            kind: NoteKind.Movement,
            dashDirection: DashDirection.Down));
        keyboard.Press(keybinds.Get(KeybindAction.R3));
        root.Update(new UpdateTick(1f / 60f, 1_000));
        keyboard.Release(keybinds.Get(KeybindAction.R3));

        Assert.That(
            player.GetComponent<Position>().Value,
            Is.EqualTo(new Vector2(550, 550)));
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        private readonly HashSet<Key> _down = new();
        public bool IsPressed(Key key) => _down.Contains(key);
        public void Press(Key key) => _down.Add(key);
        public void Release(Key key) => _down.Remove(key);
    }
}
