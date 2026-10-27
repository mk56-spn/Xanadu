using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Systems.Replays;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Replays;

[TestFixture]
public sealed class ReplaySystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakeKeyboard _real = null!;
    private KeybindSettings _keybinds = null!;
    private ReplayKeyboard _keyboard = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _real = new FakeKeyboard();
        _keybinds = new KeybindSettings(persist: false);
        _keyboard = new ReplayKeyboard(_real);
        _root = new SystemRoot(_store);
        _root.Add(new ReplayRecordSystem(_keybinds, _real));
        _root.Add(new ReplayPlaybackSystem(_keybinds, _keyboard));
    }

    [Test]
    public void RecordsPressesAndReleasesAtThePlayheadTime()
    {
        Entity play = Play(recorder: true);
        Key l1 = _keybinds.Get(KeybindAction.L1);

        Frame(play, 1000);
        _real.Press(l1);
        Frame(play, 1016);
        Frame(play, 1032);
        _real.Release(l1);
        Frame(play, 1048);

        Assert.That(
            play.GetComponent<ReplayRecorder>().Events,
            Is.EqualTo(new[]
            {
                new ReplayEvent(1016, KeybindAction.L1, true),
                new ReplayEvent(1048, KeybindAction.L1, false),
            }));
    }

    [Test]
    public void DoesNotRecordBeforeStartWhilePausedOrAfterFinish()
    {
        Entity play = Play(recorder: true, started: false);
        _real.Press(_keybinds.Get(KeybindAction.R1));

        Frame(play, 0);
        Assert.That(play.GetComponent<ReplayRecorder>().Events, Is.Empty);

        play.GetComponent<PlaySession>().Started = true;
        play.AddTag<PlayPaused>();
        Frame(play, 100);
        Assert.That(play.GetComponent<ReplayRecorder>().Events, Is.Empty);

        play.RemoveTag<PlayPaused>();
        play.GetComponent<PlaySession>().Finished = true;
        Frame(play, 200);
        Assert.That(play.GetComponent<ReplayRecorder>().Events, Is.Empty);
    }

    [Test]
    public void RecordsSimultaneousKeysAsSeparateEvents()
    {
        Entity play = Play(recorder: true);

        _real.Press(_keybinds.Get(KeybindAction.L1));
        _real.Press(_keybinds.Get(KeybindAction.R3));
        Frame(play, 500);

        Assert.That(
            play.GetComponent<ReplayRecorder>().Events.Select(e => e.Action),
            Is.EquivalentTo(new[] { KeybindAction.L1, KeybindAction.R3 }));
    }

    [Test]
    public void PlaybackPressesKeysAsTheSongReachesEachEvent()
    {
        Entity play = Play(replay: Script(
            new ReplayEvent(1000, KeybindAction.L1, true),
            new ReplayEvent(1100, KeybindAction.L1, false)));
        Key l1 = _keybinds.Get(KeybindAction.L1);

        Frame(play, 999);
        Assert.That(_keyboard.IsPressed(l1), Is.False);

        Frame(play, 1000);
        Assert.That(_keyboard.IsPressed(l1), Is.True);

        Frame(play, 1099);
        Assert.That(_keyboard.IsPressed(l1), Is.True);

        Frame(play, 1100);
        Assert.That(_keyboard.IsPressed(l1), Is.False);
    }

    [Test]
    public void ReplayIgnoresTheRealKeyboardAndRestoresItAfterwards()
    {
        Entity play = Play(replay: Script());
        Key l1 = _keybinds.Get(KeybindAction.L1);

        _real.Press(l1);
        Frame(play, 50);
        Assert.That(_keyboard.IsPressed(l1), Is.False, "real key must not leak into a replay");

        play.DeleteEntity();
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_keyboard.IsPressed(l1), Is.True, "real keyboard returns when the replay ends");
    }

    [Test]
    public void PlaybackHonoursKeybindsChangedSinceRecording()
    {
        Entity play = Play(replay: Script(new ReplayEvent(10, KeybindAction.L1, true)));
        _keybinds.BeginCapture(KeybindAction.L1);
        Assert.That(_keybinds.TryCapture(Key.Q), Is.True);

        Frame(play, 20);

        Assert.That(_keyboard.IsPressed(Key.Q), Is.True);
    }

    [Test]
    public void SeekingBackwardsReplaysFromTheStart()
    {
        Entity play = Play(replay: Script(
            new ReplayEvent(100, KeybindAction.L1, true),
            new ReplayEvent(200, KeybindAction.L1, false)));
        Key l1 = _keybinds.Get(KeybindAction.L1);

        Frame(play, 150);
        Frame(play, 250);
        Assert.That(_keyboard.IsPressed(l1), Is.False);

        Frame(play, 120);
        Assert.That(_keyboard.IsPressed(l1), Is.True);
    }

    [Test]
    public void PausedPlaybackHoldsTheKeysWhereTheyWere()
    {
        Entity play = Play(replay: Script(new ReplayEvent(100, KeybindAction.L1, true)));
        Key l1 = _keybinds.Get(KeybindAction.L1);

        Frame(play, 150);
        play.AddTag<PlayPaused>();
        Frame(play, 5000);

        Assert.That(_keyboard.IsPressed(l1), Is.True);
    }

    [Test]
    public void ARecordedPlayReplaysToTheSamePressesAtTheSameTimes()
    {
        Entity recording = Play(recorder: true);
        var pattern = new (long Time, KeybindAction Action, bool Down)[]
        {
            (1000, KeybindAction.L2, true),
            (1064, KeybindAction.L2, false),
            (1500, KeybindAction.M, true),
            (1500, KeybindAction.R1, true),
            (1580, KeybindAction.M, false),
            (1700, KeybindAction.R1, false),
        };

        for (long time = 900; time <= 1800; time += 4)
        {
            foreach (var step in pattern.Where(step => step.Time == time))
            {
                Key key = _keybinds.Get(step.Action);
                if (step.Down)
                    _real.Press(key);
                else
                    _real.Release(key);
            }

            Frame(recording, time);
        }

        Replay replay = Script(recording.GetComponent<ReplayRecorder>().Events.ToArray());
        recording.DeleteEntity();
        _real.Clear();
        Entity replayed = Play(replay: replay);

        for (long time = 900; time <= 1800; time += 4)
        {
            Frame(replayed, time);
            foreach (KeybindAction action in KeybindSettings.Actions)
            {
                bool expected = false;
                foreach (var step in pattern.Where(step => step.Time <= time && step.Action == action))
                    expected = step.Down;

                Assert.That(
                    _keyboard.IsPressed(_keybinds.Get(action)),
                    Is.EqualTo(expected),
                    $"{action} at {time}ms");
            }
        }
    }

    private Entity Play(bool recorder = false, Replay? replay = null, bool started = true)
    {
        Entity play = _store.CreateEntity(
            new Playhead(0, 120, 0, true),
            new PlaySession(default) { Started = started });
        if (recorder)
            play.AddComponent(ReplayRecorder.Create());
        if (replay is not null)
            play.AddComponent(new ReplayPlayer(replay));

        return play;
    }

    private void Frame(Entity play, long timeMs)
    {
        play.GetComponent<Playhead>().TimeMs = timeMs;
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private static Replay Script(params ReplayEvent[] events) => new(
        "level",
        "Level",
        "song",
        "tester",
        System.DateTimeOffset.UnixEpoch,
        1,
        default,
        events);
}
