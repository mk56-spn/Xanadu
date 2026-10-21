using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Levels;
using Writer.Settings;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class LevelDocumentMapperTests
{
    [Test]
    public void CapturesAndAppliesEditorState()
    {
        var sourceStore = new EntityStore();
        Entity source = sourceStore.CreateEntity(
            new LevelInfo("Mapped", "res://song.mp3", "Artist", "Insane"),
            new Playfield(1000, 1000),
            new Playhead(0, 150, 25, false, snapDivision: 16),
            new NoteSelection(default));
        sourceStore.CreateEntity(new Note(
            500,
            new Vector2(100, 200),
            target: KeybindAction.L1));
        sourceStore.CreateEntity(new Note(
            1_000,
            new Vector2(300, 400),
            target: KeybindAction.R3));

        LevelDocument document = LevelDocumentMapper.Capture(sourceStore, source);

        Assert.That(document.Name, Is.EqualTo("Mapped"));
        Assert.That(document.Artist, Is.EqualTo("Artist"));
        Assert.That(document.Difficulty, Is.EqualTo("Insane"));
        Assert.That(document.Bpm, Is.EqualTo(150));
        Assert.That(document.OffsetMs, Is.EqualTo(25));
        Assert.That(document.SnapDivision, Is.EqualTo(16));
        Assert.That(document.Notes, Has.Count.EqualTo(2));
        Assert.That(document.Notes[0].Target, Is.EqualTo(KeybindAction.L1));
        Assert.That(document.Notes[1].Target, Is.EqualTo(KeybindAction.R3));

        var targetStore = new EntityStore();
        Entity target = targetStore.CreateEntity(
            new LevelInfo("Old", ""),
            new Playfield(1, 1),
            new Playhead(0, 1, 0, false),
            new NoteSelection(default));
        targetStore.CreateEntity(new Note(9_999, Vector2.Zero));

        LevelDocumentMapper.Apply(targetStore, target, document);

        Assert.That(target.GetComponent<LevelInfo>().Name, Is.EqualTo("Mapped"));
        Assert.That(target.GetComponent<LevelInfo>().Artist, Is.EqualTo("Artist"));
        Assert.That(target.GetComponent<LevelInfo>().Difficulty, Is.EqualTo("Insane"));
        Assert.That(target.GetComponent<Playfield>().Width, Is.EqualTo(1000));
        Assert.That(target.GetComponent<Playhead>().Bpm, Is.EqualTo(150));
        Assert.That(target.GetComponent<Playhead>().SnapDivision, Is.EqualTo(16));
        Assert.That(target.GetComponent<Playhead>().AudioRevision, Is.EqualTo(1));
        Assert.That(targetStore.Query<Note>().Count, Is.EqualTo(2));
        Assert.That(
            targetStore.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.L1));
        Assert.That(
            targetStore.Query<Note>().ToEntityList()[1].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
    }
}
