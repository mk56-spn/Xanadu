using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Storage;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Groups;

/// <summary>Step 10 of docs/render-architecture.md: buffered groups and their rules (D12–D16a).</summary>
[TestFixture]
public sealed class GroupBufferTests
{
    private static GroupDefinition[] Slots()
    {
        var groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        return groups;
    }

    private static Entity Member(EntityStore store, int layer, params int[] groups)
    {
        Entity entity = store.CreateEntity(new ItemLayer(layer, 0), Tags.Get<EditorItem>());
        entity.AddComponent(new ItemGroups(ItemGroups.FromList(groups)));
        return entity;
    }

    [Test]
    public void MembersOnOneLayerInOneGroupMayBeBuffered()
    {
        var store = new EntityStore();
        Member(store, -2, 3);
        Member(store, -2, 3);
        GroupDefinition[] groups = Slots();

        Assert.That(GroupBufferRules.CanBuffer(store, groups, 3), Is.True);
        Assert.That(GroupBufferRules.Problem(store, groups, 3), Is.Null);
    }

    [Test]
    public void MembersOnTwoLayersAreRefused()
    {
        var store = new EntityStore();
        Member(store, -2, 0);
        Member(store, 1, 0);
        GroupDefinition[] groups = Slots();

        Assert.That(GroupBufferRules.Problem(store, groups, 0), Is.EqualTo(GroupBufferRules.OneLayer));
    }

    [Test]
    public void AMemberInAnotherGroupIsRefused()
    {
        var store = new EntityStore();
        Member(store, -2, 0);
        Member(store, -2, 0, 4);
        GroupDefinition[] groups = Slots();

        Assert.That(GroupBufferRules.Problem(store, groups, 0), Is.EqualTo(GroupBufferRules.OneGroup));
    }

    [Test]
    public void AnEditThatBreaksABufferedGroupIsReported()
    {
        var store = new EntityStore();
        Entity first = Member(store, -2, 1);
        Member(store, -2, 1);
        GroupDefinition[] groups = Slots();
        groups[1].Buffered = true;
        Assert.That(GroupBufferRules.Broken(store, groups), Is.Null);

        ItemLayer.Put(first, new ItemLayer(1, 0));
        Assert.That(GroupBufferRules.Broken(store, groups), Is.EqualTo(GroupBufferRules.OneLayer));
    }

    [Test]
    public void MovingOneBufferedMemberToAnotherLayerIsRefused()
    {
        var store = new EntityStore();
        LevelGroups level = LevelGroups.Create();
        store.CreateEntity(level);
        level.Groups[2].Buffered = true;
        Entity first = Member(store, -2, 2);
        Entity second = Member(store, -2, 2);

        Assert.That(
            EditorBufferGuard.MovingToLayer(store, new[] { first }, 1),
            Is.EqualTo(GroupBufferRules.OneLayer));
        Assert.That(
            EditorBufferGuard.MovingToLayer(store, new[] { first, second }, 1),
            Is.Null,
            "the whole group moving together stays on one layer");
    }

    [Test]
    public void AddingAMemberOnAnotherLayerIsRefused()
    {
        var store = new EntityStore();
        LevelGroups level = LevelGroups.Create();
        store.CreateEntity(level);
        level.Groups[1].Buffered = true;
        Member(store, -2, 1);
        Entity elsewhere = store.CreateEntity(new ItemLayer(1, 0), Tags.Get<EditorItem>());

        Assert.That(
            EditorBufferGuard.AddingToGroup(store, new[] { elsewhere }, 1),
            Is.EqualTo(GroupBufferRules.OneLayer));
    }

    [Test]
    public void AddingAMemberOnTheGroupsLayerIsAllowed()
    {
        var store = new EntityStore();
        LevelGroups level = LevelGroups.Create();
        store.CreateEntity(level);
        level.Groups[1].Buffered = true;
        Member(store, -2, 1);
        Entity beside = store.CreateEntity(new ItemLayer(-2, 0), Tags.Get<EditorItem>());

        Assert.That(EditorBufferGuard.AddingToGroup(store, new[] { beside }, 1), Is.Null);
    }

    [Test]
    public void GivingABufferedMemberASecondGroupIsRefused()
    {
        var store = new EntityStore();
        LevelGroups level = LevelGroups.Create();
        store.CreateEntity(level);
        level.Groups[1].Buffered = true;
        Entity member = Member(store, -2, 1);

        Assert.That(
            EditorBufferGuard.AddingToGroup(store, new[] { member }, 4),
            Is.EqualTo(GroupBufferRules.OneGroup));
    }

    [Test]
    public void PastingABufferedMemberKeepsTheGroupValid()
    {
        var store = new EntityStore();
        Entity item = Member(store, -2, 0);
        GroupDefinition[] groups = Slots();
        groups[0].Buffered = true;

        // A paste keeps the member's layer and group mask.
        store.CreateEntity(
            new ItemLayer(ItemLayer.Of(item).Layer, 0),
            new ItemGroups(ItemGroups.Bit(0)),
            Tags.Get<EditorItem>());

        Assert.That(GroupBufferRules.Broken(store, groups), Is.Null);
    }

    [Test]
    public void AnEmptyBufferedGroupBreaksNothing()
    {
        var store = new EntityStore();
        GroupDefinition[] groups = Slots();
        groups[2].Buffered = true;

        Assert.That(GroupBufferRules.Broken(store, groups), Is.Null);
        Assert.That(groups[2].IsEmpty, Is.False, "a buffered group is worth keeping");
    }

    [Test]
    public void TheBufferSettingsRoundTripThroughAMap()
    {
        var source = new LevelDocument { Name = "Buffered" };
        source.Groups.Add(new GroupDocument
        {
            Id = 2,
            Name = "Puff",
            Buffered = true,
            BufferTint = "#3366cc80",
            BufferAlpha = 0.75f,
            BufferShader = 3,
            BufferBlend = BlendMode.Add,
            BufferKeys =
            {
                new ShaderKeyDocument
                {
                    Param = "blur",
                    Time = 1f,
                    Value = new[] { 2.5f },
                    Ease = GroupEase.Out,
                },
            },
        });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        GroupDocument group = read.Groups.Single();
        Assert.That(group.Buffered, Is.True);
        Assert.That(group.BufferTint, Is.EqualTo("#3366cc80"));
        Assert.That(group.BufferAlpha, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(group.BufferShader, Is.EqualTo(3));
        Assert.That(group.BufferBlend, Is.EqualTo(BlendMode.Add));
        Assert.That(group.BufferKeys.Single().Param, Is.EqualTo("blur"));
        Assert.That(group.BufferKeys.Single().Value, Is.EqualTo(new[] { 2.5f }));
    }

    [Test]
    public void ALevelWithNoBufferedGroupHasNoBufferChunk()
    {
        var source = new LevelDocument { Name = "Plain" };
        source.Groups.Add(new GroupDocument { Id = 0, Name = "A" });
        byte[] data = XmfBinary.Write(source);
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet(22, out _), Is.False);
    }

    [Test]
    public void BuffersRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var easy = new XmfVariant { Id = "easy", Name = "Easy" };
        easy.Groups.Add(new GroupDocument { Id = 0, Name = "Plain" });
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Groups.Add(new GroupDocument { Id = 0, Name = "Puff", Buffered = true, BufferAlpha = 0.5f });
        mapset.Variants.Add(easy);
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Groups[0].Buffered, Is.False);
        Assert.That(read.Variants.Single(v => v.Id == "hard").Groups[0].Buffered, Is.True);
        Assert.That(read.Variants.Single(v => v.Id == "hard").Groups[0].BufferAlpha, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void TheMapperCarriesTheBufferBothWays()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new LevelInfo("Buffered", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default),
            LevelGroups.Create());
        LevelGroups groups = editor.GetComponent<LevelGroups>();
        groups.Groups[1].Buffered = true;
        groups.Groups[1].BufferTint = new Color("#3366cc80");
        groups.Groups[1].BufferAlpha = 0.4f;
        groups.Groups[1].BufferShader = 2;
        groups.Groups[1].BufferBlend = BlendMode.Mul;
        groups.Groups[1].BufferKeys.Add(new ShaderKey("size", 2f, new Vector4(3f, 0, 0, 0), 1, GroupEase.In));

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);
        Assert.That(document.Groups.Single().Buffered, Is.True);
        Assert.That(document.Groups.Single().BufferBlend, Is.EqualTo(BlendMode.Mul));

        var target = new EntityStore();
        LevelDocumentMapper.Apply(target, target.CreateEntity(
            new LevelInfo("", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default)), document);

        LevelGroups applied = target.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        GroupDefinition group = applied.Groups[1];
        Assert.That(group.Buffered, Is.True);
        Assert.That(group.BufferTint, Is.EqualTo(new Color("#3366cc80")));
        Assert.That(group.BufferAlpha, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(group.BufferBlend, Is.EqualTo(BlendMode.Mul));
        Assert.That(group.BufferKeys.Single().Value.X, Is.EqualTo(3f));
    }

    [Test]
    public void TurningBufferingOnIsAnUndoStep()
    {
        var store = new EntityStore();
        LevelGroups groups = LevelGroups.Create();
        Entity level = store.CreateEntity(groups);
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        groups.Groups[3].Buffered = true;
        groups.Groups[3].BufferAlpha = 0.5f;
        history.Record(before, history.Capture());

        history.Undo();
        LevelGroups restored = store.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        Assert.That(restored.Groups[3].Buffered, Is.False);

        history.Redo();
        LevelGroups redone = store.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        Assert.That(redone.Groups[3].Buffered, Is.True);
        Assert.That(redone.Groups[3].BufferAlpha, Is.EqualTo(0.5f).Within(0.001f));
    }

    private static EntityStore EditorLevel(out LevelGroups groups, out EditorHistory history, out Entity editor)
    {
        var store = new EntityStore();
        groups = LevelGroups.Create();
        editor = store.CreateEntity(groups, new NoteSelection(default));
        history = new EditorHistory(store);
        return store;
    }

    private static ItemLayer LayerOfBlock(EntityStore store, Vector2 at) =>
        store.Query<Block>().ToEntityList().Single(block => block.GetComponent<Block>().Position == at).GetComponent<ItemLayer>();

    [Test]
    public void TheHistoryRefusesAnyEditThatBreaksALockEvenWithoutTheCommandsCheck()
    {
        EntityStore store = EditorLevel(out LevelGroups groups, out EditorHistory history, out Entity editor);
        Entity first = EditorItems.Create(store, new Block(new Vector2(10, 10), new Vector2(20, 20)));
        Entity second = EditorItems.Create(store, new Block(new Vector2(50, 10), new Vector2(20, 20)));
        first.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        second.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        groups.Groups[2].Buffered = true;

        // A command that forgot to ask EditorBufferGuard first.
        EditorSnapshot before = history.Capture();
        EditorLayerEdits.PutOnLayer(store, new[] { first }, 1);

        Assert.That(history.Record(before, history.Capture()), Is.False);
        Assert.That(history.CanUndo, Is.False, "nothing is recorded");
        Assert.That(LayerOfBlock(store, new Vector2(10, 10)).Layer, Is.EqualTo(LayerOfBlock(store, new Vector2(50, 10)).Layer), "the move is put back (the restore rebuilds the items)");
        Assert.That(editor.GetComponent<WarningRequest>().Message, Is.EqualTo(GroupBufferRules.OneLayer));
    }

    [Test]
    public void ALevelThatWasAlreadyBrokenCanStillBeEdited()
    {
        EntityStore store = EditorLevel(out LevelGroups groups, out EditorHistory history, out Entity editor);
        Entity first = EditorItems.Create(store, new Block(new Vector2(10, 10), new Vector2(20, 20)));
        Entity second = EditorItems.Create(store, new Block(new Vector2(50, 10), new Vector2(20, 20)));
        first.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        second.AddComponent(new ItemGroups(ItemGroups.Bit(2)));
        ItemLayer.Put(second, new ItemLayer(1, 0));
        groups.Groups[2].Buffered = true;

        EditorSnapshot before = history.Capture();
        first.GetComponent<Block>().Position = new Vector2(30, 30);

        Assert.That(history.Record(before, history.Capture()), Is.True, "an edit that breaks nothing new is kept");
        Assert.That(history.CanUndo, Is.True);
        Assert.That(editor.HasComponent<WarningRequest>(), Is.False);
    }
}
