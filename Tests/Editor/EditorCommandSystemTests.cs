using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Tests.Support;
using Writer.Ecs;
using Writer.Levels;
using Writer.Editor;
using Writer.Editor.Commands;
using Writer.Settings;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorCommandSystemTests : EcsSystemTest
{
    private EditorCommandBus _commands = null!;
    private Entity _editor;

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        Root.Add(new EditorCommandSystem(
            Store,
            _commands,
            new EditorHistory(Store)));

        _editor = Store.CreateEntity(
            new LevelInfo("Untitled", ""),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
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

        Frame();

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
        Frame();

        Assert.That(playhead.Playing, Is.True);
        Assert.That(playhead.SeekRequestMs, Is.EqualTo(0));
    }

    [Test]
    public void DeleteSelectionIsUndoable()
    {
        Entity note = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(0));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void DeleteSelectionRemovesEverySelectedNote()
    {
        Entity first = Store.CreateEntity(new Note(500, new Vector2(10, 20)));
        Entity second = Store.CreateEntity(new Note(750, new Vector2(30, 40)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.Zero);
        Assert.That(_editor.GetComponent<NoteSelection>().Notes, Is.Empty);
    }

    [Test]
    public void DeleteSelectionRemovesMinesAndStartPoints()
    {
        Entity mine = Store.CreateEntity(new Mine(500, new Vector2(10, 20)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 500, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.DeleteSelection());
        Frame();

        Assert.That(Store.Query<Mine>().Count, Is.Zero);
        Assert.That(Store.Query<StartPoint>().Count, Is.Zero);
        Assert.That(_editor.GetComponent<NoteSelection>().Notes, Is.Empty);
    }

    [Test]
    public void PasteIsOneUndoableStep()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 10_000;
        Entity note = Store.CreateEntity(new Note(100, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { note });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();
        Assert.That(Store.Query<Note>().Count, Is.EqualTo(2));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(
            Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(100));
    }

    [Test]
    public void MoveSelectionNudgesEveryEditorItem()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        Entity mine = Store.CreateEntity(new Mine(1_000, new Vector2(10, 20)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 2_000, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();

        Assert.That(mine.GetComponent<Mine>().TimeMs, Is.EqualTo(1_500));
        Assert.That(start.GetComponent<StartPoint>().TimeMs, Is.EqualTo(2_500));
    }

    [Test]
    public void CopyPastePreservesRelativeTimingAndPosition()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 1_000;
        playhead.DurationMs = 10_000;

        Entity first = Store.CreateEntity(
            new Note(100, new Vector2(10, 20)));
        Entity second = Store.CreateEntity(
            new Note(300, new Vector2(30, 40)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Note[] notes = Store.Query<Note>().ToEntityList()
            .Select(entity => entity.GetComponent<Note>())
            .OrderBy(note => note.TimeMs)
            .ToArray();
        Assert.That(notes, Has.Length.EqualTo(4));
        Assert.That(notes.Select(note => note.TimeMs),
            Is.EqualTo(new long[] { 100, 300, 1_000, 1_200 }));
        Assert.That(notes[2].Position, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(notes[3].Position, Is.EqualTo(new Vector2(30, 40)));
        Assert.That(_editor.GetComponent<NoteSelection>().AllNotes.Count,
            Is.EqualTo(2));
    }

    [Test]
    public void CopyPasteHandlesMinesAndStartPoints()
    {
        ref Playhead playhead = ref _editor.GetComponent<Playhead>();
        playhead.TimeMs = 2_000;
        playhead.DurationMs = 10_000;
        Entity mine = Store.CreateEntity(new Mine(100, new Vector2(10, 20)));
        Entity start = Store.CreateEntity(new StartPoint(
            "Start", 400, new Vector2(30, 40),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { mine, start });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Assert.That(Store.Query<Mine>().Count, Is.EqualTo(2));
        Assert.That(Store.Query<StartPoint>().Count, Is.EqualTo(2));
        Assert.That(
            _editor.GetComponent<NoteSelection>().AllNotes.Count,
            Is.EqualTo(2));
    }

    [Test]
    public void SetTargetIsUndoable()
    {
        Entity note = Store.CreateEntity(
            new Note(500, new Vector2(10, 20), target: KeybindAction.M));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.SetTarget(KeybindAction.R3));
        Frame();

        Assert.That(
            Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));

        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(
            Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.M));
    }

    [Test]
    public void SetPlacementModeSwitchesTheEditorMode()
    {
        _editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Notes));

        _commands.Enqueue(
            EditorCommand.SetPlacementMode(EditorPlacementMode.Movement));
        Frame();

        Assert.That(
            _editor.GetComponent<EditorPlacementModeState>().Mode,
            Is.EqualTo(EditorPlacementMode.Movement));
    }

    [Test]
    public void ZoomsTheEditorTimeline()
    {
        _editor.AddComponent(new TimelineView(1f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(4f));

        _commands.Enqueue(EditorCommand.ZoomTimeline(-1));
        Frame();
        Assert.That(_editor.GetComponent<TimelineView>().Zoom, Is.EqualTo(2f));
    }

    [Test]
    public void MovesSelectedNoteByOneSnapStepAndUndoes()
    {
        Entity note = Store.CreateEntity(new Note(1_000, new Vector2(10, 20)));
        _editor.GetComponent<NoteSelection>().Note = note;

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();

        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(1_500));

        _commands.Enqueue(EditorCommand.MoveSelection(-1));
        Frame();

        Assert.That(note.GetComponent<Note>().TimeMs, Is.EqualTo(1_000));

        _commands.Enqueue(EditorCommand.MoveSelection(1));
        Frame();
        _commands.Enqueue(EditorCommand.Undo());
        Frame();

        Assert.That(Store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(1_000));
    }

    [Test]
    public void LoadSongAdoptsFilenameAndBumpsRevision()
    {
        _commands.Enqueue(EditorCommand.LoadSong("/tmp/Test Song.mp3"));
        Frame();

        LevelInfo level = _editor.GetComponent<LevelInfo>();
        Playhead playhead = _editor.GetComponent<Playhead>();
        Assert.That(level.AudioPath, Is.EqualTo("/tmp/Test Song.mp3"));
        Assert.That(level.Name, Is.EqualTo("Test Song"));
        Assert.That(playhead.AudioRevision, Is.EqualTo(1));
    }

    [Test]
    public void ApplyLevelReplacesChart()
    {
        var document = new LevelDocument
        {
            Name = "Loaded",
            PlayfieldWidth = 1000,
            PlayfieldHeight = 1000,
        };
        document.Notes.Add(new NoteDocument
        {
            TimeMs = 750,
            X = 100,
            Y = 200,
        });

        _commands.Enqueue(EditorCommand.ApplyLevel(document, "/tmp/loaded.json"));
        Frame();

        Assert.That(_editor.GetComponent<LevelInfo>().Name, Is.EqualTo("Loaded"));
        Assert.That(_editor.GetComponent<Playhead>().Bpm, Is.EqualTo(120));
        Assert.That(_editor.GetComponent<LevelFile>().Path, Is.EqualTo("/tmp/loaded.json"));
        Assert.That(Store.Query<Note>().Count, Is.EqualTo(1));
    }
}
