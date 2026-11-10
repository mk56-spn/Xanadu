using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Items;
using Xanadu.Layering;
using Xanadu.Levels;
using Xanadu.Storage;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

/// <summary>Step 2 of docs/render-architecture.md: every level item has a layer and a z.</summary>
[TestFixture]
public sealed class ItemLayerTests
{
    private static readonly Vector2 Spot = new(100, 100);

    [Test]
    public void EachKindStartsOnItsDefaultLayer()
    {
        var store = new EntityStore();
        Assert.Multiple(() =>
        {
            Assert.That(LayerOf(EditorItems.Create(store, new LevelPolygon(Spot, new Xif()))), Is.EqualTo(-3));
            Assert.That(LayerOf(EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)))), Is.EqualTo(-2));
            Assert.That(LayerOf(EditorItems.Create(store, new Spike(Spot, new Vector2(50, 50)))), Is.EqualTo(-2));
            Assert.That(LayerOf(EditorItems.Create(store, new Note(0, Spot))), Is.EqualTo(-1));
            Assert.That(LayerOf(EditorItems.Create(store, new Mine(0, Spot))), Is.EqualTo(-1));
            Assert.That(LayerOf(EditorItems.Create(store, new StartPoint("", 0, Spot, default, Vector2.Up))), Is.EqualTo(-1));
            Assert.That(LayerOf(EditorItems.Create(store, new GrassPatch(Spot, 1))), Is.EqualTo(1));
            Assert.That(LayerOf(EditorItems.Create(store, new LevelText(Spot, "hi"))), Is.EqualTo(2));
        });
    }

    [Test]
    public void APlacedItemDrawsInFrontOfWhatIsAlreadyOnItsLayer()
    {
        var store = new EntityStore();
        Entity first = EditorItems.Place(store, new Block(Spot, new Vector2(50, 50)));
        Entity second = EditorItems.Place(store, new Spike(Spot + new Vector2(100, 0), new Vector2(50, 50)));
        Entity note = EditorItems.Place(store, new Note(0, Spot));

        Assert.That(second.GetComponent<ItemLayer>().Z, Is.GreaterThan(first.GetComponent<ItemLayer>().Z));
        Assert.That(note.GetComponent<ItemLayer>().Z, Is.Zero, "another layer does not push it up");
    }

    [Test]
    public void ItemsPutOnTopKeepTheirOrderAmongThemselves()
    {
        var store = new EntityStore();
        Entity existing = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemLayer.Put(existing, new ItemLayer(-2, 10));
        Entity low = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemLayer.Put(low, new ItemLayer(-2, 2));
        Entity high = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemLayer.Put(high, new ItemLayer(-2, 5));

        ItemLayer.OnTop(store, [low, high]);

        Assert.That(low.GetComponent<ItemLayer>().Z, Is.EqualTo(11));
        Assert.That(high.GetComponent<ItemLayer>().Z, Is.EqualTo(14));
    }

    [Test]
    public void LayerZeroMeansTheDefaultAndLayersStayInRange()
    {
        var store = new EntityStore();
        Entity text = EditorItems.Create(store, new LevelText(Spot, "hi"));

        ItemLayer.Put(text, new ItemLayer(0, 4));
        Assert.That(text.GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(ItemLayer.Texts, 4)));

        ItemLayer.Put(text, new ItemLayer(900, 0));
        Assert.That(text.GetComponent<ItemLayer>().Layer, Is.EqualTo(DrawOrder.MaxLayer));
    }

    [Test]
    public void TheMapperCarriesLayersBothWays()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemLayer.Put(block, new ItemLayer(7, 3));
        Entity note = EditorItems.Create(store, new Note(0, Spot));
        ItemLayer.Put(note, new ItemLayer(-12, 9));

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);
        Assert.That((document.Blocks[0].Layer, document.Blocks[0].Z), Is.EqualTo((7, 3)));
        Assert.That((document.Notes[0].Layer, document.Notes[0].Z), Is.EqualTo((-12, 9)));

        // A document from a file without layers has 0s: its items take their defaults.
        document.Notes[0].Layer = 0;
        var target = new EntityStore();
        LevelDocumentMapper.Apply(target, Editor(target), document);

        Assert.That(target.Query<Block>().ToEntityList()[0].GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(7, 3)));
        Assert.That(target.Query<Note>().ToEntityList()[0].GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(ItemLayer.Gameplay, 9)));
    }

    [Test]
    public void EveryKindsLayersRoundTripThroughAMap()
    {
        var source = new LevelDocument { Name = "Layers" };
        source.Notes.Add(new NoteDocument { Layer = -5, Z = 1 });
        source.Mines.Add(new MineDocument { Layer = -6, Z = 2 });
        source.StartPoints.Add(new StartPointDocument { Layer = -7, Z = 3 });
        source.Blocks.Add(new BlockDocument { Layer = -8, Z = 4 });
        source.Spikes.Add(new SpikeDocument { Layer = 5, Z = 5 });
        source.Texts.Add(new TextDocument { Text = "t", Layer = 6, Z = 6 });
        source.Grass.Add(new GrassDocument { Layer = 7, Z = 7 });
        source.Polygons.Add(new PolygonDocument { Item = new Xif(), Layer = 200, Z = -8 });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(Layers(read), Is.EqualTo(Layers(source)));
    }

    [Test]
    public void AnItemOnItsDefaultReadsBackOnLayerZero()
    {
        var source = new LevelDocument { Name = "Plain" };
        source.Blocks.Add(new BlockDocument());

        Assert.That(XmfBinary.Read(XmfBinary.Write(source)).Blocks[0].Layer, Is.Zero);
    }

    [Test]
    public void LayersRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var easy = new XmfVariant { Id = "easy", Name = "Easy" };
        easy.Blocks.Add(new BlockDocument());
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Blocks.Add(new BlockDocument { Layer = 3, Z = 8 });
        mapset.Variants.Add(easy);
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Blocks[0].Layer, Is.Zero);
        BlockDocument block = read.Variants.Single(v => v.Id == "hard").Blocks[0];
        Assert.That((block.Layer, block.Z), Is.EqualTo((3, 8)));
    }

    [Test]
    public void ChangingOnlyALayerIsAnUndoStep()
    {
        var store = new EntityStore();
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        ItemLayer.Put(block, new ItemLayer(4, 2));
        history.Record(before, history.Capture());

        Assert.That(history.CanUndo, Is.True);
        history.Undo();
        Assert.That(store.Query<Block>().ToEntityList()[0].GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(ItemLayer.Terrain, 0)));
        history.Redo();
        Assert.That(store.Query<Block>().ToEntityList()[0].GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(4, 2)));
    }

    private static int LayerOf(Entity entity) => entity.GetComponent<ItemLayer>().Layer;

    private static Entity Editor(EntityStore store) => store.CreateEntity(
        new LevelInfo("Layers"),
        new Playfield(1000, 1000),
        new Playhead(0, 120, 0, false),
        new NoteSelection(default));

    private static (int, int)[] Layers(IChartItems items) =>
    [
        .. items.Notes.Select(i => (i.Layer, i.Z)),
        .. items.Mines.Select(i => (i.Layer, i.Z)),
        .. items.StartPoints.Select(i => (i.Layer, i.Z)),
        .. items.Blocks.Select(i => (i.Layer, i.Z)),
        .. items.Spikes.Select(i => (i.Layer, i.Z)),
        .. items.Texts.Select(i => (i.Layer, i.Z)),
        .. items.Grass.Select(i => (i.Layer, i.Z)),
        .. items.Polygons.Select(i => (i.Layer, i.Z)),
    ];
}

/// <summary>Copy and paste through the editor's commands keep layers.</summary>
[TestFixture]
public sealed class ItemLayerPasteTests : EcsSystemTest
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
    public void APastedCopyKeepsItsLayerAndLandsInFront()
    {
        Entity block = EditorItems.Create(Store, new Block(new Vector2(100, 100), new Vector2(100, 50)));
        ItemLayer.Put(block, new ItemLayer(9, 4));
        Entity other = EditorItems.Create(Store, new Block(new Vector2(600, 600), new Vector2(100, 50)));
        ItemLayer.Put(other, new ItemLayer(9, 20));
        _editor.GetComponent<NoteSelection>().SetMany(new[] { block });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Entity pasted = _editor.GetComponent<NoteSelection>().AllNotes.Single();
        Assert.That(pasted, Is.Not.EqualTo(block));
        Assert.That(pasted.GetComponent<ItemLayer>(), Is.EqualTo(new ItemLayer(9, 21)));
    }
}
