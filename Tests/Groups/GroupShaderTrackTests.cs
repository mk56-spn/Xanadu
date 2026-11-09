using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Storage;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Groups;

/// <summary>
/// Step 8 of docs/render-architecture.md: a group's keyed item-shader track and
/// the item shader source that follows it.
/// </summary>
[TestFixture]
public sealed class GroupShaderTrackTests
{
    private static readonly Vector2 Spot = new(100, 100);

    private static GroupDefinition[] Slots()
    {
        var groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        return groups;
    }

    [Test]
    public void AKeyHoldsBeforeTheFirstKeyAndAfterTheLast()
    {
        var keys = new List<ShaderKey>
        {
            new("tint", 1f, new Vector4(1, 0, 0, 1), 4, GroupEase.Linear),
            new("tint", 3f, new Vector4(0, 1, 0, 1), 4, GroupEase.Linear),
        };

        Assert.That(ShaderKeyTrack.ValueAt(keys, "tint", 0f), Is.EqualTo(new Vector4(1, 0, 0, 1)));
        Assert.That(ShaderKeyTrack.ValueAt(keys, "tint", 2f), Is.EqualTo(new Vector4(0.5f, 0.5f, 0, 1)));
        Assert.That(ShaderKeyTrack.ValueAt(keys, "tint", 9f), Is.EqualTo(new Vector4(0, 1, 0, 1)));
    }

    [Test]
    public void AnEasedKeyBendsTheSpan()
    {
        var keys = new List<ShaderKey>
        {
            new("speed", 0f, new Vector4(0, 0, 0, 0), 1, GroupEase.Out),
            new("speed", 1f, new Vector4(1, 0, 0, 0), 1, GroupEase.Out),
        };

        Assert.That(ShaderKeyTrack.ValueAt(keys, "speed", 0.5f).X, Is.EqualTo(0.75f).Within(0.001f));
    }

    [Test]
    public void AnUnkeyedSettingHoldsTheShadersDefaultAndTheFirstKeyStartsThere()
    {
        var uniform = new InstanceUniform("strength", 1, new Vector4(1f, 0, 0, 0));
        var keys = new List<ShaderKey>();

        Assert.That(ShaderKeyTrack.ValueAt(keys, uniform.Name, 3f, uniform.Default).X, Is.EqualTo(1f));

        EditorShaderKeyEdits.KeyValue(keys, uniform, 0, 0.05f, 3f);

        Assert.That(keys.Single().Value.X, Is.EqualTo(1.05f).Within(0.0001f), "nudged from the default, not from zero");
    }

    [Test]
    public void AKeyAtTheSameMomentReplacesTheEarlierOne()
    {
        var keys = new List<ShaderKey>
        {
            new("tint", 1f, new Vector4(1, 0, 0, 1), 4, GroupEase.Linear),
            new("tint", 1f, new Vector4(0, 1, 0, 1), 4, GroupEase.Linear),
        };

        Assert.That(ShaderKeyTrack.ValueAt(keys, "tint", 2f), Is.EqualTo(new Vector4(0, 1, 0, 1)));
    }

    [Test]
    public void EvaluateListsEverySettingOnceInOrder()
    {
        var keys = new List<ShaderKey>
        {
            new("b", 0f, new Vector4(1, 0, 0, 0), 1, GroupEase.Linear),
            new("a", 0f, new Vector4(0, 1, 0, 0), 2, GroupEase.Linear),
            new("b", 1f, new Vector4(2, 0, 0, 0), 1, GroupEase.Linear),
        };

        List<(string Param, Vector4 Value, int Size)> values = ShaderKeyTrack.Evaluate(keys, 0.5f);

        Assert.That(values.Select(value => value.Param), Is.EqualTo(new[] { "b", "a" }));
        Assert.That(values[0].Value.X, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(values[0].Size, Is.EqualTo(1));
        Assert.That(values[1].Size, Is.EqualTo(2));
    }

    [Test]
    public void AnItemFollowsAGroupOnlyWhenItIsInItsMaskAndTheTrackHasAShader()
    {
        GroupDefinition[] groups = Slots();
        groups[2].Shader = 4;
        groups[2].ShaderKeys.Add(new ShaderKey("tint", 0f, new Vector4(1, 1, 1, 1), 4, GroupEase.Linear));

        Assert.That(
            GroupModulation.TryItemShader(groups, ItemGroups.Bit(2), 2, 0f, out int slot, out _),
            Is.True);
        Assert.That(slot, Is.EqualTo(4));

        Assert.That(
            GroupModulation.TryItemShader(groups, ItemGroups.Bit(1), 2, 0f, out _, out _),
            Is.False,
            "a source outside the mask falls back to own");
    }

    [Test]
    public void AGroupWithKeysButNoSlotFallsBackToOwn()
    {
        GroupDefinition[] groups = Slots();
        groups[0].ShaderKeys.Add(new ShaderKey("tint", 0f, new Vector4(1, 1, 1, 1), 4, GroupEase.Linear));

        Assert.That(
            GroupModulation.TryItemShader(groups, ItemGroups.Bit(0), 0, 0f, out _, out _),
            Is.False);
    }

    [Test]
    public void ClearedKeysLeaveTheSlotAndTheItemStillFollowsIt()
    {
        GroupDefinition[] groups = Slots();
        groups[0].Shader = 2;

        Assert.That(
            GroupModulation.TryItemShader(groups, ItemBits(0), 0, 0f, out int slot, out var values),
            Is.True);
        Assert.That(slot, Is.EqualTo(2));
        Assert.That(values, Is.Empty);
    }

    [Test]
    public void TheGroupTrackAndTheItemsSourceRoundTripThroughAMap()
    {
        var source = new LevelDocument { Name = "Track" };
        source.Groups.Add(new GroupDocument
        {
            Id = 3,
            Name = "Glow",
            Shader = 2,
            ShaderKeys =
            {
                new ShaderKeyDocument
                {
                    Param = "tint",
                    Time = 1.5f,
                    Value = new[] { 0.1f, 0.2f, 0.3f, 0.4f },
                    Ease = GroupEase.Out,
                },
            },
        });
        source.Blocks.Add(new BlockDocument { ShaderSource = 3 });
        source.Notes.Add(new NoteDocument { ShaderSource = -1 });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        GroupDocument group = read.Groups.Single();
        Assert.That((group.Shader, group.ShaderKeys.Count), Is.EqualTo((2, 1)));
        Assert.That(
            (group.ShaderKeys[0].Param, group.ShaderKeys[0].Time, group.ShaderKeys[0].Ease),
            Is.EqualTo(("tint", 1.5f, GroupEase.Out)));
        Assert.That(group.ShaderKeys[0].Value, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f, 0.4f }));
        Assert.That(read.Blocks[0].ShaderSource, Is.EqualTo(3));
        Assert.That(read.Notes[0].ShaderSource, Is.EqualTo(-1));
    }

    [Test]
    public void ALevelWithNoTracksAndNoSourcesHasNeitherChunk()
    {
        var source = new LevelDocument { Name = "Plain" };
        source.Groups.Add(new GroupDocument { Id = 0, Name = "A" });
        source.Blocks.Add(new BlockDocument());
        byte[] data = XmfBinary.Write(source);
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet(20, out _), Is.False);
        Assert.That(reader.TryGet(21, out _), Is.False);
    }

    [Test]
    public void TheMapperCarriesTheTrackAndTheItemsSource()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new LevelInfo("Track"),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default),
            LevelGroups.Create());
        LevelGroups groups = editor.GetComponent<LevelGroups>();
        groups.Groups[0].Shader = 2;
        groups.Groups[0].ShaderKeys.Add(new ShaderKey("tint", 1f, new Vector4(1, 0, 0, 1), 4, GroupEase.In));
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemShaderSource.Put(block, 0);

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);
        Assert.That(document.Groups[0].Shader, Is.EqualTo(2));
        Assert.That(document.Groups[0].ShaderKeys.Single().Ease, Is.EqualTo(GroupEase.In));
        Assert.That(document.Blocks[0].ShaderSource, Is.EqualTo(0));

        var target = new EntityStore();
        LevelDocumentMapper.Apply(target, target.CreateEntity(
            new LevelInfo(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default)), document);

        LevelGroups applied = target.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        Assert.That(applied.Groups[0].Shader, Is.EqualTo(2));
        Assert.That(applied.Groups[0].ShaderKeys.Single().Value, Is.EqualTo(new Vector4(1, 0, 0, 1)));
        Assert.That(ItemShaderSource.Of(target.Query<Block>().ToEntityList().Single()), Is.EqualTo(0));
    }

    [Test]
    public void ChangingTheTrackIsAnUndoStep()
    {
        var store = new EntityStore();
        LevelGroups groups = LevelGroups.Create();
        Entity level = store.CreateEntity(groups);
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        groups.Groups[2].Shader = 1;
        groups.Groups[2].ShaderKeys.Add(new ShaderKey("speed", 0f, new Vector4(1, 0, 0, 0), 1, GroupEase.Linear));
        history.Record(before, history.Capture());

        history.Undo();
        LevelGroups restored = store.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        Assert.That(restored.Groups[2].Shader, Is.Zero);
        Assert.That(restored.Groups[2].ShaderKeys, Is.Empty);

        history.Redo();
        LevelGroups redone = store.Query<LevelGroups>().ToEntityList().Single().GetComponent<LevelGroups>();
        Assert.That(redone.Groups[2].Shader, Is.EqualTo(1));
        Assert.That(redone.Groups[2].ShaderKeys.Single().Param, Is.EqualTo("speed"));
    }

    [Test]
    public void TheSourceSkipsGroupsWithoutAShaderAndResetsWhenTheItemLeavesTheGroup()
    {
        var store = new EntityStore();
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        block.AddComponent(new ItemGroups(ItemGroups.FromList(new[] { 1, 4 })));
        LevelGroups groups = LevelGroups.Create();
        groups.Groups[4].Shader = 2;

        Assert.That(EditorLayerEdits.StepShaderSource([block], 1, groups.Groups), Is.True);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(4), "group 1 has no shader, so it's skipped");
        EditorLayerEdits.StepShaderSource([block], 1, groups.Groups);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(ItemShaderSource.Own));
    }

    [Test]
    public void TheSourceStepsThroughOwnAndTheItemsGroups()
    {
        var store = new EntityStore();
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        block.AddComponent(new ItemGroups(ItemGroups.FromList(new[] { 1, 4 })));

        Assert.That(EditorLayerEdits.StepShaderSource([block], 1), Is.True);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(1));
        EditorLayerEdits.StepShaderSource([block], 1);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(4));
        EditorLayerEdits.StepShaderSource([block], 1);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(ItemShaderSource.Own));
        EditorLayerEdits.StepShaderSource([block], -1);
        Assert.That(ItemShaderSource.Of(block), Is.EqualTo(4));
    }

    private static uint ItemBits(int group) => ItemGroups.Bit(group);
}
