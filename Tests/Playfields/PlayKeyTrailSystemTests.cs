using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayKeyTrailSystemTests : EcsSystemTest
{
    [Test]
    public void PressedKeyLightsAndReleasedKeyDecays()
    {
        Entity playfield = Store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new KeyTrail());
        var keyboard = new FakeKeyboard();
        Root.Add(new PlayKeyTrailSystem(
            Store,
            new KeybindSettings(persist: false),
            keyboard));

        keyboard.Press(Key.F);
        Frame();

        Assert.That(
            playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3),
            Is.EqualTo(1f));

        keyboard.Release(Key.F);
        Frame(10);

        float value = playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3);
        Assert.That(value, Is.GreaterThan(0f));
        Assert.That(value, Is.LessThan(1f));
    }

    [Test]
    public void RecordsPressAndReleaseTimes()
    {
        Entity playfield = Store.CreateEntity(
            new Playhead(1_000, 120, 0, false),
            new KeyTrail());
        var keyboard = new FakeKeyboard();
        Root.Add(new PlayKeyTrailSystem(
            Store,
            new KeybindSettings(persist: false),
            keyboard));

        keyboard.Press(Key.F);
        Frame();

        Entity segment = Store.Query<KeyPressTrail>().ToEntityList()[0];
        Assert.That(segment.GetComponent<KeyPressTrail>().StartMs, Is.EqualTo(1_000));
        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(-1));

        playfield.GetComponent<Playhead>().TimeMs = 1_500;
        keyboard.Release(Key.F);
        Frame();

        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(1_500));
    }

    [Test]
    public void PausedPlayfieldDoesNotUpdateTheTrail()
    {
        Entity playfield = Store.CreateEntity(
            new Playhead(0, 120, 0, false),
            new KeyTrail(),
            Tags.Get<PlayPaused>());
        var keyboard = new FakeKeyboard();
        keyboard.Press(Key.F);

        Root.Add(new PlayKeyTrailSystem(
            Store,
            new KeybindSettings(persist: false),
            keyboard));
        Frame();

        Assert.That(
            playfield.GetComponent<KeyTrail>().Get(KeybindAction.L3),
            Is.Zero);
    }


}
