using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Items;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Storage;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

/// <summary>Step 7 of docs/render-architecture.md: an item's static blend mode.</summary>
[TestFixture]
public sealed class ItemBlendTests
{
    private static readonly Vector2 Spot = new(100, 100);

    [Test]
    public void EveryKindStartsOnMix()
    {
        var store = new EntityStore();
        Assert.Multiple(() =>
        {
            Assert.That(BlendOf(EditorItems.Create(store, new LevelPolygon(Spot, new Xif()))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new Spike(Spot, new Vector2(50, 50)))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new Note(0, Spot))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new Mine(0, Spot))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new StartPoint("", 0, Spot, default, Vector2.Up))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new GrassPatch(Spot, 1))), Is.EqualTo(BlendMode.Mix));
            Assert.That(BlendOf(EditorItems.Create(store, new LevelText(Spot, "hi"))), Is.EqualTo(BlendMode.Mix));
        });
    }

    [Test]
    public void TheModesStepAndWrapAround()
    {
        Assert.That(BlendModes.Step(BlendMode.Mix, 1), Is.EqualTo(BlendMode.Add));
        Assert.That(BlendModes.Step(BlendMode.Add, 1), Is.EqualTo(BlendMode.Sub));
        Assert.That(BlendModes.Step(BlendMode.Sub, 1), Is.EqualTo(BlendMode.Mul));
        Assert.That(BlendModes.Step(BlendMode.Mul, 1), Is.EqualTo(BlendMode.Premultiplied));
        Assert.That(BlendModes.Step(BlendMode.Premultiplied, 1), Is.EqualTo(BlendMode.Mix));
        Assert.That(BlendModes.Step(BlendMode.Mix, -1), Is.EqualTo(BlendMode.Premultiplied));
        Assert.That(BlendModes.Step(BlendMode.Mix, 5), Is.EqualTo(BlendMode.Mix), "a full turn changes nothing");
        Assert.That(BlendModes.Name(BlendMode.Premultiplied), Is.EqualTo("PREMUL"));
    }

    [Test]
    public void TheBlendChoiceSetsEverySelectedItemAndUndoesAsOneStep()
    {
        var store = new EntityStore();
        var history = new EditorHistory(store);
        Entity first = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        Entity second = EditorItems.Create(store, new Note(0, Spot));
        EditorSnapshot before = history.Capture();

        Assert.That(EditorLayerEdits.SetBlend([first, second], BlendMode.Add), Is.True);
        history.Record(before, history.Capture());

        Assert.That(BlendOf(first), Is.EqualTo(BlendMode.Add));
        Assert.That(BlendOf(second), Is.EqualTo(BlendMode.Add));
        Assert.That(
            EditorLayerEdits.SetBlend([first, second], BlendMode.Add),
            Is.False,
            "choosing the mode they already have is not an edit");

        // Undo rebuilds the items from the snapshot, so ask the store for them again.
        history.Undo();
        Assert.That(store.Query<Block>().ToEntityList().Select(e => BlendOf(e)), Is.EqualTo(new[] { BlendMode.Mix }));
        Assert.That(store.Query<Note>().ToEntityList().Select(e => BlendOf(e)), Is.EqualTo(new[] { BlendMode.Mix }));

        history.Redo();
        Assert.That(store.Query<Block>().ToEntityList().Select(e => BlendOf(e)), Is.EqualTo(new[] { BlendMode.Add }));
    }

    [Test]
    public void TheMapperCarriesBlendsBothWays()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        Entity block = EditorItems.Create(store, new Block(Spot, new Vector2(50, 50)));
        ItemBlend.Put(block, BlendMode.Add);
        Entity note = EditorItems.Create(store, new Note(0, Spot));
        ItemBlend.Put(note, BlendMode.Mul);

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);
        Assert.That(document.Blocks[0].Blend, Is.EqualTo(BlendMode.Add));
        Assert.That(document.Notes[0].Blend, Is.EqualTo(BlendMode.Mul));

        var target = new EntityStore();
        LevelDocumentMapper.Apply(target, Editor(target), document);

        Assert.That(BlendOf(target.Query<Block>().ToEntityList()[0]), Is.EqualTo(BlendMode.Add));
        Assert.That(BlendOf(target.Query<Note>().ToEntityList()[0]), Is.EqualTo(BlendMode.Mul));
    }

    [Test]
    public void EveryKindsBlendRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Blends" };
        source.Notes.Add(new NoteDocument { Blend = BlendMode.Add });
        source.Mines.Add(new MineDocument { Blend = BlendMode.Sub });
        source.StartPoints.Add(new StartPointDocument { Blend = BlendMode.Mul });
        source.Blocks.Add(new BlockDocument { Blend = BlendMode.Premultiplied });
        source.Spikes.Add(new SpikeDocument { Blend = BlendMode.Add });
        source.Texts.Add(new TextDocument { Text = "t", Blend = BlendMode.Mul });
        source.Grass.Add(new GrassDocument { Blend = BlendMode.Add });
        source.Polygons.Add(new PolygonDocument { Item = new Xif(), Blend = BlendMode.Sub });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(Modes(read), Is.EqualTo(Modes(source)));
    }

    [Test]
    public void AMapWithEveryItemOnMixHasNoBlendChunk()
    {
        var source = new LevelDocument { Name = "Plain" };
        source.Blocks.Add(new BlockDocument());
        byte[] data = XmfBinary.Write(source);
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet(19, out _), Is.False);
        Assert.That(XmfBinary.Read(data).Blocks[0].Blend, Is.EqualTo(BlendMode.Mix));
    }

    [Test]
    public void BlendsRoundTripThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var easy = new XmfVariant { Id = "easy", Name = "Easy" };
        easy.Blocks.Add(new BlockDocument());
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Blocks.Add(new BlockDocument { Blend = BlendMode.Add });
        mapset.Variants.Add(easy);
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Blocks[0].Blend, Is.EqualTo(BlendMode.Mix));
        Assert.That(read.Variants.Single(v => v.Id == "hard").Blocks[0].Blend, Is.EqualTo(BlendMode.Add));
    }

    private static BlendMode BlendOf(Entity entity) => ItemBlend.Of(entity);

    private static Entity Editor(EntityStore store) => store.CreateEntity(
        new LevelInfo("Blends"),
        new Playfield(1000, 1000),
        new Playhead(0, 120, 0, false),
        new NoteSelection(default));

    private static BlendMode[] Modes(IChartItems items) =>
    [
        .. items.Notes.Select(i => i.Blend),
        .. items.Mines.Select(i => i.Blend),
        .. items.StartPoints.Select(i => i.Blend),
        .. items.Blocks.Select(i => i.Blend),
        .. items.Spikes.Select(i => i.Blend),
        .. items.Texts.Select(i => i.Blend),
        .. items.Grass.Select(i => i.Blend),
        .. items.Polygons.Select(i => i.Blend),
    ];
}

/// <summary>Copy and paste through the editor's commands keep blend modes.</summary>
[TestFixture]
public sealed class ItemBlendPasteTests : EcsSystemTest
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
    public void APastedCopyKeepsItsBlend()
    {
        Entity block = EditorItems.Create(Store, new Block(new Vector2(100, 100), new Vector2(100, 50)));
        ItemBlend.Put(block, BlendMode.Add);
        _editor.GetComponent<NoteSelection>().SetMany(new[] { block });

        _commands.Enqueue(EditorCommand.CopySelection());
        _commands.Enqueue(EditorCommand.PasteSelection());
        Frame();

        Entity pasted = _editor.GetComponent<NoteSelection>().AllNotes.Single();
        Assert.That(pasted, Is.Not.EqualTo(block));
        Assert.That(ItemBlend.Of(pasted), Is.EqualTo(BlendMode.Add));
    }
}
