using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Settings;
using Writer.Systems.Input;
using Writer.Systems.Playfields;

namespace Writer.Tests.Playfields;

[TestFixture]
public sealed class PlayKeyTrailSystemTests
{
    [Test]
    public void PressedKeyLightsAndReleasedKeyDecays()
    {
        var store = new EntityStore();
        Entity playfield = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new KeyTrail());
        var keyboard = new FakeKeyboard();
        var root = new SystemRoot(store);
        root.Add(new PlayKeyTrailSystem(
            store,
            new KeybindSettings(persist: false),
            keyboard));

        keyboard.Press(Key.F);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3),
            Is.EqualTo(1f));

        keyboard.Release(Key.F);
        for (int i = 0; i < 10; i++)
            root.Update(new UpdateTick(1f / 60f, 0));

        float value = playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3);
        Assert.That(value, Is.GreaterThan(0f));
        Assert.That(value, Is.LessThan(1f));
    }

    [Test]
    public void RecordsPressAndReleaseTimes()
    {
        var store = new EntityStore();
        Entity playfield = store.CreateEntity(
            new Playhead(1_000, 120, 0, false),
            new KeyTrail());
        var keyboard = new FakeKeyboard();
        var root = new SystemRoot(store);
        root.Add(new PlayKeyTrailSystem(
            store,
            new KeybindSettings(persist: false),
            keyboard));

        keyboard.Press(Key.F);
        root.Update(new UpdateTick(1f / 60f, 0));

        Entity segment = store.Query<KeyPressTrail>().ToEntityList()[0];
        Assert.That(segment.GetComponent<KeyPressTrail>().StartMs, Is.EqualTo(1_000));
        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(-1));

        playfield.GetComponent<Playhead>().TimeMs = 1_500;
        keyboard.Release(Key.F);
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(1_500));
    }

    [Test]
    public void PausedPlayfieldDoesNotUpdateTheTrail()
    {
        var store = new EntityStore();
        Entity playfield = store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new KeyTrail(),
            Tags.Get<PlayPaused>());
        var keyboard = new FakeKeyboard();
        keyboard.Press(Key.F);

        var root = new SystemRoot(store);
        root.Add(new PlayKeyTrailSystem(
            store,
            new KeybindSettings(persist: false),
            keyboard));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3),
            Is.Zero);
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        private readonly HashSet<Key> _down = new();

        public bool IsPressed(Key key) => _down.Contains(key);

        public void Press(Key key) => _down.Add(key);

        public void Release(Key key) => _down.Remove(key);
    }
}
