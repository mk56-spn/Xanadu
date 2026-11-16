using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayKeyTrailSystemTests : EcsSystemTest
{
    [Test]
    public void PressedKeyLightsAndReleasedKeyDecays()
    {
        Entity playfield = Store.CreateEntity(new Playhead(0, 120, 0, false), new KeyTrail());
        var keyboard = new FakeKeyboard();
        Root.Add(new PlayKeyTrailSystem(Store, new KeybindSettings(persist: false), keyboard));

        keyboard.Press(Key.S);
        Frame();

        Assert.That(playfield.GetComponent<KeyTrail>().Get(KeybindAction.Action1), Is.EqualTo(1f));

        keyboard.Release(Key.S);
        Frame(10);

        float value = playfield.GetComponent<KeyTrail>().Get(KeybindAction.Action1);
        Assert.That(value, Is.GreaterThan(0f));
        Assert.That(value, Is.LessThan(1f));
    }

    [Test]
    public void RecordsPressAndReleaseTimes()
    {
        Entity playfield = Store.CreateEntity(new Playhead(1_000, 120, 0, false), new KeyTrail());
        var keyboard = new FakeKeyboard();
        Root.Add(new PlayKeyTrailSystem(Store, new KeybindSettings(persist: false), keyboard));

        keyboard.Press(Key.S);
        Frame();

        Entity segment = Store.Query<KeyPressTrail>().ToEntityList()[0];
        Assert.That(segment.GetComponent<KeyPressTrail>().StartMs, Is.EqualTo(1_000));
        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(-1));

        playfield.GetComponent<Playhead>().TimeMs = 1_500;
        keyboard.Release(Key.S);
        Frame();

        Assert.That(segment.GetComponent<KeyPressTrail>().EndMs, Is.EqualTo(1_500));
    }

    [Test]
    public void PausedPlayfieldDoesNotUpdateTheTrail()
    {
        Entity playfield = Store.CreateEntity(new Playhead(0, 120, 0, false), new KeyTrail(), Tags.Get<PlayPaused>());
        var keyboard = new FakeKeyboard();
        keyboard.Press(Key.S);

        Root.Add(new PlayKeyTrailSystem(Store, new KeybindSettings(persist: false), keyboard));
        Frame();

        Assert.That(playfield.GetComponent<KeyTrail>().Get(KeybindAction.Action1), Is.Zero);
    }
}
