using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Levels;
using Writer.Settings;
using Writer.Systems.Scenes;
using Writer.Ui;

namespace Writer.Tests.Scenes;

[TestFixture]
public sealed class SceneSystemTests
{
    private EntityStore _store = null!;
    private SceneDirector _director = null!;
    private SystemRoot _root = null!;
    private SceneSystem _scenes = null!;
    private KeybindSettings _keybinds = null!;
    private string _directory = null!;
    private List<LevelEntry> _catalog = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _director = new SceneDirector();
        _root = new SystemRoot(_store);
        _directory = Path.Combine(
            Path.GetTempPath(),
            "writer-scene-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _catalog = new List<LevelEntry>
        {
            CreateLevel("First Level", "res://song-a.mp3", 140),
            CreateLevel("Second Level", "res://song-b.mp3", 170),
        };

        _keybinds = new KeybindSettings(persist: false);
        _scenes = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => _catalog,
            _keybinds);

        _root.Add(_scenes);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void MainMenuStartsAndLabelsScene()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(_store.Query<SceneRoot>().Count, Is.EqualTo(1));
        Assert.That(_store.Query<SceneRoot>().ToEntityList()[0].GetComponent<SceneRoot>().Scene,
            Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("MAIN"), Is.True);
        Assert.That(HasText("WRITER"), Is.True);
    }

    [Test]
    public void SongSelectShowsCatalogAndStepsCarousel()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.SongSelect));
        Assert.That(HasText("SONG SELECT"), Is.True);

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(view.Count, Is.EqualTo(2));
        Assert.That(view.Selected, Is.EqualTo(0));
        Assert.That(view.Title.GetComponent<Text>().Value, Is.EqualTo("First Level"));
        Assert.That(view.Index.GetComponent<Text>().Value, Is.EqualTo("1 / 2"));

        Entity step = _store.CreateEntity(new CarouselStep(1), Tags.Get<Pressed>());
        Frame();

        view = root.GetComponent<SongSelectState>();
        Assert.That(view.Selected, Is.EqualTo(1));
        Assert.That(view.Title.GetComponent<Text>().Value, Is.EqualTo("Second Level"));
        Assert.That(view.Index.GetComponent<Text>().Value, Is.EqualTo("2 / 2"));

        step.DeleteEntity();
    }

    [Test]
    public void PlaySceneReusesPlayfieldAndShowsNotes()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.Start(SceneKind.Play);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(HasText("PLAY"), Is.True);

        Entity playfield = _store.Query<PlayfieldMode>().ToEntityList()[0];
        Assert.That(playfield.GetComponent<PlayfieldMode>().Kind, Is.EqualTo(PlayfieldKind.Play));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void BackFromSongSelectGoesToMain()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        _scenes.TryBack();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("MAIN"), Is.True);
    }

    [Test]
    public void ForwardWalksMainToSongSelectToPlay()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        _scenes.TryForward();
        Frame();
        Assert.That(_director.Current, Is.EqualTo(SceneKind.SongSelect));

        _scenes.TryForward();
        Frame();
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
    }

    [Test]
    public void BackFromPlayOpensPauseAndStopsSong()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        Entity playfield = Playfield();
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        playhead.Playing = true;

        _scenes.TryBack();

        Assert.That(Pause().Visible, Is.True);
        Assert.That(
            Pause().Title.GetComponent<Text>().Value,
            Is.EqualTo("PAUSED"));
        Assert.That(playhead.Playing, Is.False);
        Assert.That(playfield.Tags.Has<PlayPaused>(), Is.True);
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));

        _scenes.TryForward();
        Frame();

        Assert.That(Pause().Visible, Is.False);
        Assert.That(playhead.Playing, Is.True);
        Assert.That(playfield.Tags.Has<PlayPaused>(), Is.False);
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
    }

    [Test]
    public void PauseResumeButtonResumesSong()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        Entity playfield = Playfield();
        ref Playhead playhead = ref playfield.GetComponent<Playhead>();
        _scenes.TryBack();

        Pause().Resume.AddTag<Pressed>();
        Frame();

        Assert.That(Pause().Visible, Is.False);
        Assert.That(playhead.Playing, Is.True);
        Assert.That(playfield.Tags.Has<PlayPaused>(), Is.False);
    }

    [Test]
    public void PauseLeaveButtonReturnsToSongSelect()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        _scenes.TryBack();
        Pause().Leave.AddTag<Pressed>();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.SongSelect));
        Assert.That(_store.Query<PlayfieldMode>().Count, Is.EqualTo(0));
    }

    [Test]
    public void PauseHudButtonOpensPause()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        _store.Query<PauseAction>().ToEntityList()[0].AddTag<Pressed>();
        Frame();

        Assert.That(Pause().Visible, Is.True);
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
    }

    [Test]
    public void BackAgainResumesPause()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        _scenes.TryBack();
        Assert.That(Pause().Visible, Is.True);

        _scenes.TryBack();
        Frame();

        Assert.That(Pause().Visible, Is.False);
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
    }

    [Test]
    public void BackFromSettingsGoesToMain()
    {
        _scenes.Start(SceneKind.Settings);
        Frame();

        _scenes.TryBack();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
    }

    [Test]
    public void BackFromMainDoesNothing()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        _scenes.TryBack();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
    }

    [Test]
    public void SettingsShowsAllDefaultKeybinds()
    {
        _scenes.Start(SceneKind.Settings);
        Frame();

        var rows = _store.Query<KeybindButton>().ToEntityList();
        Assert.That(rows.Count, Is.EqualTo(KeybindSettings.Actions.Length));

        foreach (Entity row in rows)
        {
            KeybindAction action = row.GetComponent<KeybindButton>().Action;
            string expected = KeybindSettings.KeyLabel(
                KeybindSettings.DefaultFor(action));
            Assert.That(row.GetComponent<Text>().Value, Is.EqualTo(expected));
        }
    }

    [Test]
    public void ClickingAKeybindRowStartsCapture()
    {
        _scenes.Start(SceneKind.Settings);
        Frame();

        Entity row = _store.Query<KeybindButton>().ToEntityList()[0];
        row.AddTag<Pressed>();
        Frame();

        Assert.That(_keybinds.Capturing, Is.EqualTo(KeybindAction.L1));
        Assert.That(_keybinds.TryCapture(Key.A), Is.True);
        Assert.That(_keybinds.Get(KeybindAction.L1), Is.EqualTo(Key.A));
    }

    [Test]
    public void SettingsIsASceneWithItsOwnLabel()
    {
        _scenes.Start(SceneKind.Settings);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Settings));
        Assert.That(HasText("SETTINGS"), Is.True);
    }

    private LevelEntry CreateLevel(string name, string audioPath, int bpm)
    {
        string path = Path.Combine(_directory, name.Replace(' ', '-') + ".json");
        var document = new LevelDocument
        {
            Name = name,
            AudioPath = audioPath,
            Bpm = bpm,
        };
        document.Notes.Add(new NoteDocument
        {
            TimeMs = 500,
            X = 100,
            Y = 200,
        });

        LevelStore.Save(document, path);
        return new LevelEntry(path, LevelStore.Load(path));
    }

    private ScenePause Pause() =>
        _store.Query<ScenePause>().ToEntityList()[0]
            .GetComponent<ScenePause>();

    private Entity Playfield() =>
        _store.Query<PlayfieldMode>().ToEntityList()[0];

    private bool HasText(string value)
    {
        foreach (Entity entity in _store.Query<Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == value)
                return true;
        }

        return false;
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class FakeVisualRidFactory : IVisualRidFactory
    {
        public Rid CreateCanvasItem() => default;
    }
}
