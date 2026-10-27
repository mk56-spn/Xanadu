using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;

namespace Xanadu.Tests.Groups;

[TestFixture]
public sealed class GroupPersistenceTests
{
    private static Entity Editor(EntityStore store) => store.CreateEntity(
        new LevelInfo("Groups", "res://song.mp3"),
        new Playfield(1000, 1000),
        new Playhead(0, 120, 0, false),
        LevelGroups.Create());

    private static void Define(Entity editor)
    {
        GroupDefinition[] groups = editor.GetComponent<LevelGroups>().Groups;
        groups[2].Name = "Wobble";
        groups[2].Effects.Add(GroupEffect.Move(0.4f, 0.6f, new Vector2(30, -10), GroupEase.Out));
        groups[2].Effects.Add(GroupEffect.Colour(0.8f, 1.2f, new Color(1f, 0.5f, 0.25f, 0.5f), GroupEase.In));
        groups[9].Effects.Add(GroupEffect.Move(1f, 1f, new Vector2(1, 2)));
        groups[9].Effects[0] = groups[9].Effects[0] with { Muted = true };
    }

    [Test]
    public void GroupsAndMembershipSurviveCaptureAndApply()
    {
        var source = new EntityStore();
        Entity editor = Editor(source);
        Define(editor);
        Entity note = source.CreateEntity(new Note(500, new Vector2(10, 20)));
        note.AddComponent(new ItemGroups(ItemGroups.FromList(new[] { 2, 9 })));
        Entity mine = source.CreateEntity(new Mine(900, new Vector2(30, 40)));
        mine.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        Entity block = source.CreateEntity(new Block(new Vector2(100, 100), new Vector2(50, 50)));
        block.AddComponent(new ItemGroups(ItemGroups.Bit(9)));
        Entity start = source.CreateEntity(new StartPoint(
            "Intro",
            1_000,
            new Vector2(40, 50),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)));
        start.AddComponent(new ItemGroups(ItemGroups.Bit(2)));

        LevelDocument document = LevelDocumentMapper.Capture(source, editor);

        var target = new EntityStore();
        Entity loaded = Editor(target);
        LevelDocumentMapper.Apply(target, loaded, document);

        GroupDefinition[] groups = loaded.GetComponent<LevelGroups>().Groups;
        Assert.That(groups[2].Name, Is.EqualTo("Wobble"));
        Assert.That(groups[2].Effects, Has.Count.EqualTo(2));
        Assert.That(groups[2].Effects[0], Is.EqualTo(GroupEffect.Move(0.4f, 0.6f, new Vector2(30, -10), GroupEase.Out)));
        Assert.That(groups[2].Effects[1].Kind, Is.EqualTo(GroupEffectKind.Color));
        Assert.That(groups[2].Effects[1].Tint.R, Is.EqualTo(1f).Within(0.01f));
        Assert.That(groups[2].Effects[1].Tint.G, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(groups[2].Effects[1].Tint.A, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(groups[2].Effects[1].Ease, Is.EqualTo(GroupEase.In));
        Assert.That(groups[9].Effects[0].Start, Is.EqualTo(1f));
        Assert.That(groups[9].Effects[0].Muted, Is.True);

        Entity noteBack = target.Query<Note>().ToEntityList()[0];
        Assert.That(ItemGroups.ToList(noteBack.GetComponent<ItemGroups>().Mask), Is.EqualTo(new[] { 2, 9 }));
        Entity mineBack = target.Query<Mine>().ToEntityList()[0];
        Assert.That(ItemGroups.ToList(mineBack.GetComponent<ItemGroups>().Mask), Is.EqualTo(new[] { 2 }));
        Entity blockBack = target.Query<Block>().ToEntityList()[0];
        Assert.That(ItemGroups.ToList(blockBack.GetComponent<ItemGroups>().Mask), Is.EqualTo(new[] { 9 }));
        Entity startBack = target.Query<StartPoint>().ToEntityList()[0];
        Assert.That(ItemGroups.ToList(startBack.GetComponent<ItemGroups>().Mask), Is.EqualTo(new[] { 2 }));
        Assert.That(startBack.GetComponent<StartPoint>().Groups, Is.EqualTo(ItemGroups.Bit(2)));
    }

    [Test]
    public void OnlyGroupsWithContentAreSaved()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        Define(editor);

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);

        Assert.That(document.Groups.ConvertAll(group => group.Id), Is.EqualTo(new[] { 2, 9 }));
    }

    [Test]
    public void GroupsSurviveAMapFile()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        Define(editor);
        Entity note = store.CreateEntity(new Note(500, new Vector2(10, 20)));
        note.AddComponent(new ItemGroups(ItemGroups.Bit(2)));

        byte[] map = LevelSerializer.Serialize(LevelDocumentMapper.Capture(store, editor));
        LevelDocument back = LevelSerializer.Deserialize(map);

        Assert.That(back.Groups, Has.Count.EqualTo(2));
        Assert.That(back.Groups[0].Effects[0].End, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(back.Groups[0].Effects[0].Ease, Is.EqualTo(GroupEase.Out));
        Assert.That(back.Groups[1].Effects[0].Muted, Is.True);
        Assert.That(back.Notes[0].Groups, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void AnUnknownEaseFallsBackToLinear()
    {
        var level = new LevelDocument();
        level.Groups.Add(new GroupDocument
        {
            Id = 0,
            Effects =
            {
                new GroupEffectDocument { Ease = (GroupEase)99 },
            },
        });

        LevelDocument document = LevelSerializer.Normalize(level);

        Assert.That(document.Groups[0].Effects[0].Ease, Is.EqualTo(GroupEase.Linear));
    }

    [Test]
    public void AnOldLevelWithoutGroupsStillLoads()
    {
        var document = new LevelDocument();
        document.Notes.Add(new NoteDocument { TimeMs = 100, X = 1, Y = 2 });
        document.Mines.Add(new MineDocument { TimeMs = 5 });
        document.Blocks.Add(new BlockDocument { Width = 4, Height = 4 });

        document = LevelSerializer.Normalize(document);
        var store = new EntityStore();
        Entity editor = Editor(store);
        LevelDocumentMapper.Apply(store, editor, document);

        Assert.That(document.Groups, Is.Empty);
        Assert.That(store.Query<Note>().Count, Is.EqualTo(1));
        Assert.That(store.Query<Note>().ToEntityList()[0].HasComponent<ItemGroups>(), Is.False);
        Assert.That(editor.GetComponent<LevelGroups>().Groups, Has.Length.EqualTo(GroupLimits.Count));
    }

    [Test]
    public void LoadingALevelWithoutGroupsOnTheEntityAddsThem()
    {
        var store = new EntityStore();
        Entity bare = store.CreateEntity(
            new LevelInfo("x", ""), new Playfield(1000, 1000), new Playhead(0, 120, 0, false));

        LevelDocumentMapper.Apply(store, bare, new LevelDocument());

        Assert.That(bare.HasComponent<LevelGroups>(), Is.True);
    }

    [Test]
    public void BadGroupDataIsDroppedOnLoad()
    {
        var level = new LevelDocument();
        level.Groups.Add(new GroupDocument { Id = 99, Name = "far" });
        level.Groups.Add(new GroupDocument { Id = 1, Name = "ok" });
        level.Groups.Add(null!);

        LevelDocument document = LevelSerializer.Normalize(level);

        Assert.That(document.Groups.ConvertAll(group => group.Id), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void UndoAndRedoCoverGroupEditsAndMembership()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        Entity note = store.CreateEntity(new Note(500, new Vector2(10, 20)));
        Entity start = store.CreateEntity(new StartPoint(
            "Intro",
            500,
            new Vector2(40, 50),
            System.Numerics.Vector2.Zero,
            new Vector2(0, -1)));
        var history = new EditorHistory(store);

        EditorSnapshot before = history.Capture();
        editor.GetComponent<LevelGroups>().Groups[1].Effects.Add(GroupEffect.Move(0f, 1f, new Vector2(5, 5)));
        note.AddComponent(new ItemGroups(ItemGroups.Bit(1)));
        start.AddComponent(new ItemGroups(ItemGroups.Bit(1)));
        EditorSnapshot after = history.Capture();
        history.Record(before, after);

        Assert.That(history.Undo(), Is.True);
        Assert.That(editor.GetComponent<LevelGroups>().Groups[1].Effects, Is.Empty);
        Assert.That(store.Query<Note>().ToEntityList()[0].HasComponent<ItemGroups>(), Is.False);
        Assert.That(store.Query<StartPoint>().ToEntityList()[0].HasComponent<ItemGroups>(), Is.False);

        Assert.That(history.Redo(), Is.True);
        Assert.That(editor.GetComponent<LevelGroups>().Groups[1].Effects, Has.Count.EqualTo(1));
        Assert.That(store.Query<Note>().ToEntityList()[0].GetComponent<ItemGroups>().Has(1), Is.True);
        Assert.That(store.Query<StartPoint>().ToEntityList()[0].GetComponent<ItemGroups>().Has(1), Is.True);
    }

    [Test]
    public void ChangingOnlyGroupsCountsAsAnEdit()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        var history = new EditorHistory(store);

        EditorSnapshot before = history.Capture();
        editor.GetComponent<LevelGroups>().Groups[0].Name = "Renamed";
        history.Record(before, history.Capture());

        Assert.That(history.CanUndo, Is.True);
    }

    [Test]
    public void ValidationFlagsAnEffectThatEndsBeforeItStarts()
    {
        var document = new LevelDocument { Name = "n", SongId = "s" };
        document.Notes.Add(new NoteDocument());
        document.Groups.Add(new GroupDocument
        {
            Id = 3,
            Name = "Bad",
            Effects = { new GroupEffectDocument { Start = 1f, End = 0.5f }, new GroupEffectDocument { Start = -1f, End = 0f } },
        });

        var issues = LevelValidator.Validate(document);

        Assert.That(issues, Has.Some.Contains("Bad effect 1 ends before it starts"));
        Assert.That(issues, Has.Some.Contains("Bad effect 2 starts before 0"));
    }
}
