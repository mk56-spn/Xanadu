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
            target: KeybindAction.R3,
            kind: NoteKind.Movement,
            dashDirection: DashDirection.DownLeft));
        sourceStore.CreateEntity(new Mine(900, new Vector2(450, 550)));
        sourceStore.CreateEntity(new StartPoint(
            "Chorus",
            750,
            new Vector2(300, 400),
            new System.Numerics.Vector2(2, -3),
            new Vector2(1, 0)));

        LevelDocument document = LevelDocumentMapper.Capture(sourceStore, source);

        Assert.That(document.Name, Is.EqualTo("Mapped"));
        Assert.That(document.SnapDivision, Is.EqualTo(16));
        Assert.That(document.Notes, Has.Count.EqualTo(2));
        Assert.That(document.Notes[0].Target, Is.EqualTo(KeybindAction.L1));
        Assert.That(document.Notes[1].Target, Is.EqualTo(KeybindAction.R3));
        Assert.That(document.Notes[1].Kind, Is.EqualTo(NoteKind.Movement));
        Assert.That(
            document.Notes[1].DashDirection,
            Is.EqualTo(DashDirection.DownLeft));
        Assert.That(document.Mines, Has.Count.EqualTo(1));
        Assert.That(document.Mines[0].TimeMs, Is.EqualTo(900));
        Assert.That(document.Mines[0].X, Is.EqualTo(450));
        Assert.That(document.StartPoints, Has.Count.EqualTo(1));
        Assert.That(document.StartPoints[0].Name, Is.EqualTo("Chorus"));
        Assert.That(document.StartPoints[0].MomentumX, Is.EqualTo(2));

        var targetStore = new EntityStore();
        Entity target = targetStore.CreateEntity(
            new LevelInfo("Old", ""),
            new Playfield(1, 1),
            new Playhead(0, 1, 0, false),
            new NoteSelection(default));
        targetStore.CreateEntity(new Note(9_999, Vector2.Zero));
        targetStore.CreateEntity(new Mine(9_999, Vector2.Zero));

        LevelDocumentMapper.Apply(targetStore, target, document);

        Assert.That(target.GetComponent<LevelInfo>().Name, Is.EqualTo("Mapped"));
        Assert.That(target.GetComponent<LevelInfo>().Artist, Is.Empty);
        Assert.That(target.GetComponent<LevelInfo>().Difficulty, Is.EqualTo("Mapped"));
        Assert.That(target.GetComponent<Playfield>().Width, Is.EqualTo(1000));
        Assert.That(target.GetComponent<Playhead>().Bpm, Is.EqualTo(120));
        Assert.That(target.GetComponent<Playhead>().SnapDivision, Is.EqualTo(16));
        Assert.That(target.GetComponent<Playhead>().AudioRevision, Is.EqualTo(1));
        Assert.That(targetStore.Query<Note>().Count, Is.EqualTo(2));
        Assert.That(
            targetStore.Query<Note>().ToEntityList()[0].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.L1));
        Assert.That(
            targetStore.Query<Note>().ToEntityList()[1].GetComponent<Note>().Target,
            Is.EqualTo(KeybindAction.R3));
        Assert.That(targetStore.Query<Mine>().Count, Is.EqualTo(1));
        Assert.That(targetStore.Query<StartPoint>().Count, Is.EqualTo(1));
        Assert.That(
            targetStore.Query<StartPoint>().ToEntityList()[0]
                .GetComponent<StartPoint>().Momentum.Y,
            Is.EqualTo(-3));
        Assert.That(
            targetStore.Query<Mine>().ToEntityList()[0].GetComponent<Mine>().TimeMs,
            Is.EqualTo(900));
        Assert.That(
            targetStore.Query<Note>().ToEntityList()[1].GetComponent<Note>().Kind,
            Is.EqualTo(NoteKind.Movement));

        foreach (Entity entity in targetStore.Query<Note>().ToEntityList())
            Assert.That(entity.Tags.Has<EditorItem>(), Is.True);
        foreach (Entity entity in targetStore.Query<Mine>().ToEntityList())
            Assert.That(entity.Tags.Has<EditorItem>(), Is.True);
        foreach (Entity entity in targetStore.Query<StartPoint>().ToEntityList())
            Assert.That(entity.Tags.Has<EditorItem>(), Is.True);
    }

    [Test]
    public void EditorItemsFactoryTagsEveryItemType()
    {
        var store = new EntityStore();
        Entity note = EditorItems.Create(store, new Note(
            0, new Vector2(10, 20)));
        Entity mine = EditorItems.Create(store, new Mine(
            100, new Vector2(30, 40)));
        Entity start = EditorItems.Create(store, new StartPoint(
            "Start", 200, new Vector2(50, 60),
            System.Numerics.Vector2.Zero, new Vector2(0, -1)));

        Assert.That(note.Tags.Has<EditorItem>(), Is.True);
        Assert.That(mine.Tags.Has<EditorItem>(), Is.True);
        Assert.That(start.Tags.Has<EditorItem>(), Is.True);
    }
}
