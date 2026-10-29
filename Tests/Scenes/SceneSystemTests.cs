using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Profiles;
using Xanadu.Settings;
using Xanadu.Systems.Scenes;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Scenes;

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
    public void MainMenuStartsScene()
    {
        _scenes.Start(SceneKind.Main);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(_store.Query<SceneRoot>().Count, Is.EqualTo(1));
        Assert.That(_store.Query<SceneRoot>().ToEntityList()[0].GetComponent<SceneRoot>().Scene,
            Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("XANADU"), Is.True);
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
        Assert.That(view.Names[4].GetComponent<Text>().Value,
            Is.EqualTo("First Level"));

        Entity step = _store.CreateEntity(new SceneCommand(SceneActionId.Next), Tags.Get<Pressed>());
        Frame();

        SettleScroll(root);
        view = root.GetComponent<SongSelectState>();
        Assert.That(view.Selected, Is.EqualTo(1));
        Assert.That(view.Names[4].GetComponent<Text>().Value,
            Is.EqualTo("Second Level"));

        step.DeleteEntity();
    }

    [Test]
    public void SongSelectWithNoLevelsInvitesCreatingOne()
    {
        _catalog.Clear();

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.SongSelect));
        Assert.That(HasText("NO LEVELS FOUND"), Is.True);
        Assert.That(HasText("CREATE A LEVEL IN THE EDITOR"), Is.True);

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(view.Count, Is.EqualTo(0));
        Assert.That(view.Thumb.GetComponent<UiShape>().Points,
            Has.Length.EqualTo(1), "an empty list has no scrollbar thumb");

        // The hint is a button: it takes input and asks for the editor.
        Entity hint = _store.Query<Text>().ToEntityList()
            .First(entity => entity.GetComponent<Text>().Value ==
                             "CREATE A LEVEL IN THE EDITOR");
        Assert.That(hint.Tags.Has<TakesInput>(), Is.True);
        Assert.That(
            hint.GetComponent<SceneCommand>().Action,
            Is.EqualTo(SceneActionId.Edit));
    }

    [Test]
    public void EditWithNoLevelsStartsANewOne()
    {
        _catalog.Clear();

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_scenes.EditSelected(), Is.True);
        Assert.That(
            _store.Query<Xanadu.Ecs.SceneState>().ToEntityList()[0]
                .GetComponent<Xanadu.Ecs.SceneState>().Pending,
            Is.EqualTo(SceneKind.Editor));
    }

    [Test]
    public void SongSelectWithLevelsHidesTheCreateLevelHint()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(HasText("NO LEVELS FOUND"), Is.False);
        Assert.That(HasText("CREATE A LEVEL IN THE EDITOR"), Is.False);

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        Assert.That(
            root.GetComponent<SongSelectState>().Thumb.GetComponent<UiShape>().Points,
            Has.Length.EqualTo(2));
    }

    private static void SettleScroll(Entity root)
    {
        ref SongSelectState view = ref root.GetComponent<SongSelectState>();
        for (int i = 0; i < 600 && SongSelectScene.AdvanceScroll(ref view, 1f / 60f); i++)
        {
        }
    }

    [Test]
    public void SongSelectScrollEasesBetweenSlotsThenRests()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        float restY = root.GetComponent<SongSelectState>()
            .Tiles[4].GetComponent<Xanadu.Ecs.Position>().Value.Y;

        Entity step = _store.CreateEntity(new SceneCommand(SceneActionId.Next), Tags.Get<Pressed>());
        Frame();
        step.DeleteEntity();

        ref SongSelectState view = ref root.GetComponent<SongSelectState>();
        Assert.That(SongSelectScene.AdvanceScroll(ref view, 1f / 60f), Is.True);
        Assert.That(view.Scroll, Is.GreaterThan(0).And.LessThan(1));
        float midY = view.Tiles[4].GetComponent<Xanadu.Ecs.Position>().Value.Y;
        Assert.That(midY, Is.LessThan(restY));

        SettleScroll(root);
        Assert.That(view.Scroll, Is.EqualTo(1f));
        Assert.That(
            view.Tiles[4].GetComponent<Xanadu.Ecs.Position>().Value.Y,
            Is.EqualTo(restY).Within(0.01f));
        Assert.That(SongSelectScene.AdvanceScroll(ref view, 1f / 60f), Is.False);
    }

    [Test]
    public void SongSelectScrollAdvancesAcrossFramesInTheSystemRoot()
    {
        _root.Add(new Xanadu.Systems.Scenes.SongSelectScrollSystem());
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(_scenes.TryStepCarousel(1), Is.True);
        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        Frame();
        float first = root.GetComponent<SongSelectState>().Scroll;
        Frame();
        float second = root.GetComponent<SongSelectState>().Scroll;

        Assert.That(first, Is.GreaterThan(0).And.LessThan(1));
        Assert.That(second, Is.GreaterThan(first).And.LessThan(1));
    }

    [Test]
    public void SongSelectTileClickSelectsThatLevel()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Entity tile = _store
            .Query<SongSelectTile>()
            .ToEntityList()
            .First(entity => entity.GetComponent<SongSelectTile>().Index == 1);
        tile.AddTag<Pressed>();
        Frame();

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SettleScroll(root);
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(view.Selected, Is.EqualTo(1));
        Assert.That(
            view.Names[4].GetComponent<Text>().Value,
            Is.EqualTo("Second Level"));
        Assert.That(
            _store.Query<Xanadu.Ecs.SceneState>().ToEntityList()[0]
                .GetComponent<Xanadu.Ecs.SceneState>().SelectedLevelPath,
            Is.EqualTo(_catalog[1].Path));
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
    public void SongSelectListsAllProfileScores()
    {
        UserProfile profile = _profiles.Create("Scored");
        _profiles.RecordScore(
            _catalog[0].Path,
            new PlayResults("First Level", 900_000, 0.97, 42, 50, 55));

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(view.Tiles, Has.Count.EqualTo(9));
        Assert.That(
            view.Names[4].GetComponent<Text>().Value,
            Is.EqualTo("First Level"));
    }

    [Test]
    public void ScoreBarsShowAccuracyThenScoreHighestFirst()
    {
        _profiles.Create("Low");
        _profiles.RecordScore(
            _catalog[0].Path,
            new PlayResults("First Level", 400_000, 0.5, 10, 10, 20));
        _profiles.Create("High");
        _profiles.RecordScore(
            _catalog[0].Path,
            new PlayResults("First Level", 900_000, 0.9876, 42, 50, 55));

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        SongSelectState view = _store.Query<SceneRoot>().ToEntityList()[0]
            .GetComponent<SongSelectState>();
        Assert.That(view.ScoreAccuracies[0].GetComponent<Text>().Value, Is.EqualTo("98.76%"));
        Assert.That(view.ScoreValues[0].GetComponent<Text>().Value, Is.EqualTo("0900000"));
        Assert.That(view.ScoreAccuracies[1].GetComponent<Text>().Value, Is.EqualTo("50.00%"));
        Assert.That(view.ScoreValues[1].GetComponent<Text>().Value, Is.EqualTo("0400000"));
        Assert.That(view.ScoreValues[2].GetComponent<Text>().Value, Is.Empty);
    }

    [Test]
    public void ScoresScrollAndTheScrollbarFollows()
    {
        _root.Add(new Xanadu.Systems.Scenes.SongSelectScrollSystem());
        _root.Add(new Xanadu.Systems.Layout.ContainerLayoutSystem());
        _root.Add(new Xanadu.Systems.Ui.ScrollSystem());
        for (int i = 0; i < 8; i++)
        {
            _profiles.Create($"Player {i}");
            _profiles.RecordScore(
                _catalog[0].Path,
                new PlayResults("First Level", 900_000 - i * 1000, 0.9, 10, 10, 20));
        }

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Entity root = _store.Query<SceneRoot>().ToEntityList()[0];
        SongSelectState view = root.GetComponent<SongSelectState>();
        Assert.That(
            view.ScoreContent.GetComponent<Rectangle>().Bounds.Size.Y,
            Is.GreaterThan(224));
        float top = view.ScoreThumbSpan.X;

        view.ScoreViewport.GetComponent<ScrollView>().Offset = 1000;
        Frame();
        Frame();

        view = root.GetComponent<SongSelectState>();
        Assert.That(view.ScoreThumbSpan.X, Is.GreaterThan(top));
        Assert.That(
            view.ScoreThumbSpan.X + view.ScoreThumbSpan.Y,
            Is.EqualTo(1036.348f).Within(0.01f));
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
            Song = new SongDocument { Id = "a", AudioPath = first },
            SongAvailable = true,
        };
        _catalog[1] = _catalog[1] with
        {
            Song = new SongDocument { Id = "b", AudioPath = second },
            SongAvailable = true,
        };

        var audio = new FakeTrackAudio();
        _root.Add(new Xanadu.Systems.Audio.SongPreviewSystem(_store, audio));

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

        Entity playfield = _store.Query<PlayfieldMode>().ToEntityList()[0];
        Assert.That(playfield.GetComponent<PlayfieldMode>().Kind, Is.EqualTo(PlayfieldKind.Play));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void PlaySceneScalesThePlayerWithThePlayfield()
    {
        var document = new LevelDocument
        {
            Name = "Scaled Level",
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };

        _scenes.StartPlay(new LevelEntry("memory://scaled", document));
        Frame();

        Entity playfield = _store.Query<PlayfieldMode>().ToEntityList()[0];
        float scale = playfield.GetComponent<PlayfieldView>().Scale;
        Vector2 size = playfield.GetComponent<Player>().Size;

        // The 1280x720 test screen cannot fit a 1000px field, so the player has
        // to come out smaller than 200 px too, in the same proportion.
        Assert.That(scale, Is.LessThan(1f));
        Assert.That(size, Is.EqualTo(Player.DefaultSize * scale));
        Assert.That(size.Y / size.X, Is.EqualTo(200f / 30f).Within(0.0001f));
    }

    [Test]
    public void PlaySceneStartsThePlayerOnTheFloor()
    {
        var document = new LevelDocument
        {
            Name = "Floor Level",
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };

        _scenes.StartPlay(new LevelEntry("memory://floor", document));
        Frame();

        Entity playfield = _store.Query<PlayfieldMode>().ToEntityList()[0];
        PlayfieldView view = playfield.GetComponent<PlayfieldView>();
        Playfield field = playfield.GetComponent<Playfield>();
        Player player = playfield.GetComponent<Player>();
        Vector2 position = playfield.GetComponent<Xanadu.Ecs.Position>().Value;

        float floor = view.ScreenOrigin.Y + field.Height * view.Scale;
        float centerX = view.ScreenOrigin.X + field.Width * view.Scale / 2;
        Assert.That(position.X, Is.EqualTo(centerX).Within(0.5f));
        Assert.That(position.Y + player.Size.Y / 2,
            Is.EqualTo(floor).Within(0.5f));
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
        Assert.That(playfield.GetComponent<Xanadu.Ecs.Position>().Value,
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

        _store.Query<SceneCommand>().ToEntityList().First(command => command.GetComponent<SceneCommand>().Action == SceneActionId.Retry).AddTag<Pressed>();
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
    public void BackFromSongSelectGoesToMain()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        _scenes.TryBack();
        Frame();

        Assert.That(_director.Current, Is.EqualTo(SceneKind.Main));
        Assert.That(HasText("XANADU"), Is.True);
    }

    [Test]
    public void ClickingTheLevelArtPlaysAndEditIsADiamond()
    {
        _scenes.Start(SceneKind.SongSelect);
        Frame();

        SongSelectState view = _store.Query<SceneRoot>().ToEntityList()[0]
            .GetComponent<SongSelectState>();
        Assert.That(view.Play.GetComponent<SceneCommand>().Action, Is.EqualTo(SceneActionId.Play));
        Assert.That(view.Play.Tags.Has<TakesInput>(), Is.True);
        Assert.That(view.Play.HasComponent<UiMaterial>(), Is.False, "the test factory loads no shaders");

        Assert.That(view.Edit.GetComponent<SceneCommand>().Action, Is.EqualTo(SceneActionId.Edit));
        Assert.That(view.Edit.Tags.Has<TakesInput>(), Is.True);
        Assert.That(view.Edit.HasComponent<UiDiamond>(), Is.True);
        Assert.That(view.Edit.GetComponent<UiContainer>().Skew, Is.Zero);
        Assert.That(view.ActionIcons, Is.Not.Empty);
    }

    [Test]
    public void SongSelectInfoShowsDifficultyThenGenreTitleAndArtist()
    {
        _catalog[0] = _catalog[0] with
        {
            Song = new SongDocument
            {
                Id = "a",
                Title = "Choo Choo Train",
                Artist = "Slake",
                Genre = "J-Revival",
            },
            SongAvailable = true,
        };

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        SongSelectState view = _store.Query<SceneRoot>().ToEntityList()[0]
            .GetComponent<SongSelectState>();
        Assert.That(view.InfoDifficulty.GetComponent<Text>().Value, Is.EqualTo("FIRST LEVEL"));
        Assert.That(view.InfoGenre.GetComponent<Text>().Value, Is.EqualTo("J-REVIVAL"));
        Assert.That(view.InfoTitle.GetComponent<Text>().Value, Is.EqualTo("Choo Choo Train"));
        Assert.That(view.InfoArtist.GetComponent<Text>().Value, Is.EqualTo("Slake"));
    }

    [Test]
    public void LongTitlesShrinkToFitTheInfoCard()
    {
        _catalog[0] = _catalog[0] with
        {
            Song = new SongDocument { Id = "a", Title = new string('W', 40) },
            SongAvailable = true,
        };

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        SongSelectState view = _store.Query<SceneRoot>().ToEntityList()[0]
            .GetComponent<SongSelectState>();
        Assert.That(view.InfoTitle.GetComponent<TextStyle>().FontSize, Is.LessThan(46));
        Assert.That(view.InfoTitle.GetComponent<TextStyle>().FontSize, Is.GreaterThanOrEqualTo(24));
    }

    [Test]
    public void MissingSongBlocksPlayAndEdit()
    {
        _catalog.Clear();
        var document = new LevelDocument
        {
            Name = "Broken",
            SongId = "missing-song",
        };
        document.Notes.Add(new NoteDocument { TimeMs = 500, X = 100, Y = 200 });
        _catalog.Add(new LevelEntry("memory://broken", document, null));

        _scenes.Start(SceneKind.SongSelect);
        Frame();

        Assert.That(HasText("[SONG MISSING]"), Is.True);

        SongSelectState view = _store.Query<SceneRoot>().ToEntityList()[0]
            .GetComponent<SongSelectState>();
        Assert.That(view.Play.Tags.Has<TakesInput>(), Is.False);
        Assert.That(view.Edit.Tags.Has<TakesInput>(), Is.False);
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

        _store.Query<SceneCommand>().ToEntityList()
            .First(command => command.GetComponent<SceneCommand>().Action ==
                SceneActionId.Pause)
            .AddTag<Pressed>();
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
        string path = Path.Combine(_directory, name.Replace(' ', '-') + XmfBinary.Extension);
        var document = new LevelDocument
        {
            Name = name,
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

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            Frame();
    }

    private sealed class FakeTrackAudio : Xanadu.Systems.Audio.ITrackAudio
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
