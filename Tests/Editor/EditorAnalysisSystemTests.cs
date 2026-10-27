using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorAnalysisSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private Entity _editor;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _editor = _store.CreateEntity(
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1920, 1080)),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false, durationMs: 10_000),
            EditorAnalysis.Create());
        _root = new SystemRoot(_store);
        _root.Add(new EditorAnalysisSystem(_store));
    }

    [Test]
    public void FlagsAnUnreachableNoteAndBuildsTheDensityGraph()
    {
        AddNote(1000, 500, 900);
        AddNote(2000, 100, 900);

        Frame();

        EditorAnalysis analysis = _editor.GetComponent<EditorAnalysis>();
        Assert.That(analysis.Issues, Has.Length.EqualTo(1));
        Assert.That(analysis.Issues[0].TimeMs, Is.EqualTo(2000));
        Assert.That(analysis.Density.PeakNotes, Is.GreaterThan(0));
    }

    [Test]
    public void DoesNotRecomputeWhileTheChartIsUnchanged()
    {
        AddNote(1000, 500, 900);
        Frame();
        EditorAnalysis first = _editor.GetComponent<EditorAnalysis>();

        Frame();
        Frame();

        EditorAnalysis later = _editor.GetComponent<EditorAnalysis>();
        Assert.That(later.Density, Is.SameAs(first.Density));
        Assert.That(later.Issues, Is.SameAs(first.Issues));
    }

    [Test]
    public void RecomputesWhenANoteIsAddedMovedOrDeleted()
    {
        Entity note = AddNote(1000, 500, 900);
        Frame();
        Assert.That(_editor.GetComponent<EditorAnalysis>().Issues, Is.Empty);

        note.GetComponent<Note>().Position = new Vector2(100, 900);
        Frame();
        Assert.That(_editor.GetComponent<EditorAnalysis>().Issues, Has.Length.EqualTo(1));

        note.DeleteEntity();
        Frame();
        Assert.That(_editor.GetComponent<EditorAnalysis>().Issues, Is.Empty);
    }

    private Entity AddNote(long time, float x, float y) => _store.CreateEntity(
        new Note(time, new Vector2(x, y), Xanadu.Settings.KeybindAction.M, NoteKind.Hit, DashDirection.Up, 0, CharacterKind.Contact));

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));
}
