using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Shaders;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

/// <summary>Step 11 of docs/render-architecture.md: screen-read regions refresh the back buffer where they are reached.</summary>
[TestFixture]
public sealed class ScreenReadTests
{
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(800, 600));
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly Playhead Head = new(0, 120, 0, false);
    private static readonly Rid Main = TestRid.Make(1);

    [Test]
    public void TheDrawListSortsARegionByItsLayerAndZ()
    {
        var store = new EntityStore();
        Entity back = EditorItems.Create(store, new Block(new Vector2(10, 10), new Vector2(50, 50)));
        ItemLayer.Put(back, new ItemLayer(-1, 0));
        Entity region = Region(store, new Vector2(100, 100), new Vector2(200, 150), -1, 5);
        Entity front = EditorItems.Create(store, new Block(new Vector2(300, 10), new Vector2(50, 50)));
        ItemLayer.Put(front, new ItemLayer(-1, 9));

        var entries = new List<PlayfieldDrawEntry>();
        PlayfieldDrawList.Gather(store, entries, startPoints: false, railLanes: false);

        int atBack = entries.FindIndex(entry => entry.Entity.Id == back.Id);
        int atRegion = entries.FindIndex(entry => entry.Entity.Id == region.Id);
        int atFront = entries.FindIndex(entry => entry.Entity.Id == front.Id);
        Assert.That(atRegion, Is.GreaterThan(atBack));
        Assert.That(atRegion, Is.LessThan(atFront));
        Assert.That(entries[atRegion].Kind, Is.EqualTo(PlayfieldDrawKind.ScreenRead));
    }

    [Test]
    public void ARectCopyHappensWhereTheRegionIsReached()
    {
        var store = new EntityStore();
        Entity before = EditorItems.Create(store, new Block(new Vector2(10, 10), new Vector2(50, 50)));
        ItemLayer.Put(before, new ItemLayer(-1, 0));
        Region(store, new Vector2(200, 100), new Vector2(300, 200), -1, 5);
        Entity after = EditorItems.Create(store, new Block(new Vector2(300, 10), new Vector2(50, 50)));
        ItemLayer.Put(after, new ItemLayer(-1, 9));

        var canvas = new RecordingCanvas();
        var items = new RecordingItems();
        var routed = new ShaderRoutingCanvas(canvas, Main, items);
        routed.Clear(Main);

        PlayfieldRenderer.DrawLayers(
            routed,
            Main,
            store,
            new PlayfieldItemFrame(View, Field, Head, 0f, Editor: false),
            drawWedge: null);

        Assert.That(items.Copies, Has.Count.EqualTo(1));
        Assert.That(items.Copies[0].Rect, Is.EqualTo(new Rect2(200, 100, 300, 200)));
        int copyAt = items.Created.IndexOf(items.Copies[0].Item);
        Assert.That(copyAt, Is.GreaterThanOrEqualTo(0));
        Rid afterItem = canvas.Draws.Last().Item;
        int afterAt = items.Created.IndexOf(afterItem);
        Assert.That(afterAt, Is.GreaterThan(copyAt), "the draw after the copy lands after it");
    }

    [Test]
    public void TheRegionToolPlacesOnTheCurrentLayer()
    {
        var store = new EntityStore();
        var history = new EditorHistory(store);
        Entity editor = store.CreateEntity(
            new LevelInfo("", ""),
            new Playfield(1000, 1000),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(800, 600)),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default),
            EditorLayers.Create(),
            LevelGroups.Create(),
            new EditorPlacement { Mode = EditorPlacementMode.ScreenRead });

        ref EditorLayers layers = ref editor.GetComponent<EditorLayers>();
        layers.Current = 2;

        var creator = new EditorPlacementCreator(new EditorSceneQuery(store), history);
        creator.Create(editor, new Vector2(400, 300));

        Entity region = store.Query<ScreenReadRegion>().ToEntityList().Single();
        Assert.That(ItemLayer.Of(region).Layer, Is.EqualTo(2));
        Assert.That(editor.GetComponent<NoteSelection>().Note, Is.EqualTo(region));
        Assert.That(history.CanUndo, Is.True);
    }

    [Test]
    public void ARegionRoundTripsThroughAMap()
    {
        var source = new LevelDocument { Name = "Reads" };
        source.ScreenReads.Add(new ScreenReadDocument
        {
            X = 10,
            Y = 20,
            Width = 300,
            Height = 200,
            Layer = -2,
            Z = 7,
        });

        LevelDocument read = XmfBinary.Read(XmfBinary.Write(source));

        ScreenReadDocument region = read.ScreenReads.Single();
        Assert.That(region.X, Is.EqualTo(10));
        Assert.That(region.Width, Is.EqualTo(300));
        Assert.That(region.Height, Is.EqualTo(200));
        Assert.That(region.Layer, Is.EqualTo(-2));
        Assert.That(region.Z, Is.EqualTo(7));
    }

    [Test]
    public void ARegionRoundTripsThroughAMapsetVariant()
    {
        var mapset = new XmfMapset { Id = "set", SongId = "song" };
        var variant = new XmfVariant { Id = "hard", Name = "Hard" };
        variant.ScreenReads.Add(new ScreenReadDocument { Width = 300, Height = 200, Layer = 1, Z = 4 });
        mapset.Variants.Add(variant);

        XmfMapset read = XmfBinary.ReadMapset(XmfBinary.WriteMapset(mapset));

        ScreenReadDocument region = read.Variants.Single().ScreenReads.Single();
        Assert.That(region.Width, Is.EqualTo(300));
        Assert.That(region.Layer, Is.EqualTo(1));
        Assert.That(region.Z, Is.EqualTo(4));
    }

    [Test]
    public void TheMapperCarriesRegionsBothWays()
    {
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new LevelInfo("Reads", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default),
            LevelGroups.Create(),
            LevelLayers.Create());
        Entity created = EditorItems.Create(store, new ScreenReadRegion(new Vector2(30, 40), new Vector2(300, 200)));
        ItemLayer.Put(created, new ItemLayer(-1, 6));

        LevelDocument document = LevelDocumentMapper.Capture(store, editor);
        Assert.That(document.ScreenReads.Single().Layer, Is.EqualTo(-1));
        Assert.That(document.ScreenReads.Single().Z, Is.EqualTo(6));

        var target = new EntityStore();
        LevelDocumentMapper.Apply(target, target.CreateEntity(
            new LevelInfo("", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new NoteSelection(default)), document);

        Entity applied = target.Query<ScreenReadRegion>().ToEntityList().Single();
        Assert.That(applied.GetComponent<ScreenReadRegion>().Size, Is.EqualTo(new Vector2(300, 200)));
        Assert.That(ItemLayer.Of(applied), Is.EqualTo(new ItemLayer(-1, 6)));
    }

    [Test]
    public void MovingARegionIsOneUndoStep()
    {
        var store = new EntityStore();
        Entity region = EditorItems.Create(store, new ScreenReadRegion(new Vector2(10, 20), new Vector2(200, 100)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        region.GetComponent<ScreenReadRegion>().Position = new Vector2(90, 120);
        history.Record(before, history.Capture());
        Assert.That(history.Undo(), Is.True);

        ScreenReadRegion restored = store.Query<ScreenReadRegion>().ToEntityList().Single().GetComponent<ScreenReadRegion>();
        Assert.That(restored.Position, Is.EqualTo(new Vector2(10, 20)));
    }

    [Test]
    public void ARegionsGroupsRoundTrip()
    {
        var source = new LevelDocument { Name = "Reads" };
        source.ScreenReads.Add(new ScreenReadDocument { Width = 300, Height = 200, Layer = -2, Z = 7, Groups = new List<int> { 2, 5 } });

        ScreenReadDocument read = XmfBinary.Read(XmfBinary.Write(source)).ScreenReads.Single();
        Assert.That(read.Groups, Is.EqualTo(new[] { 2, 5 }));
        Assert.That((read.Width, read.Layer, read.Z), Is.EqualTo((300f, -2, 7)));
    }

    [Test]
    public void ARegionKeepsItsGroupsThroughTheMapperAndUndo()
    {
        var store = new EntityStore();
        Entity region = EditorItems.Create(store, new ScreenReadRegion(new Vector2(10, 20), new Vector2(200, 100)));
        region.AddComponent(new ItemGroups(ItemGroups.Bit(3)));
        var history = new EditorHistory(store);
        EditorSnapshot before = history.Capture();

        region.GetComponent<ScreenReadRegion>().Position = new Vector2(90, 120);
        history.Record(before, history.Capture());
        history.Undo();

        Entity restored = store.Query<ScreenReadRegion>().ToEntityList().Single();
        Assert.That(restored.GetComponent<ItemGroups>().Has(3), Is.True);
    }

    [Test]
    public void ANewRegionFitsTheView()
    {
        Assert.That(ScreenReadRegion.StartSize(new Vector2(3000, 3000)), Is.EqualTo(new Vector2(600, 400)), "the default when there's room");
        Assert.That(ScreenReadRegion.StartSize(new Vector2(900, 600)), Is.EqualTo(new Vector2(300, 200)), "a third of a close-up view");
    }

    private static Entity Region(EntityStore store, Vector2 position, Vector2 size, int layer, int z)
    {
        Entity region = EditorItems.Create(store, new ScreenReadRegion(position, size));
        ItemLayer.Put(region, new ItemLayer(layer, z));
        return region;
    }

    private sealed class RecordingItems : IShaderCanvasItems
    {
        public List<Rid> Created { get; } = new();
        public List<(Rid Item, Rect2 Rect)> Copies { get; } = new();

        public Rid CreateItem(Rid parent)
        {
            Rid item = TestRid.Make((ulong)(100 + Created.Count));
            Created.Add(item);
            return item;
        }

        public void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend) { }

        public void CopyRectangle(Rid item, Rect2 pixelRect) => Copies.Add((item, pixelRect));

        public void DisableCopy(Rid item) { }
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<(string Op, Rid Item)> Calls { get; } = new();

        public List<(string Op, Rid Item)> Draws => Calls.Where(call => call.Op != "Clear").ToList();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) => Calls.Add(("Clear", canvas));

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Calls.Add(("Rect", canvas));

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) => Calls.Add(("Circle", canvas));

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) => Calls.Add(("Polygon", canvas));

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) => Calls.Add(("Line", canvas));

        public void DrawPolyline(Rid canvas, ReadOnlySpan<Vector2> points, Color color, float width, bool antialiased = true) =>
            Calls.Add(("Polyline", canvas));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("Text", canvas));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("TextLeft", canvas));

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("TextRight", canvas));
    }
}
