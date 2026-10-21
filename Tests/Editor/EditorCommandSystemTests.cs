using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Levels;
using Writer.Editor;
using Writer.Editor.Commands;
using Writer.Settings;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorCommandSystemTests
{
    private EntityStore _store = null!;
    private EditorCommandBus _commands = null!;
    private Entity _editor;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _commands = new EditorCommandBus();
        _root = new SystemRoot(_store);
        _root.Add(new EditorCommandSystem(
            _store,
            _commands,
            new EditorHistory(_store)));

        _editor = _store.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorPreview(false, 0, false),
            new EditorMetronome(false),
            new NoteSelection(default));
    }

    [Test]
    public void AppliesMetadataAndClampsTiming()
    {
        _commands.Enqueue(EditorCommand.SetName("Level"));
        _commands.Enqueue(EditorCommand.SetArtist("Artist"));
        _commands.Enqueue(EditorCommand.SetDifficulty("Hard"));
        _commands.Enqueue(EditorCommand.SetBpm(0));
        _commands.Enqueue(EditorCommand.SetOffset(900_000));
        _commands.Enqueue(EditorCommand.SetSnap(7));

        _root.Update(new UpdateTick(1f / 60f, 0));

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Playhead playhead = _editor.GetComponent<Playhead>();
        Assert.That(level.Name, Is.EqualTo("Level"));
        Assert.That(level.Artist, Is.EqualTo("Artist"));
        Assert.That(level.Difficulty, Is.EqualTo("Hard"));
        Assert.That(playhead.Bpm, Is.EqualTo(1));
        Assert.That(playhead.OffsetMs, Is.EqualTo(600_000));
        Assert.That(playhead.SnapDivision, Is.EqualTo(4));
    }

    [Test]
    public void TogglePlaybackAtEndSeeksToStart()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 1_000;

        _commands.Enqueue(EditorCommand.TogglePlayback());
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.Playing, Is.True);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(0));
    }

    [Test]
    public void PreviewRoundTripRestoresSavedState()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 500;

        _commands.Enqueue(EditorCommand.BeginPreview());
        _root.Update(new UpdateTick(1f / 60f, 0));

        EditorPreview preview = _editor.GetComponent<EditorPreview>();
        Assert.That(preview.Active, Is.True);
        Assert.That(preview.SavedTimeMs, Is.EqualTo(500));
        Assert.That(playhead.Playing, Is.True);

        _commands.Enqueue(EditorCommand.EndPreview());
        _root.Update(new UpdateTick(1f / 60f, 0));

        preview = _editor.GetComponent<EditorPreview>();
        Assert.That(preview.Active, Is.False);
        Assert.That(playhead.Playing, Is.False);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(500));
    }

    [Test]
    public void DeleteSelectionIsUndoable()
    {
        Entity note = _store.CreateEntity(new Note(500, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.DeleteSelection());
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(0));

        _commands.Enqueue(EditorCommand.Undo());
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void SetTargetIsUndoable()
    {
        Entity note = _store.CreateEntity(
            new Note(500, new Vector2(10, 20), target: KeybindAction.M));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.SetTarget(KeybindAction.R3));
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            _store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));

        _commands.Enqueue(EditorCommand.Undo());
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            _store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.M));
    }

    [Test]
    public void LoadSongAdoptsFilenameAndBumpsRevision()
    {
        _commands.Enqueue(EditorCommand.LoadSong("/tmp/Test Song.mp3"));
        _root.Update(new UpdateTick(1f / 60f, 0));

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Playhead playhead = _editor.GetComponent<Playhead>();
        Assert.That(level.AudioPath, Is.EqualTo("/tmp/Test Song.mp3"));
        Assert.That(level.Name, Is.EqualTo("Test Song"));
        Assert.That(playhead.AudioRevision, Is.EqualTo(1));
    }

    [Test]
    public void ApplyLevelReplacesChartAndResetsPreview()
    {
        ref EditorPreview preview = ref _editor.GetComponent<EditorPreview>();
        preview.Active = true;

        var document = new LevelDocument
        {
            Name = "Loaded",
            Artist = "Artist",
            Difficulty = "Normal",
            AudioPath = "res://song.mp3",
            Bpm = 150,
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        document.Notes.Add(new NoteDocument
        {
            TimeMs = 750,
            X = 100,
            Y = 200,
            FadeInMs = 600,
            FadeOutMs = 600,
        });

        _commands.Enqueue(EditorCommand.ApplyLevel(document, "/tmp/loaded.json"));
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_editor.GetComponent<LevelInfo>().Name, Is.EqualTo("Loaded"));
        Assert.That(_editor.GetComponent<Playhead>().Bpm, Is.EqualTo(150));
        Assert.That(_editor.GetComponent<EditorPreview>().Active, Is.False);
        Assert.That(_editor.GetComponent<LevelFile>().Path, Is.EqualTo("/tmp/loaded.json"));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
    }
}
