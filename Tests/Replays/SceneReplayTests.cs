using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Systems.Scenes;
using Xanadu.Tests.Support;
using SceneState = Xanadu.Ecs.SceneState;

namespace Xanadu.Tests.Replays;

[TestFixture]
public sealed class SceneReplayTests
{
    private string _directory = null!;
    private EntityStore _store = null!;
    private SceneDirector _director = null!;
    private SystemRoot _root = null!;
    private ProfileStore _profiles = null!;
    private SceneSystem _scenes = null!;
    private ReplayStore _replays = null!;
    private LevelEntry _level = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "scene-replays-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_directory);

        var document = new LevelDocument { Name = "Replay Level" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        string path = Path.Combine(_directory, "replay-level.json");
        LevelStore.Save(document, path);
        _level = new LevelEntry(path, LevelStore.Load(path));

        _store = new EntityStore();
        _director = new SceneDirector();
        _root = new SystemRoot(_store);
        _profiles = new ProfileStore(persist: false);
        _profiles.Create("Recorder");
        _replays = new ReplayStore(Path.Combine(_directory, "replays"));
        _scenes = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => new List<LevelEntry> { _level },
            new KeybindSettings(persist: false),
            transitions: false,
            profiles: _profiles,
            replays: _replays);
        _root.Add(_scenes);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void ARealPlayRecordsAndSavesAnXrsFile()
    {
        PlayThrough(out Entity _);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));
        string saved = _replays.List().Single();
        Replay replay = ReplayStore.Load(saved);
        Assert.That(saved, Does.EndWith(".xrs"));
        Assert.That(replay.LevelId, Is.EqualTo(_level.Document.Id));
        Assert.That(replay.Player, Is.EqualTo("Recorder"));
        Assert.That(replay.NoteCount, Is.EqualTo(1));
        Assert.That(HasText("WATCH REPLAY"), Is.True);
    }

    [Test]
    public void WatchingAReplayDrivesThePlayWithoutRecordingOrScoring()
    {
        PlayThrough(out Entity _);
        int savedBefore = _replays.List().Count;
        double? bestBefore = _profiles.Best(_level.Path)?.Accuracy;

        Assert.That(_scenes.WatchLastReplay(), Is.True);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(_store.Query<ReplayPlayer>().Count, Is.EqualTo(1));
        Assert.That(_store.Query<ReplayRecorder>().Count, Is.EqualTo(0));

        _store.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Finished = true;
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));
        Assert.That(_replays.List(), Has.Count.EqualTo(savedBefore));
        Assert.That(_profiles.Best(_level.Path)?.Accuracy, Is.EqualTo(bestBefore));
        Assert.That(HasText("WATCH REPLAY"), Is.True, "a watched replay can be watched again");
    }

    [Test]
    public void ReplayOfADeletedLevelIsRefused()
    {
        var orphan = new Replay("gone", "Gone", "", "", DateTimeOffset.UtcNow, 0, default, []);

        Assert.That(_scenes.StartReplay(orphan), Is.False);
    }

    [Test]
    public void PracticeFromAStartPointLeavesNoReplay()
    {
        _scenes.StartPlay(_level, new StartPoint { TimeMs = 100 });
        Frame();

        Assert.That(_store.Query<ReplayRecorder>().Count, Is.EqualTo(0));

        _store.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Finished = true;
        Frame();

        Assert.That(_replays.List(), Is.Empty);
        Assert.That(HasText("WATCH REPLAY"), Is.False);
    }

    private void PlayThrough(out Entity play)
    {
        _scenes.StartPlay(_level);
        Frame();
        play = _store.Query<PlaySession>().ToEntityList()[0];
        Assert.That(_store.Query<ReplayRecorder>().Count, Is.EqualTo(1));
        play.GetComponent<PlaySession>().Finished = true;
        Frame();
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private bool HasText(string text) =>
        _store.Query<Text>().ToEntityList().Any(e => e.GetComponent<Text>().Value == text);
}
