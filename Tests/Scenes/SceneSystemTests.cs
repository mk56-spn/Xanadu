using Xanadu.Serialisation.Levels;
using Xanadu.Storage.Levels;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Scoring;
using Xanadu.Storage.Profiles;
using Xanadu.Model.Settings;
using Xanadu.Tests.Support;
using Xanadu.Tests.Support.Fakes;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Xanadu.Screens.SongSelect;
using Xanadu.Screens.SongTiming;
using Xanadu.Wiring.Scenes;
using Position = Xanadu.Core.Position;
using Xanadu.Storage.Profiles;
using Xanadu.Storage.Settings;
using Xanadu.Audio;

namespace Xanadu.Tests.Scenes;

[TestFixture]
public sealed partial class SceneSystemTests
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
        _directory = Path.Combine(Path.GetTempPath(), "writer-scene-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _catalog = new List<LevelEntry>
        {
            CreateLevel("First Level", "song-a.mp3", 140),
            CreateLevel("Second Level", "song-b.mp3", 170)
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
            false,
            _profiles);

        _root.Add(_scenes);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public void TheAddSongActionAsksTheHostForAFile()
    {
        bool asked = false;
        _scenes.AddSongRequested = () => asked = true;

        var registry = new SceneActionRegistry();
        SongTimingSceneActions.Register(registry);

        bool handled = registry.TryInvoke(
            SceneKind.SongTiming,
            new SceneActionContext(_scenes, _store, default, SceneActionId.AddSong));

        Assert.That(handled, Is.True);
        Assert.That(asked, Is.True);
    }

    [Test]
    public void MainMenuStartsScene()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(_scenes.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("XANADU"), Is.True);
    }

    [Test]
    public void EditWithNoLevelsStartsANewOne()
    {
        _catalog.Clear();

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_scenes.EditSelected(), Is.True);
        Assert.That(
            _store.Query<SceneState>().ToEntityList()[0].GetComponent<SceneState>().Pending,
            Is.EqualTo(SceneKind.Editor));
    }

    private void AddLevels(int count)
    {
        LevelEntry first = _catalog[0];
        while (_catalog.Count < count)
            _catalog.Add(new LevelEntry($"{first.Path}-{_catalog.Count}", first.Document));
    }

    private void DragBar(FakePointerInput pointer, Entity track, float y)
    {
        pointer.Current = new PointerInput(new Vector2(0, y), true, false, false, false);
        if (!track.Tags.Has<Pressed>())
            track.AddTag<Pressed>();
        Frame();
    }

    [Test]
    public void ReleasingTheListScrollbarLetsGo()
    {
        AddLevels(20);
        var pointer = new FakePointerInput();
        _root.Add(new SongSelectScrollbarSystem(pointer, _scenes));
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        Entity track = _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>().ListTrack;

        DragBar(pointer, track, 5000);
        track.RemoveTag<Pressed>();
        Frame();

        pointer.Current = new PointerInput(new Vector2(0, -5000), false, false, false, false);
        Frame();
        Assert.That(SelectedLevel(), Is.EqualTo(19));
    }

    [Test]
    public void ASingleLevelHasNothingToDrag()
    {
        _catalog.RemoveAt(1);
        var pointer = new FakePointerInput();
        _root.Add(new SongSelectScrollbarSystem(pointer, _scenes));
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        Entity track = _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>().ListTrack;

        DragBar(pointer, track, 5000);

        Assert.That(SelectedLevel(), Is.EqualTo(0));
    }

    [Test]
    public void DraggingTheScoresScrollbarScrollsThem()
    {
        var pointer = new FakePointerInput();
        _root.Add(new SongSelectScrollbarSystem(pointer, _scenes));
        _root.Add(new SongSelectScrollSystem());
        _root.Add(new ContainerLayoutSystem());
        _root.Add(new ScrollSystem());
        for (int i = 0; i < 8; i++)
        {
            _profiles.Create($"Player {i}");
            _profiles.RecordScore(
                _catalog[0].Path,
                new PlayResults("First Level", 900_000 - i * 1000, 0.9, 10, 10, 20));
        }

        _scenes.Start(SceneKind.SongSelect);
        Frame();
        Frame();
        SongSelectState view = _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>();

        DragBar(pointer, view.ScoreTrack, 5000);
        Frame();

        float content = view.ScoreContent.Entity.GetComponent<Rectangle>().Bounds.Size.Y;
        Assert.That(view.ScoreViewport.GetComponent<ScrollView>().Offset, Is.GreaterThan(0));
        Assert.That(
            SongSelectScene.ScoreValue(
                _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>()),
            Is.EqualTo(1f).Within(0.001f),
            $"content {content}");
    }

    [Test]
    public void SongSelectPlaysTheSelectedSongAndStopsOnLeaving()
    {
        string first = Path.Combine(_directory, "first.wav");
        string second = Path.Combine(_directory, "second.wav");
        File.WriteAllBytes(first, [0]);
        File.WriteAllBytes(second, [0]);
        _catalog[0] = _catalog[0] with
        {
            Song = TestSongs.At(first, "a")
        };
        _catalog[1] = _catalog[1] with
        {
            Song = TestSongs.At(second, "b")
        };

        var audio = new FakeTrackAudio();
        _root.Add(new Xanadu.Logic.Audio.SongPreviewSystem(_store, audio));

        _scenes.Start(SceneKind.SongSelect);
        Frames(10);
        Assert.That(audio.Loaded, Is.Empty, "a new selection waits to settle");

        Frames(20);
        Assert.That(audio.Loaded, Is.EqualTo(new[] { first }));
        Assert.That(audio.IsPlaying, Is.True);

        _scenes.StepSongSelect(1);
        Frames(30);
        Assert.That(audio.Loaded, Is.EqualTo(new[] { first, second }));

        _scenes.Start(SceneKind.Main);
        Frames(3);
        Assert.That(audio.IsPlaying, Is.False);
    }

    [Test]
    public void FinishedPlayStoresTheScoreForTheActiveProfile()
    {
        _profiles.Create("Scored");
        var document = new LevelDocument { Name = "Scored Level" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        string path = Path.Combine(_directory, "scored-level.xmf");
        LevelStore.Save(document, path);

        _scenes.StartPlay(new LevelEntry(path, document));
        Frame();
        _store.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>().Finished = true;
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
            PlayfieldHeight = 600
        };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });

        _scenes.StartPlay(new LevelEntry("memory://provided", document));
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(HasText("Provided Entry"), Is.True);
        Assert.That(HasText("SCORE 0000000"), Is.True);
    }

    [Test]
    public void FinishedPlayMovesToResultsWithFinalValues()
    {
        var document = new LevelDocument
        {
            Name = "Results Level",
            PlayfieldWidth = 800,
            PlayfieldHeight = 600
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
    public void DyingRestartsTheLevelFromItsStart()
    {
        var document = new LevelDocument { Name = "Spike Level" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        _scenes.StartPlay(new LevelEntry("memory://spike", document));
        Frame();

        ref PlaySession died = ref _store.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>();
        died.Died = true;
        died.Finished = true;
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        PlaySession fresh = _store.Query<PlaySession>().ToEntityList()[0].GetComponent<PlaySession>();
        Assert.That(fresh.Died, Is.False);
        Assert.That(fresh.Finished, Is.False);
        Assert.That(HasText("Spike Level"), Is.True);
    }

    [Test]
    public void PauseRetryRestartsTheLevel()
    {
        var document = new LevelDocument { Name = "Pause Retry" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        _scenes.StartPlay(new LevelEntry("memory://pause-retry", document));
        Frame();

        _scenes.TryBack();
        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.True);

        Pause().Retry.AddTag<Pressed>();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.False);
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
    public void BackFromSongSelectGoesToMain()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        _scenes.TryBack();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("XANADU"), Is.True);
    }

    private int SelectedLevel() =>
        _store.Query<SongSelectState>().ToEntityList()[0].GetComponent<SongSelectState>().Selected;

    [Test]
    public void BothArrowsTogetherDoNothing()
    {
        var keyboard = new FakeKeyboard();
        _root.Add(new SongSelectHoldSystem(keyboard, _scenes));
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        keyboard.Press(Key.Right);
        keyboard.Press(Key.Left);
        Frame();

        Assert.That(SelectedLevel(), Is.EqualTo(0));
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

        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.True);
        Assert.That(Pause().Title.GetComponent<Text>().Value, Is.EqualTo("PAUSED"));
        Assert.That(playhead.Playing, Is.False);
        Assert.That(playfield.Tags.Has<PlayPaused>(), Is.True);
        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));

        _scenes.TryForward();
        Frame();

        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.False);
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

        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.False);
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
    public void EnterInPlayRequestsASkipUnlessItPlaysANote()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();
        Entity play = _store.Query<PlaySession>().ToEntityList()[0];

        _scenes.TryForward();
        Assert.That(play.GetComponent<PlaySession>().SkipRequested, Is.True);

        play.GetComponent<PlaySession>().SkipRequested = false;
        _scenes.Keybinds.BeginCapture(KeybindAction.Action2);
        _scenes.Keybinds.TryCapture(Key.Enter);
        _scenes.TryForward();
        Assert.That(play.GetComponent<PlaySession>().SkipRequested, Is.False);
    }

    [Test]
    public void PauseHudButtonOpensPause()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();
        _scenes.TryForward();
        Frame();

        _store.Query<UiCommand>().ToEntityList()
            .First(command => command.GetComponent<UiCommand>().As<SceneActionId>() == SceneActionId.Pause)
            .AddTag<Pressed>();
        Frame();

        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.True);
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
        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.True);

        _scenes.TryBack();
        Frame();

        Assert.That(!UiLayout.IsHidden(PausePanel()), Is.False);
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
            true);
        _root.Add(transitional);

        var document = new LevelDocument { Name = "Fade Retry" };
        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        transitional.StartPlay(new LevelEntry("memory://fade-retry", document));
        Advance(20);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));

        transitional.TryBack();
        Advance(2);
        Assert.That(!UiLayout.IsHidden(transitional.PausePanel), Is.True);

        transitional.Pause.Retry.AddTag<Pressed>();

        Advance(40);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Play));
        Assert.That(!UiLayout.IsHidden(transitional.PausePanel), Is.False);
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
    public void MenuScenesShareOneBackground()
    {
        _scenes.Start(SceneKind.Profiles);
        Frame();
        Entity background = MenuBackground();

        foreach (SceneKind scene in new[] { SceneKind.Main, SceneKind.SongSelect, SceneKind.Main })
        {
            _scenes.Start(scene);
            Frame();
            Assert.That(MenuBackground(), Is.EqualTo(background), scene.ToString());
        }
    }

    [Test]
    public void SceneWithoutSharedBackgroundDropsItAndReturningRebuildsIt()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        _scenes.Start(SceneKind.Settings);
        Frame();
        Assert.That(_store.Query<SceneBackground>().Count, Is.EqualTo(0));

        _scenes.Start(SceneKind.Main);
        Frame();
        Assert.That(_store.Query<SceneBackground>().Count, Is.EqualTo(1));
    }

    [Test]
    public void SharedBackgroundFadesOnlyTheSceneLayers()
    {
        var transitional = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => _catalog,
            _keybinds,
            true);
        _root.Add(transitional);
        transitional.Start(SceneKind.Profiles);
        Advance(40);
        Entity background = MenuBackground();

        transitional.Start(SceneKind.Main);
        Advance(5);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Profiles));
        Assert.That(MenuLayerOpacity(), Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(UiLayout.LayerOpacityOf(background), Is.EqualTo(1f));
        Assert.That(TransitionOverlayAlpha(), Is.EqualTo(0f), "no fade to black");

        Advance(40);

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(MenuBackground(), Is.EqualTo(background));
        Assert.That(MenuLayerOpacity(), Is.EqualTo(1f));
    }

    [Test]
    public void LeavingTheSharedBackgroundStillFadesThroughBlack()
    {
        var transitional = new SceneSystem(
            _store,
            _director,
            new Vector2(1280, 720),
            new FakeVisualRidFactory(),
            () => _catalog,
            _keybinds,
            true);
        _root.Add(transitional);
        transitional.Start(SceneKind.Main);
        Advance(40);

        transitional.Start(SceneKind.Settings);
        Advance(5);

        Assert.That(MenuLayerOpacity(), Is.EqualTo(1f));
        Assert.That(TransitionOverlayAlpha(), Is.GreaterThan(0f));
    }

    private float MenuLayerOpacity() =>
        UiLayout.LayerOpacityOf(
            _store.Query<Layer>().ToEntityList().Single(layer => layer.GetComponent<Layer>().ZIndex == UiLayers.Menu));

    private float TransitionOverlayAlpha()
    {
        Entity layer = _store.Query<Layer>().ToEntityList()
            .Single(entity => entity.GetComponent<Layer>().ZIndex == UiLayers.SceneTransition);
        foreach (Entity panel in layer.ChildEntities)
            return panel.GetComponent<FillColor>().Value.A;

        throw new InvalidOperationException("The transition layer has no panel.");
    }

    private Entity MenuBackground() =>
        _store.Query<SceneBackground>().ToEntityList().Single();

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
        string path = Path.Combine(_directory, name.Replace(' ', '-') + XmfBinary.Extension);
        var document = new LevelDocument
        {
            Name = name
        };
        document.Notes.Add(
            new NoteDocument
            {
                TimeMs = 500,
                X = 100,
                Y = 200
            });

        LevelStore.Save(document, path);
        return new LevelEntry(path, LevelStore.Load(path));
    }

    private Entity PausePanel() => _scenes.PausePanel;

    private ScenePause Pause() => _scenes.Pause;

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

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            Frame();
    }

    private sealed class FakeTrackAudio : Xanadu.Audio.ITrackAudio
    {
        public List<string> Loaded { get; } = new();

        public bool IsPlaying { get; private set; }

        public float PositionSeconds => 0;

        public float LengthSeconds => 60;

        public bool Play(string audioPath, float volume) => Load(audioPath, volume) && Resume();

        public bool Load(string audioPath, float volume)
        {
            IsPlaying = false;
            Loaded.Add(audioPath);
            return true;
        }

        public bool Resume() => IsPlaying = true;

        public void Pause() => IsPlaying = false;

        public void Seek(float seconds)
        {
        }

        public void SetVolume(float volume)
        {
        }

        public void Stop() => IsPlaying = false;

        public void Dispose()
        {
        }
    }

    private void Advance(int frames)
    {
        for (int i = 0; i < frames; i++)
            Frame();
    }
}
