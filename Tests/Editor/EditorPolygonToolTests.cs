using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Items;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

/// <summary>The polygon tool lays an outline click by click and closes it into a shape the XIF editor can edit.</summary>
[TestFixture]
public sealed class EditorPolygonToolTests
{
    private EntityStore _store = null!;
    private FakePointerInput _pointer = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;
    private Entity _editor;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, _history));
        _editor = _store.CreateEntity(
            new LevelInfo("Test", TestSongs.At("/tmp/test.wav")),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default),
            new EditorPlacement { Mode = EditorPlacementMode.Polygon },
            new EditorPolygonDraft());

        // The editor starts with snapping off, so points land where they are clicked.
        Entity snap = _store.CreateEntity(new Toggle(ViewportSnap.Label, ViewportSnap.Label, false));
        snap.AddTag<ViewportSnapToggle>();
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Click(Vector2 point)
    {
        _pointer.Current = new PointerInput(point, true);
        Frame();
        _pointer.Current = new PointerInput(point, false);
        Frame();
    }

    private EditorPolygonDraft Draft => _editor.GetComponent<EditorPolygonDraft>();

    private void LayTriangle()
    {
        Click(new Vector2(200, 200));
        Click(new Vector2(400, 200));
        Click(new Vector2(300, 380));
    }

    [Test]
    public void ClicksLayPointsAndTheFirstPointClosesTheShape()
    {
        LayTriangle();
        Assert.That(Draft.Points, Has.Count.EqualTo(3));
        Assert.That(_store.Query<LevelPolygon>().Count, Is.Zero);

        Click(new Vector2(204, 203));

        Entity shape = _store.Query<LevelPolygon>().ToEntityList().Single();
        LevelPolygon polygon = shape.GetComponent<LevelPolygon>();
        Assert.That(Draft.Active, Is.False);
        Assert.That(polygon.Position, Is.EqualTo(new Vector2(300, 290)), "the origin is the middle of the outline");
        Assert.That(polygon.Outline(polygon.Item.Primitives[0]),
            Is.EqualTo(new[] { new Vector2(200, 200), new Vector2(400, 200), new Vector2(300, 380) }));
        Assert.That(_editor.GetComponent<NoteSelection>().Note, Is.EqualTo(shape));

        Assert.That(_history.Undo(), Is.True);
        Assert.That(_store.Query<LevelPolygon>().Count, Is.Zero);
    }

    [Test]
    public void EnterClosesAndEscDrops()
    {
        LayTriangle();
        _editor.GetComponent<EditorPolygonDraft>().CancelRequested = true;
        Frame();
        Assert.That(Draft.Active, Is.False);
        Assert.That(_store.Query<LevelPolygon>().Count, Is.Zero);

        LayTriangle();
        _editor.GetComponent<EditorPolygonDraft>().FinishRequested = true;
        Frame();
        Assert.That(_store.Query<LevelPolygon>().Count, Is.EqualTo(1));
    }

    [Test]
    public void TwoPointsAreNotAShape()
    {
        Click(new Vector2(200, 200));
        Click(new Vector2(400, 200));
        _editor.GetComponent<EditorPolygonDraft>().FinishRequested = true;
        Frame();

        Assert.That(_store.Query<LevelPolygon>().Count, Is.Zero);
        Assert.That(Draft.Active, Is.False);
    }

    [Test]
    public void PuttingTheToolAwayDropsTheOutline()
    {
        LayTriangle();
        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Notes;
        Frame();

        Assert.That(Draft.Active, Is.False);
    }

    [Test]
    public void AShapeIsPickedInsideItsOutlineOnly()
    {
        LayTriangle();
        Click(new Vector2(200, 200));
        _editor.GetComponent<NoteSelection>().Clear();
        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Text;

        Click(new Vector2(300, 260));
        Assert.That(_editor.GetComponent<NoteSelection>().Note.HasComponent<LevelPolygon>(), Is.True);
    }

    [Test]
    public void TheXifHostEditsTheSelectedShapeAsOneUndoStep()
    {
        LayTriangle();
        Click(new Vector2(200, 200));
        var host = new EditorXifHost(_store) { History = _history };
        Xif item = host.Item!;

        host.Edit(() => XifEditing.InsertPoint(item.Primitives[0], 0));
        Assert.That(_store.Query<LevelPolygon>().ToEntityList()[0].GetComponent<LevelPolygon>().Item.Primitives[0].Points,
            Has.Count.EqualTo(4));

        Assert.That(_history.Undo(), Is.True);
        Assert.That(_store.Query<LevelPolygon>().ToEntityList()[0].GetComponent<LevelPolygon>().Item.Primitives[0].Points,
            Has.Count.EqualTo(3));
    }
}
