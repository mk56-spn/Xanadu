using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Storage;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

/// <summary>Step 8 of docs/render-architecture.md: where an item's shader comes from.</summary>
[TestFixture]
public sealed class ItemShaderSourceTests
{
    private static readonly Vector2 Spot = new(100, 100);

    [Test]
    public void EveryKindStartsOnItsOwnShader()
    {
        var store = new EntityStore();
        Assert.Multiple(() =>
        {
            Assert.That(SourceOf(EditorItems.Create(store, new LevelPolygon(Spot, new Xif()))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new Spike(Spot, new Vector2(50, 50)))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new Note(0, Spot))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new Mine(0, Spot))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new StartPoint("", 0, Spot, default, Vector2.Up))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new GrassPatch(Spot, 1))), Is.EqualTo(-1));
            Assert.That(SourceOf(EditorItems.Create(store, new LevelText(Spot, "hi"))), Is.EqualTo(-1));
        });
    }

    [Test]
    public void EveryKindsSourceRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Sources" };
        source.Notes.Add(new NoteDocument { ShaderSource = 1 });
        source.Mines.Add(new MineDocument { ShaderSource = 2 });
        source.StartPoints.Add(new StartPointDocument { ShaderSource = 3 });
        source.Blocks.Add(new BlockDocument { ShaderSource = 4 });
        source.Spikes.Add(new SpikeDocument { ShaderSource = 5 });
        source.Texts.Add(new TextDocument { Text = "t", ShaderSource = 6 });
        source.Grass.Add(new GrassDocument { ShaderSource = 7 });
        source.Polygons.Add(new PolygonDocument { Item = new Xif(), ShaderSource = 8 });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(Sources(read), Is.EqualTo(Sources(source)));
    }

    [Test]
    public void SourcesRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var easy = new XmfVariant { Id = "easy", Name = "Easy" };
        easy.Blocks.Add(new BlockDocument());
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Blocks.Add(new BlockDocument { ShaderSource = 5 });
        mapset.Variants.Add(easy);
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Blocks[0].ShaderSource, Is.EqualTo(-1));
        Assert.That(read.Variants.Single(v => v.Id == "hard").Blocks[0].ShaderSource, Is.EqualTo(5));
    }

    [Test]
    public void ChangingOnlyTheSourceIsAnUndoStep()
    {
        var store = new EntityStore();
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        ItemShaderSource.Put(block, 3);
        history.Record(before, history.Capture());

        history.Undo();
        Assert.That(
            store.Query<Block>().ToEntityList().Select(SourceOf),
            Is.EqualTo(new[] { -1 }));
        history.Redo();
        Assert.That(
            store.Query<Block>().ToEntityList().Select(SourceOf),
            Is.EqualTo(new[] { 3 }));
    }

    private static int SourceOf(Entity entity) => ItemShaderSource.Of(entity);

    private static int[] Sources(IChartItems items) =>
    [
        .. items.Notes.Select(i => i.ShaderSource),
        .. items.Mines.Select(i => i.ShaderSource),
        .. items.StartPoints.Select(i => i.ShaderSource),
        .. items.Blocks.Select(i => i.ShaderSource),
        .. items.Spikes.Select(i => i.ShaderSource),
        .. items.Texts.Select(i => i.ShaderSource),
        .. items.Grass.Select(i => i.ShaderSource),
        .. items.Polygons.Select(i => i.ShaderSource),
    ];
}

/// <summary>Copy and paste through the editor's commands keep the shader source.</summary>
[TestFixture]
public sealed class ItemShaderSourcePasteTests : EcsSystemTest
{
    private EditorCommandBus _commands = null!;
    private Entity _editor;

    protected override void OnEcsSetUp()
    {
        _commands = new EditorCommandBus();
        Root.Add(new EditorCommandSystem(Store, _commands, new EditorHistory(Store)));
        _editor = Store.CreateEntity(
            new LevelInfo("Untitled"),
            new LevelFile(""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new EditorMetronome(false),
            new NoteSelection(default),
            LevelGroups.Create());
    }

    [Test]
    public void APastedCopyKeepsItsSource()
    {
        Entity block = EditorItems.Create(Store, new Block(new Vector2(100, 100), new Vector2(100, 50)));
        ItemShaderSource.Put(block, 2);
        _editor.GetComponent<NoteSelection>().SetMany(new[] { block });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Entity pasted = _editor.GetComponent<NoteSelection>().AllNotes.Single();
        Assert.That(ItemShaderSource.Of(pasted), Is.EqualTo(2));
    }
}
