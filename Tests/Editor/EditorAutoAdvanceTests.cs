using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorAutoAdvanceTests
{
    private EntityStore _store = null!;
    private FakePointerInput _pointer = null!;
    private SystemRoot _root = null!;
    private Entity _editor;
    private Entity _toggle;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _pointer = new FakePointerInput();
        _root = new SystemRoot(_store);
        _root.Add(new EditorNoteInputSystem(_store, _pointer, new EditorHistory(_store)));
        _editor = _store.CreateEntity(
            new LevelInfo("Test", TestSongs.At("/tmp/test.wav")),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default));
        _toggle = EditorAutoAdvance.Attach(_store.CreateEntity());
    }

    private void Click(Vector2 point)
    {
        _pointer.Current = new PointerInput(point, true);
        _root.Update(new UpdateTick(1f / 60f, 0));
        _pointer.Current = new PointerInput(point, false);
        _root.Update(new UpdateTick(1f / 60f, 0));
    }

    private long StepMs => (long)EditorTiming.StepMs(_editor.GetComponent<Playhead>());

    [Test]
    public void ItStartsOffAndLeavesThePlayheadWhereItWas()
    {
        Assert.That(EditorAutoAdvance.IsOn(_store), Is.False);

        Click(new Vector2(300, 400));

        Assert.That(_store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(_editor.GetComponent<Playhead>().TimeMs, Is.EqualTo(1_000));
    }

    [Test]
    public void PlacingANoteMovesThePlayheadOnOneSnap()
    {
        _toggle.GetComponent<Toggle>().Value = true;

        Click(new Vector2(300, 400));
        Click(new Vector2(500, 400));

        long[] times = new long[2];
        int i = 0;
        foreach (Entity note in _store.Query<Note>().ToEntityList())
            times[i++] = note.GetComponent<Note>().TimeMs;
        System.Array.Sort(times);
        Assert.That(times, Is.EqualTo(new[] { 1_000, 1_000 + StepMs }));
        Assert.That(_editor.GetComponent<Playhead>().TimeMs, Is.EqualTo(1_000 + StepMs * 2));
        Assert.That(_editor.GetComponent<Playhead>().SeekRequestMs, Is.EqualTo(1_000 + StepMs * 2));
    }

    [Test]
    public void WithSnapOnAPlacedNoteLandsOnTheNearestGridCrossing()
    {
        Entity snap = ViewportSnap.Attach(_store.CreateEntity(), "snap");
        snap.GetComponent<Toggle>().Value = true;
        Vector2 cell = Xanadu.Systems.Playfields.PlayfieldGrid.Cell(_editor.GetComponent<Playfield>());

        Click(new Vector2(cell.X * 3 + cell.X * 0.3f, cell.Y * 5 + cell.Y * 0.7f));

        Note note = _store.Query<Note>().ToEntityList()[0].GetComponent<Note>();
        Assert.That(note.Position, Is.EqualTo(new Vector2(cell.X * 3, cell.Y * 6)));
    }

    [Test]
    public void WithSnapOffANoteLandsWhereItWasClicked()
    {
        Entity snap = ViewportSnap.Attach(_store.CreateEntity(), "snap");
        snap.GetComponent<Toggle>().Value = false;

        Click(new Vector2(313, 427));

        Assert.That(_store.Query<Note>().ToEntityList()[0].GetComponent<Note>().Position, Is.EqualTo(new Vector2(313, 427)));
    }

    [Test]
    public void TheNextSnapStaysInsideTheSong()
    {
        // 1/1: the next line is a whole beat (500 ms) on, past the song's end.
        var playhead = new Playhead(1_000, 120, 0, false, snapDivision: 1) { DurationMs = 1_200 };

        Assert.That(EditorAutoAdvance.Next(1_000, playhead), Is.EqualTo(1_200));
    }

    [Test]
    public void AnOffBeatPlacementLandsOnTheNextGridLine()
    {
        var playhead = new Playhead(0, 120, 0, false);
        long step = (long)EditorTiming.StepMs(playhead);

        Assert.That(EditorAutoAdvance.Next(step, playhead), Is.EqualTo(step * 2));
    }
}
