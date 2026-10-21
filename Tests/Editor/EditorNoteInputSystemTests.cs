using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Editor;
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
            new EditorPlayhead(1_000, 120, 0, false),
            new EditorView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
    }

    [Test]
    public void ClickWithoutSongRequestsWarningAndSkipsNote()
    {
        Entity editor = _store.Query<EditorView>().ToEntityList()[0];
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.AudioPath = "";

        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
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

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(1));
        Assert.That(notes[0].GetComponent<Note>().TimeMs, Is.EqualTo(1_000));
        Assert.That(notes[0].GetComponent<Note>().Position, Is.EqualTo(new Vector2(100, 200)));

        Entity editor = _store.Query<EditorView>().ToEntityList()[0];
        Assert.That(editor.GetComponent<NoteSelection>().Note.Id, Is.EqualTo(notes[0].Id));
    }

    [Test]
    public void ClickingExistingNoteSelectsWithoutAdding()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(new Vector2(100, 200), false);
        _root.Update(new UpdateTick(1f / 60f, 0));

        Entity editor = _store.Query<EditorView>().ToEntityList()[0];
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

        var notes = _store.Query<Note>().ToEntityList();
        Assert.That(notes.Count, Is.EqualTo(2));
    }

    [Test]
    public void DeletedDraggedNoteDoesNotThrow()
    {
        _pointer.Current = new PointerInput(new Vector2(100, 200), true);
        _root.Update(new UpdateTick(1f / 60f, 0));

        var notes = _store.Query<Note>().ToEntityList();
        notes[0].DeleteEntity();

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
