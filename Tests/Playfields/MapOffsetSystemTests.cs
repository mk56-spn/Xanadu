using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Replays;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class MapOffsetSystemTests
{
    private const float Delta = 1f / 60f;

    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private FakeKeys _keys = null!;
    private MapOffsetSettings _maps = null!;
    private OffsetSettings _global = null!;
    private Entity _play;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _root = new SystemRoot(_store);
        _keys = new FakeKeys();
        _maps = new MapOffsetSettings(persist: false);
        _global = new OffsetSettings(persist: false);
        _root.Add(new MapOffsetSystem(_keys, _maps, _global));
        _play = _store.CreateEntity(new PlayOffset("level", _store.CreateEntity(new Text(""))));
    }

    private void Frame() => _root.Update(new UpdateTick(Delta, 0));

    private void Press(params Key[] keys)
    {
        foreach (Key key in keys)
            _keys.Down.Add(key);
        Frame();
        _keys.Down.Clear();
        Frame();
    }

    private long Map => _play.GetComponent<PlayOffset>().MapMs;

    [Test]
    public void ShiftRightAndLeftStepTheMapOffset()
    {
        Press(Key.Shift, Key.Right);
        Press(Key.Shift, Key.Right);
        Press(Key.Shift, Key.Left);

        Assert.That(Map, Is.EqualTo(1));
        Assert.That(_maps.Get("level"), Is.EqualTo(1), "saved");
    }

    [Test]
    public void ArrowsAloneDoNothing()
    {
        Press(Key.Right);

        Assert.That(Map, Is.Zero);
    }

    [Test]
    public void HoldingRepeats()
    {
        _keys.Down.Add(Key.Shift);
        _keys.Down.Add(Key.Right);
        for (int i = 0; i < 60; i++)
            Frame();

        Assert.That(Map, Is.GreaterThan(3));
    }

    [Test]
    public void ASavedOffsetIsLoadedForTheMap()
    {
        _maps.Set("level", 9);
        Frame();

        Assert.That(Map, Is.EqualTo(9));
    }

    [Test]
    public void ItAddsToTheGlobalOffset()
    {
        _global.Set(20);
        _maps.Set("level", -5);
        Frame();

        Assert.That(PlayOffset.TotalMs(_play, _global), Is.EqualTo(15));
    }

    [Test]
    public void TheReadoutShowsThenGoes()
    {
        Entity label = _play.GetComponent<PlayOffset>().Label;
        _global.Set(20);
        Press(Key.Shift, Key.Right);

        Assert.That(label.GetComponent<Text>().Value, Is.EqualTo("MAP OFFSET +1 ms   ·   TOTAL +21 ms"));
        Assert.That(label.Tags.Has<UiHidden>(), Is.False);

        for (int i = 0; i < 100; i++)
            Frame();

        Assert.That(label.Tags.Has<UiHidden>(), Is.True);
    }

    [Test]
    public void AReplayIsNotChangedByTheKeys()
    {
        _play.AddComponent(new ReplayPlayer(Recorded()));

        Press(Key.Shift, Key.Right);

        Assert.That(Map, Is.Zero);
        Assert.That(_maps.Get("level"), Is.Zero);
    }

    [Test]
    public void AReplayIsJudgedWithTheOffsetsItWasPlayedWithNotTodays()
    {
        var keyboard = new ReplayKeyboard(_keys);
        var root = new SystemRoot(_store);
        root.Add(new ReplayPlaybackSystem(new KeybindSettings(persist: false), keyboard));
        _global.Set(99);
        Entity play = _store.CreateEntity(
            new PlayOffset("level", default),
            new Playhead(0, 120, 0, true, durationMs: 10_000),
            new PlaySession(default) { Started = true },
            new ReplayPlayer(Recorded(new ReplayOffset(0, 12), new ReplayOffset(500, 15))));

        root.Update(new UpdateTick(Delta, 0));
        Assert.That(PlayOffset.TotalMs(play, _global), Is.EqualTo(12));

        play.GetComponent<Playhead>().TimeMs = 600;
        root.Update(new UpdateTick(Delta, 0));
        Assert.That(PlayOffset.TotalMs(play, _global), Is.EqualTo(15));

        // A retry goes back to the start's offset.
        play.GetComponent<Playhead>().TimeMs = 0;
        root.Update(new UpdateTick(Delta, 0));
        Assert.That(PlayOffset.TotalMs(play, _global), Is.EqualTo(12));
    }

    [Test]
    public void RecordingNotesEachChangeOfTheWholeOffset()
    {
        var root = new SystemRoot(_store);
        root.Add(new ReplayRecordSystem(new KeybindSettings(persist: false), _keys, _global));
        _global.Set(10);
        Entity play = _store.CreateEntity(
            new PlayOffset("level", default),
            new Playhead(100, 120, 0, true, durationMs: 10_000),
            new PlaySession(default) { Started = true },
            ReplayRecorder.Create());

        root.Update(new UpdateTick(Delta, 0));
        root.Update(new UpdateTick(Delta, 0));
        play.GetComponent<PlayOffset>().MapMs = 3;
        play.GetComponent<Playhead>().TimeMs = 200;
        root.Update(new UpdateTick(Delta, 0));

        Assert.That(play.GetComponent<ReplayRecorder>().Offsets, Is.EqualTo(new[]
        {
            new ReplayOffset(100, 10),
            new ReplayOffset(200, 13),
        }));
    }

    private static Replay Recorded(params ReplayOffset[] offsets) => new(
        "level", "Level", "song", "", DateTimeOffset.UnixEpoch, 0, default,
        Array.Empty<ReplayEvent>(), -1, offsets);

    private sealed class FakeKeys : IKeyboardState
    {
        public readonly HashSet<Key> Down = new();

        public bool IsPressed(Key key) => Down.Contains(key);
    }
}
