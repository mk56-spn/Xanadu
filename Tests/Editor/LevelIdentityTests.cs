using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class LevelIdentityTests
{
    private EntityStore _store = null!;
    private Entity _editor;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _editor = _store.CreateEntity(
            new LevelInfo("Untitled", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            LevelGroups.Create());
    }

    [Test]
    public void ReSavingALevelKeepsItsIdAndSlug()
    {
        var original = new LevelDocument { Id = "abc123", Slug = "my-level", Name = "Mine" };
        original.Notes.Add(new NoteDocument { TimeMs = 500, X = 10, Y = 20 });

        LevelDocumentMapper.Apply(_store, _editor, original);
        LevelDocument saved = LevelDocumentMapper.Capture(_store, _editor);

        Assert.That(saved.Id, Is.EqualTo("abc123"));
        Assert.That(saved.Slug, Is.EqualTo("my-level"));
        Assert.That(saved.Notes, Has.Count.EqualTo(1));
    }

    [Test]
    public void CapturingTwiceGivesTheSameIdentity()
    {
        LevelDocumentMapper.Apply(_store, _editor, new LevelDocument { Id = "stable" });

        Assert.That(
            LevelDocumentMapper.Capture(_store, _editor).Id,
            Is.EqualTo(LevelDocumentMapper.Capture(_store, _editor).Id));
    }

    [Test]
    public void ANewLevelStillGetsAnIdentity()
    {
        LevelDocument fresh = LevelDocumentMapper.Capture(_store, _editor);

        Assert.That(fresh.Id, Is.Not.Empty);
    }
}
