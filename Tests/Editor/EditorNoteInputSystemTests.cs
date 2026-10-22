using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Editor;
using Writer.Settings;
using Writer.Systems.Input;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorNoteInputSystemTests
{
    private EntityStore _store = null!;
    private ScriptedPointer _pointer = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new ScriptedPointer();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, _history));

        _store.CreateEntity(
            new LevelInfo("Test", "/tmp/test.wav"),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
    }

    [Test]
    public void ClickWithoutSongRequestsWarningAndSkipsNote()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.AudioPath = "";

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(0));
        Assert.That(editor.HasComponent<WarningRequest>(), Is.True);
        Assert.That(
            editor.GetComponent<WarningRequest>().Message,
            Is.EqualTo(EditorWarningPopup.NoSongMessage));
    }

    [Test]
    public void ClickAddsSnappedNoteAndSelectsIt()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(1));
        Assert.That(notes[0].GetComponent<Note>().TimeMs, Is.EqualTo(1_000));
        Assert.That(notes[0].GetComponent<Note>().Position, Is.EqualTo(new Vector2(100, 200)));

        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        Assert.That(editor.GetComponent<NoteSelection>().Note.Id, Is.EqualTo(notes[0].Id));
    }

    [Test]
    public void NewNotesKeepTheSelectedTarget()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorNoteTarget(KeybindAction.R3));

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        editor.GetComponent<NoteSelection>().Note = default;

        _pointer.Current = new PointerInput(new Vector2(700, 700), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(700, 700), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(2));
        Assert.That(
            notes[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
        Assert.That(
            notes[1].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
    }

    [Test]
    public void DraggingTheTimelineSeeksContinuously()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.DurationMs = 10_000;
        playhead.TimeMs = 1_000;

        var view = editor.GetComponent<PlayfieldView>();
        Rect2 ruler = EditorLayout.RulerRect(view);
        _pointer.Current = new PointerInput(ruler.GetCenter(), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(playhead.SeekRequestMs, Is.EqualTo(5_000));
    }

    [Test]
    public void MineModePlacesMinesInsteadOfNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Mines));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.Zero);
        var mines = _store.Query<Mine>().ToEntityList();
        Assert.That(mines, Has.Count.EqualTo(1));
        Assert.That(mines[0].GetComponent<Mine>().TimeMs, Is.EqualTo(1_000));
        Assert.That(
            mines[0].GetComponent<Mine>().Position,
            Is.EqualTo(new Vector2(300, 400)));
        Assert.That(_history.CanUndo, Is.True);
    }

    [Test]
    public void MovementModePlacesADirectionalMovementNote()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Movement));
        editor.AddComponent(
            new EditorDashDirectionState(DashDirection.UpRight));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes, Has.Count.EqualTo(1));
        Assert.That(notes[0].GetComponent<Note>().Kind,
            Is.EqualTo(NoteKind.Movement));
        Assert.That(notes[0].GetComponent<Note>().DashDirection,
            Is.EqualTo(DashDirection.UpRight));
        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void UndoRemovesAPlacedMine()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorPlacementModeState(EditorPlacementMode.Mines));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_history.Undo(), Is.True);

        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void SidebarClicksDoNotPlaceNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(new EditorSidebar(new Rect2(800, 50, 200, 500)));

        _pointer.Current = new PointerInput(new Vector2(900, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.Zero);
    }

    [Test]
    public void ClickingExistingNoteSelectsWithoutAdding()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.GetComponent<NoteSelection>().Note = default;

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(editor.GetComponent<NoteSelection>().Note.IsNull, Is.False);
    }

    [Test]
    public void InvisibleNoteDoesNotCaptureClick()
    {
        _store.CreateEntity(new Note(5_000, new Vector2(100, 200)));
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(2));
    }

    [Test]
    public void DraggingEmptySpaceSelectsWithoutCreating()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));
        _store.CreateEntity(new Note(1_000, new Vector2(700, 700)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(3));
        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));

        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(3));
    }

    [Test]
    public void MarqueeSkipsInvisibleNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(5_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            editor.GetComponent<NoteSelection>().Notes,
            Has.Count.EqualTo(1));
    }

    [Test]
    public void MineModeMarqueeSelectsWithoutPlacingAMine()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Mines));
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(_store.Query<Mine>().Count, Is.Zero);
    }

    [Test]
    public void MovementModeMarqueeSelectsNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Movement));
        _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));

        _pointer.Current = new PointerInput(new Vector2(50, 50), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(400, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(_store.Query<Note>().Count, Is.EqualTo(2));
    }

    [Test]
    public void DraggingAMultiSelectionMovesAllSelectedNotes()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        Entity first = _store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        Entity second = _store.CreateEntity(new Note(1_000, new Vector2(300, 300)));
        editor.GetComponent<NoteSelection>().SetMany(new[] { first, second });

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(250, 250), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(250, 250), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(editor.GetComponent<NoteSelection>().Notes, Has.Count.EqualTo(2));
        Assert.That(first.GetComponent<Note>().Position, Is.EqualTo(new Vector2(250, 250)));
        Assert.That(second.GetComponent<Note>().Position, Is.EqualTo(new Vector2(350, 350)));
    }

    [Test]
    public void DraggingAMineMovesIt()
    {
        Entity mine = _store.CreateEntity(
            new Mine(1_000, new Vector2(200, 200)));

        _pointer.Current = new PointerInput(new Vector2(200, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(260, 270), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(mine.GetComponent<Mine>().Position,
            Is.EqualTo(new Vector2(260, 270)));
    }

    [Test]
    public void StartModePlacesAPracticeStart()
    {
        Entity editor = _store.Query<PlayfieldView>().ToEntityList()[0];
        editor.AddComponent(
            new EditorPlacementModeState(EditorPlacementMode.Start));

        _pointer.Current = new PointerInput(new Vector2(300, 400), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(300, 400), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var starts = _store.Query<StartPoint>().ToEntityList();
        Assert.That(starts, Has.Count.EqualTo(1));
        Assert.That(starts[0].GetComponent<StartPoint>().TimeMs,
            Is.EqualTo(1_000));
        Assert.That(starts[0].GetComponent<StartPoint>().Position,
            Is.EqualTo(new Vector2(300, 400)));
    }

    [Test]
    public void DeletedDraggedNoteDoesNotThrow()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        notes[0].DeleteEntity();

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        Assert.DoesNotThrow(() =>
            _root.Update(new UpdateTick(1f / 60f, 0)));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        Assert.DoesNotThrow(() =>
            _root.Update(new UpdateTick(1f / 60f, 0)));
    }

    private sealed class ScriptedPointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }
}
