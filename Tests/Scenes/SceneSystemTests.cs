using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Levels;
using Writer.Scenes;
using Writer.Profiles;
using Writer.Settings;
using Writer.Systems.Scenes;

namespace Writer.Tests.Scenes;

[TestFixture]
public sealed class SceneSystemTests
{
    private EntityStore _store = null!;
    private SceneDirector _director = null!;
    private SystemRoot _root = null!;
    private SceneSystem _scenes = null!;
    private KeybindSettings _keybinds = null!;
    private ProfileStore _profiles = null!;
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
        _profiles = new ProfileStore(persist: false);
        _scenes = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => _catalog,
            _keybinds,
            transitions: false,
            profiles: _profiles);

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
    public void ProfilesSceneCreatesAProfileAndContinues()
    {
        _scenes.Start(SceneKind.Profiles);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Profiles));
        Assert.That(HasText("WHO'S PLAYING?"), Is.True);
        Assert.That(HasText("NEW PROFILE"), Is.True);

        _store.Query<ProfileCreate>().ToEntityList()[0].AddTag<Pressed>();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(_profiles.ActiveProfile, Is.Not.Null);
    }

    [Test]
    public void SongSelectShowsTheActiveProfilesBestScore()
    {
        UserProfile profile = _profiles.Create("Scored");
        _profiles.RecordScore(
            _catalog[0].Path,
            new PlayResults("First Level", 900_000, 0.97, 42, 50, 55));

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(
            view.Best.GetComponent<Text>().Value,
            Does.Contain("BEST"));
        Assert.That(
            view.Best.GetComponent<Text>().Value,
            Does.Contain("900000"));
    }

    [Test]
    public void FinishedPlayStoresTheScoreForTheActiveProfile()
    {
        _profiles.Create("Scored");
        var document = new LevelDocument { Name = "Scored Level" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        string path = Path.Combine(_directory, "scored-level.json");
        LevelStore.Save(document, path);

        _scenes.StartPlay(new LevelEntry(path, document));
        Frame();
        _store.Query<PlaySession>().ToEntityList()[0]
            .GetComponent<PlaySession>().Finished = true;
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));
        Assert.That(_profiles.Best(path), Is.Not.Null);
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
    public void StartPlayUsesTheProvidedEntry()
    {
        var document = new LevelDocument
        {
            Name = "Provided Entry",
            PlayfieldWidth = 800,
            PlayfieldHeight = 600,
        };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });

        _scenes.StartPlay(new LevelEntry("memory://provided", document));
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(HasText("Provided Entry"), Is.True);
        Assert.That(HasText("SCORE 0000000"), Is.True);
    }

    [Test]
    public void StartPlayUsesASavedStartPoint()
    {
        var document = new LevelDocument
        {
            Name = "Start Level",
            PlayfieldWidth = 800,
            PlayfieldHeight = 600,
        };
        var start = new StartPoint(
            "Chorus",
            5_000,
            new Vector2(200, 300),
            new System.Numerics.Vector2(4, -2),
            new Vector2(1, 0));

        _scenes.StartPlay(
            new LevelEntry("memory://start", document),
            start);
        Frame();

        Entity playfield = _store.Query<PlayfieldMode>().ToEntityList()[0];
        Assert.That(playfield.GetComponent<PlaySession>().StartTimeMs,
            Is.EqualTo(5_000));
        Assert.That(playfield.GetComponent<Momentum>().Value,
            Is.EqualTo(new System.Numerics.Vector2(4, -2)));
        Vector2 expected = playfield.GetComponent<PlayfieldView>()
            .PlayfieldToScreen(new Vector2(200, 300));
        Assert.That(playfield.GetComponent<Writer.Ecs.Position>().Value,
            Is.EqualTo(expected));
    }

    [Test]
    public void FinishedPlayMovesToResultsWithFinalValues()
    {
        var document = new LevelDocument
        {
            Name = "Results Level",
            PlayfieldWidth = 800,
            PlayfieldHeight = 600,
        };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });

        _scenes.StartPlay(new LevelEntry("memory://results", document));
        Frame();

        Entity playfield = _store.Query<ScoreState>().ToEntityList()[0];
        ref ScoreState score = ref playfield.GetComponent<ScoreState>();
        score.ScorePoints = 0.75;
        score.AccuracyPoints = 0.9876;
        score.MaxCombo = 42;
        score.NotesHit = 1;
        playfield.GetComponent<PlaySession>().Finished = true;

        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));
        Assert.That(HasText("RESULTS"), Is.True);
        Assert.That(HasText("Results Level"), Is.True);
        Assert.That(HasText("98.76%"), Is.True);
        Assert.That(HasText("42"), Is.True);
        Assert.That(HasText("1 / 1"), Is.True);
        Assert.That(HasText("0750000"), Is.True);
    }

    [Test]
    public void RetryFromResultsRestartsTheLevel()
    {
        var document = new LevelDocument { Name = "Retry Level" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        _scenes.StartPlay(new LevelEntry("memory://retry", document));
        Frame();

        _store.Query<PlaySession>().ToEntityList()[0]
            .GetComponent<PlaySession>().Finished = true;
        Frame();
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));

        _store.Query<RetryAction>().ToEntityList()[0].AddTag<Pressed>();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(HasText("Retry Level"), Is.True);
    }

    [Test]
    public void PauseRetryRestartsTheLevel()
    {
        var document = new LevelDocument { Name = "Pause Retry" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        _scenes.StartPlay(new LevelEntry("memory://pause-retry", document));
        Frame();

        _scenes.TryBack();
        Assert.That(Pause().Visible, Is.True);

        Pause().Retry.AddTag<Pressed>();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(Pause().Visible, Is.False);
    }

    [Test]
    public void StartResultsUsesTheProvidedSnapshot()
    {
        _scenes.StartResults(new PlayResults("Snapshot", 123_456, 0.95, 12, 12, 14));
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Results));
        Assert.That(HasText("Snapshot"), Is.True);
        Assert.That(HasText("95.00%"), Is.True);
        Assert.That(HasText("12"), Is.True);
        Assert.That(HasText("12 / 14"), Is.True);
        Assert.That(HasText("0123456"), Is.True);
    }

    [Test]
    public void SceneTagUsesLargeLowOpacityBlackText()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        Entity tag = default;
        foreach (Entity entity in _store.Query<Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == "MAIN")
            {
                tag = entity;
                break;
            }
        }

        Assert.That(tag.IsNull, Is.False);
        TextStyle style = tag.GetComponent<TextStyle>();
        Assert.That(style.FontSize, Is.EqualTo(56));
        Assert.That(style.Color.R, Is.Zero);
        Assert.That(style.Color.G, Is.Zero);
        Assert.That(style.Color.B, Is.Zero);
        Assert.That(style.Color.A, Is.LessThan(0.6f));
        Assert.That(
            tag.GetComponent<Rectangle>().Bounds.Size,
            Is.EqualTo(new Vector2(380, 100)));
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
    public void ArrowCarouselStepsAndClamps()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_scenes.TryStepCarousel(1), Is.True);
        Assert.That(
            _store.Query<SongSelectState>().ToEntityList()[0]
                .GetComponent<SongSelectState>().Selected,
            Is.EqualTo(1));

        Assert.That(_scenes.TryStepCarousel(1), Is.False);
        Assert.That(_scenes.TryStepCarousel(-1), Is.True);
        Assert.That(
            _store.Query<SongSelectState>().ToEntityList()[0]
                .GetComponent<SongSelectState>().Selected,
            Is.EqualTo(0));
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
    public void PauseRetryRestartsWithFadeTransitions()
    {
        _scenes.Enabled = false;

        var transitional = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => _catalog,
            _keybinds,
            transitions: true);
        _root.Add(transitional);

        var document = new LevelDocument { Name = "Fade Retry" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        transitional.StartPlay(new LevelEntry("memory://fade-retry", document));
        Advance(20);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));

        transitional.TryBack();
        Advance(2);
        Assert.That(
            _store.Query<ScenePause>().ToEntityList()[0]
                .GetComponent<ScenePause>().Visible,
            Is.True);

        _store.Query<ScenePause>().ToEntityList()[0]
            .GetComponent<ScenePause>().Retry.AddTag<Pressed>();

        Advance(40);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(
            _store.Query<ScenePause>().ToEntityList()[0]
                .GetComponent<ScenePause>().Visible,
            Is.False);
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
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
    public void SettingsKeepsBackgroundBelowContent()
    {
        _scenes.Start(SceneKind.Settings);
        Frame();

        int backgroundLayers = 0;
        int contentLayers = 0;
        foreach (Entity layer in _store.Query<Layer>().ToEntityList())
        {
            int z = layer.GetComponent<Layer>().ZIndex;
            if (z == SceneUi.MenuLayer - 1)
                backgroundLayers++;
            else if (z == SceneUi.MenuLayer)
                contentLayers++;
        }

        Assert.That(backgroundLayers, Is.EqualTo(1));
        Assert.That(contentLayers, Is.EqualTo(1));

        bool hasFullScreenBackground = false;
        bool hasContentTitle = false;

        foreach (Entity rect in _store.Query<Rectangle>().ToEntityList())
        {
            int z = rect.GetComponent<Drawable>().ZIndex;
            if (z == SceneUi.MenuLayer - 1 &&
                rect.GetComponent<Rectangle>().Bounds.Size == new Vector2(1280, 720))
            {
                hasFullScreenBackground = true;
            }

            if (z == SceneUi.MenuLayer &&
                rect.HasComponent<Text>() &&
                rect.GetComponent<Text>().Value == "SETTINGS")
            {
                hasContentTitle = true;
            }
        }

        Assert.That(hasFullScreenBackground, Is.True);
        Assert.That(hasContentTitle, Is.True);
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

    private void Advance(int frames)
    {
        for (int i = 0; i < frames; i++)
            Frame();
    }

    private sealed class FakeVisualRidFactory : IVisualRidFactory
    {
        public Rid CreateCanvasItem() => default;
    }
}
