using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorHistoryTests
{
    [Test]
    public void UndoRedoAddsAndRemovesNotes()
    {
        var store = new EntityStore();
        var history = new EditorHistory(store);

        EditorSnapshot before = history.Capture();
        store.CreateEntity(new Note(500, new Vector2(10, 20)));
        EditorSnapshot after = history.Capture();
        history.Record(before, after);

        Assert.That(history.CanUndo, Is.True);
        Assert.That(history.Undo(), Is.True);
        Assert.That(store.Query<Note>().Count, Is.EqualTo(0));
        Assert.That(history.Redo(), Is.True);
        Assert.That(store.Query<Note>().Count, Is.EqualTo(1));
    }

    [Test]
    public void UndoRestoresMovedNotePosition()
    {
        var store = new EntityStore();
        Entity note = store.CreateEntity(new Note(500, new Vector2(10, 20)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        ref Note component = ref note.GetComponent<Note>();
        component.Position = new Vector2(200, 300);
        EditorSnapshot after = history.Capture();
        history.Record(before, after);

        history.Undo();
        Assert.That(store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Position,
            Is.EqualTo(new Vector2(10, 20)));

        history.Redo();
        Assert.That(store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Position,
            Is.EqualTo(new Vector2(200, 300)));
    }

    [Test]
    public void UndoRestoresDeletedNote()
    {
        var store = new EntityStore();
        Entity note = store.CreateEntity(new Note(500, new Vector2(10, 20)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        note.DeleteEntity();
        EditorSnapshot after = history.Capture();
        history.Record(before, after);

        history.Undo();
        Assert.That(store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(store.Query<Note>().ToEntityList()[0].GetComponent<Note>().TimeMs,
            Is.EqualTo(500));
    }

    [Test]
    public void UndoRestoresDeletedStartPoint()
    {
        var store = new EntityStore();
        Entity start = store.CreateEntity(new StartPoint(
            "Start", 500, new Vector2(10, 20),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        start.DeleteEntity();
        history.Record(before, history.Capture());

        Assert.That(history.Undo(), Is.True);
        Assert.That(store.Query<StartPoint>().Count, Is.EqualTo(1));
        Assert.That(
            store.Query<StartPoint>().ToEntityList()[0]
                .GetComponent<StartPoint>().Name,
            Is.EqualTo("Start"));
    }
}
