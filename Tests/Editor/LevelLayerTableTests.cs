using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Storage;
using Xanadu.Systems.Editor;

namespace Xanadu.Tests.Editor;

/// <summary>Step 5 of docs/render-architecture.md: the level's layer table, saved in XMF and carried by undo.</summary>
[TestFixture]
public sealed class LevelLayerTableTests
{
    private static LayerDocument Glow() => new()
    {
        Number = -3,
        Name = "GLOW",
        Pass = LayerPassKind.SelfContained,
        PassShader = 2,
        PassKeys =
        {
            new ShaderKeyDocument { Param = "blur", Time = 0.5f, Value = [4f], Ease = GroupEase.In },
            new ShaderKeyDocument { Param = "tint", Time = 2f, Value = [1f, 0.5f, 0.25f, 1f] },
        },
    };

    private static Entity Editor(EntityStore store) => store.CreateEntity(
        new LevelInfo("Layers", ""),
        new Playfield(1000, 1000),
        new Playhead(0, 120, 0, false),
        new NoteSelection(default),
        LevelLayers.Create());

    private static void AssertSame(LayerDocument read, LayerDocument expected)
    {
        Assert.That((read.Number, read.Name, read.Pass, read.PassShader),
            Is.EqualTo((expected.Number, expected.Name, expected.Pass, expected.PassShader)));
        Assert.That(read.PassKeys.Select(k => (k.Param, k.Time, k.Ease)),
            Is.EqualTo(expected.PassKeys.Select(k => (k.Param, k.Time, k.Ease))));
        Assert.That(read.PassKeys.Select(k => k.Value), Is.EqualTo(expected.PassKeys.Select(k => k.Value)));
    }

    [Test]
    public void TheTableRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Table" };
        source.Layers.Add(Glow());
        source.Layers.Add(new LayerDocument { Number = 4, Name = "HUD", Pass = LayerPassKind.BackgroundReader });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        Assert.That(read.Layers.Select(l => l.Number), Is.EqualTo(new[] { -3, 4 }));
        AssertSame(read.Layers[0], Glow());
        Assert.That((read.Layers[1].Name, read.Layers[1].Pass), Is.EqualTo(("HUD", LayerPassKind.BackgroundReader)));
    }

    [Test]
    public void AMapWithNoLayerSettingsHasNoTableChunk()
    {
        byte[] data = XmfBinary.Write(new LevelDocument { Name = "Plain" });
        ChunkReader reader = ChunkReader.Open(data, "XMF1"u8.ToArray(), XmfBinary.MajorVersion, "XMF");

        Assert.That(reader.TryGet((uint)XmfChunk.Layers, out _), Is.False);
        Assert.That(XmfBinary.Read(data).Layers, Is.Empty);
    }

    [Test]
    public void TheTableRoundTripsThroughAMapsetPerVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var easy = new XmfVariant { Id = "easy", Name = "Easy" };
        var hard = new XmfVariant { Id = "hard", Name = "Hard" };
        hard.Layers.Add(Glow());
        mapset.Variants.Add(easy);
        mapset.Variants.Add(hard);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        Assert.That(read.Variants.Single(v => v.Id == "easy").Layers, Is.Empty);
        AssertSame(read.Variants.Single(v => v.Id == "hard").Layers.Single(), Glow());
        Assert.That(LevelStore.ToLevelDocument(read, read.Variants[1]).Layers.Single().Name, Is.EqualTo("GLOW"));
    }

    [Test]
    public void TheMapperCarriesTheTableBothWays()
    {
        var source = new LevelDocument { Name = "Table" };
        source.Layers.Add(Glow());
        var store = new EntityStore();
        Entity editor = Editor(store);

        LevelDocumentMapper.Apply(store, editor, source);
        LevelLayer layer = editor.GetComponent<LevelLayers>().Layers[-3];
        Assert.That((layer.Name, layer.Pass, layer.PassShader), Is.EqualTo(("GLOW", LayerPassKind.SelfContained, 2)));
        Assert.That(layer.PassKeys[1], Is.EqualTo(new ShaderKey("tint", 2f, new Vector4(1f, 0.5f, 0.25f, 1f), 4, GroupEase.Linear)));

        LevelDocument captured = LevelDocumentMapper.Capture(store, editor);
        AssertSame(captured.Layers.Single(), Glow());
    }

    [Test]
    public void EntriesForTheWedgeOutOfRangeOrWithoutSettingsAreDropped()
    {
        var source = new LevelDocument { Name = "Odd" };
        source.Layers.Add(new LayerDocument { Number = 0, Name = "WEDGE" });
        source.Layers.Add(new LayerDocument { Number = 900, Name = "FAR" });
        source.Layers.Add(new LayerDocument { Number = 5 });
        var store = new EntityStore();
        Entity editor = Editor(store);

        LevelDocumentMapper.Apply(store, editor, source);

        Assert.That(editor.GetComponent<LevelLayers>().Layers, Is.Empty);
        Assert.That(LevelDocumentMapper.Capture(store, editor).Layers, Is.Empty);
    }

    [Test]
    public void ABlankNameTakesTheEntryOut()
    {
        LevelLayers table = LevelLayers.Create();
        table.SetName(2, "  FRONT ");
        Assert.That(table.NameOf(2), Is.EqualTo("FRONT"));

        table.SetName(2, " ");
        Assert.That(table.Layers, Is.Empty);
    }

    [Test]
    public void RenamingALayerIsAnUndoStep()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        editor.GetComponent<LevelLayers>().SetName(-2, "TERRAIN");
        history.Record(before, history.Capture());

        Assert.That(history.CanUndo, Is.True);
        history.Undo();
        Assert.That(editor.GetComponent<LevelLayers>().NameOf(-2), Is.Empty);
        history.Redo();
        Assert.That(editor.GetComponent<LevelLayers>().NameOf(-2), Is.EqualTo("TERRAIN"));
    }

    [Test]
    public void AddingALayerMovesTheTableWithTheLayersAndUndoMovesItBack()
    {
        var store = new EntityStore();
        Entity editor = Editor(store);
        var history = new EditorHistory(store);
        Entity block = EditorItems.Create(store, new Block(new Vector2(10, 10), new Vector2(50, 50)));
        ItemLayer.Put(block, new ItemLayer(1, 0));
        editor.GetComponent<LevelLayers>().SetName(1, "ONE");
        EditorLayers layers = EditorLayers.Create();
        EditorSnapshot before = history.Capture();

        EditorLayerEdits.Insert(store, ref layers, ref editor.GetComponent<LevelLayers>(), 0, front: true);
        history.Record(before, history.Capture());

        Assert.That(block.GetComponent<ItemLayer>().Layer, Is.EqualTo(2));
        Assert.That(editor.GetComponent<LevelLayers>().NameOf(2), Is.EqualTo("ONE"));
        history.Undo();
        Assert.That(editor.GetComponent<LevelLayers>().NameOf(1), Is.EqualTo("ONE"), "the name went back with its items");
    }
}
