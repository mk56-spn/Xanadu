using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayfieldDrawListTests
{
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(1920, 1080));
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly Playhead Head = new(1000, 120, 0, true);

    private static Entity Block(EntityStore store, int layer, int z) =>
        store.CreateEntity(new Block(new Vector2(10, 10), new Vector2(20, 20)), new ItemLayer(layer, z));

    private static Entity Note(EntityStore store, int layer, int z, NoteKind kind = NoteKind.Hit) =>
        store.CreateEntity(new Note(1000, new Vector2(50, 50), kind: kind), new ItemLayer(layer, z));

    private static List<PlayfieldDrawEntry> Gather(EntityStore store, bool startPoints = false)
    {
        var list = new List<PlayfieldDrawEntry>();
        PlayfieldDrawList.Gather(store, list, startPoints);
        return list;
    }

    [Test]
    public void ItemsSortByLayerThenZWhateverTheirKind()
    {
        var store = new EntityStore();
        Entity front = Block(store, 3, 0);
        Entity noteHigh = Note(store, -1, 5);
        Entity back = Block(store, -2, 9);
        Entity noteLow = Note(store, -1, 1);

        List<Entity> order = Gather(store)
            .Where(e => e.Kind != PlayfieldDrawKind.NoteGuides)
            .Select(e => e.Entity)
            .ToList();

        Assert.That(order, Is.EqualTo(new[] { back, noteLow, noteHigh, front }));
    }

    [Test]
    public void TiesOnZFallBackToKindThenListOrder()
    {
        var store = new EntityStore();
        Entity note = Note(store, -1, 0);
        Entity second = Block(store, -1, 0);
        Entity first = Block(store, -1, 0);
        Entity polygon = store.CreateEntity(new LevelPolygon(), new ItemLayer(-1, 0));

        List<Entity> order = Gather(store)
            .Where(e => e.Kind != PlayfieldDrawKind.NoteGuides)
            .Select(e => e.Entity)
            .ToList();

        // Polygons, then blocks (made first, drawn first), then notes: the old stacking.
        Assert.That(order, Is.EqualTo(new[] { polygon, second, first, note }));
    }

    [Test]
    public void AnItemWithoutALayerTakesItsKindsDefault()
    {
        var store = new EntityStore();
        Entity block = store.CreateEntity(new Block(new Vector2(10, 10), new Vector2(20, 20)));

        PlayfieldDrawEntry entry = Gather(store).Single();

        Assert.That(entry.Entity, Is.EqualTo(block));
        Assert.That(entry.Layer, Is.EqualTo(ItemLayer.Terrain));
    }

    [Test]
    public void GuidesGoJustUnderTheBackmostNote()
    {
        var store = new EntityStore();
        Note(store, 4, 0);
        Entity backmost = Note(store, -1, 2);
        Entity block = Block(store, -1, 2);

        List<PlayfieldDrawEntry> list = Gather(store);
        int guides = list.FindIndex(e => e.Kind == PlayfieldDrawKind.NoteGuides);

        Assert.That(list.Count(e => e.Kind == PlayfieldDrawKind.NoteGuides), Is.EqualTo(1));
        Assert.That(list[guides - 1].Entity, Is.EqualTo(block), "over terrain tied with the note");
        Assert.That(list[guides + 1].Entity, Is.EqualTo(backmost));
        Assert.That(list[guides].Layer, Is.EqualTo(-1));
    }

    [Test]
    public void StartPointsDrawOnlyInTheEditor()
    {
        var store = new EntityStore();
        store.CreateEntity(new StartPoint("start", 0, Vector2.Zero, default, default, 0), new ItemLayer(-1, 0));

        Assert.That(Gather(store), Is.Empty);
        Assert.That(Gather(store, startPoints: true).Single().Kind, Is.EqualTo(PlayfieldDrawKind.StartPoint));
    }

    [Test]
    public void AnEffectDrawsJustAboveTheItemItCameFrom()
    {
        var store = new EntityStore();
        Entity source = Note(store, -1, 3);
        Entity above = Note(store, -1, 4);
        Entity pop = store.CreateEntity(
            new NotePop(source.GetComponent<Note>(), Vector2.Zero, Colors.White, 10f),
            EffectLayer.From(source));
        source.DeleteEntity();

        List<Entity> order = Gather(store)
            .Where(e => e.Kind != PlayfieldDrawKind.NoteGuides)
            .Select(e => e.Entity)
            .ToList();

        Assert.That(order, Is.EqualTo(new[] { pop, above }));
    }

    [Test]
    public void AnEffectWithoutASourceGoesInFrontOfTheGameplayLayer()
    {
        var store = new EntityStore();
        Note(store, ItemLayer.Gameplay, 1_000);
        Entity trail = store.CreateEntity(new HitTrail(Vector2.Zero, Colors.White, "M"));
        Block(store, 1, 0);

        List<PlayfieldDrawEntry> list = Gather(store);
        int at = list.FindIndex(e => e.Entity == trail);

        Assert.That(list[at].Layer, Is.EqualTo(ItemLayer.Gameplay));
        Assert.That(list[at - 1].Kind, Is.EqualTo(PlayfieldDrawKind.Note));
        Assert.That(list[at + 1].Layer, Is.EqualTo(1));
    }

    [Test]
    public void TheWedgeDrawsBetweenTheLayersBehindAndInFront()
    {
        var store = new EntityStore();
        Block(store, 2, 0);
        Block(store, -3, 0);
        Block(store, -1, 0);
        Block(store, 1, 0);
        var canvas = new LayerLog();

        PlayfieldRenderer.DrawLayers(
            canvas,
            default,
            store,
            new PlayfieldItemFrame(View, Field, Head, 0f, Editor: false),
            () => canvas.Log.Add("wedge"));

        Assert.That(canvas.Log, Is.EqualTo(new[]
        {
            "layer -3", "rect", "layer -1", "rect", "layer 0", "wedge", "layer 1", "rect", "layer 2", "rect", "end",
        }));
    }

    [Test]
    public void TheWedgeStillDrawsWithNoItemsOrOnlyItemsBehindIt()
    {
        var store = new EntityStore();
        var canvas = new LayerLog();
        var frame = new PlayfieldItemFrame(View, Field, Head, 0f, Editor: false);

        PlayfieldRenderer.DrawLayers(canvas, default, store, frame, () => canvas.Log.Add("wedge"));
        Assert.That(canvas.Log, Is.EqualTo(new[] { "layer 0", "wedge", "end" }));

        canvas.Log.Clear();
        Block(store, -2, 0);
        PlayfieldRenderer.DrawLayers(canvas, default, store, frame, () => canvas.Log.Add("wedge"));
        Assert.That(canvas.Log, Is.EqualTo(new[] { "layer -2", "rect", "layer 0", "wedge", "end" }));
    }

    /// <summary>Logs layers and the first draw in each, so the order reads at a glance.</summary>
    private sealed class LayerLog : IRectCanvas
    {
        public List<string> Log { get; } = new();

        public void BeginLayer(int layer, Handle? target = null) => Log.Add("layer " + layer);

        public void EndLayers() => Log.Add("end");

        public void SetVisible(Handle canvas, bool visible) { }

        public void SetTransform(Handle canvas, Transform2D transform) { }

        public void Clear(Handle canvas) { }

        public void DrawRect(Handle canvas, Rect2 bounds, Color color)
        {
            if (Log.Count == 0 || Log[^1] != "rect")
                Log.Add("rect");
        }

        public void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Handle canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color) { }
    }
}
