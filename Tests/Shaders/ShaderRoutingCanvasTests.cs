using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Shaders;

[TestFixture]
public sealed class ShaderRoutingCanvasTests
{
    private static readonly Rid Root = TestRid.Make(1);

    private RecordingCanvas _inner = null!;
    private FakeItems _items = null!;
    private ShaderRoutingCanvas _canvas = null!;

    [SetUp]
    public void SetUp()
    {
        _inner = new RecordingCanvas();
        _items = new FakeItems();
        _canvas = new ShaderRoutingCanvas(_inner, Root, _items);
        _canvas.Clear(Root);
        _inner.Calls.Clear();
    }

    [Test]
    public void WithoutShadersEverythingGoesToTheRootItem()
    {
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.DrawCircle(Root, Vector2.Zero, 4, Colors.Red);

        Assert.That(_inner.Draws.Select(d => d.Item), Is.All.EqualTo(Root));
        Assert.That(_items.Created, Is.Empty);
    }

    [Test]
    public void ObjectsKeepTheirDrawOrderAroundShadedOnes()
    {
        Draw(slot: 0);
        Draw(slot: 1);
        Draw(slot: 0);
        Draw(slot: 1);
        Draw(slot: 1);

        // Plain, shaded, then plain again must land in items that stack in that order.
        List<Rid> order = _inner.Draws.Select(d => d.Item).ToList();
        Assert.That(order[0], Is.EqualTo(Root));
        Assert.That(order[1], Is.EqualTo(_items.Created[0]));
        Assert.That(order[2], Is.EqualTo(_items.Created[1]));
        Assert.That(order[3], Is.EqualTo(_items.Created[2]));
        Assert.That(order[4], Is.EqualTo(order[3]), "the same slot twice in a row shares an item");
        Assert.That(_items.Created, Has.Count.EqualTo(3));
        Assert.That(_items.Materials, Is.EqualTo(new[] { 1, 0, 1 }));
    }

    [Test]
    public void TheSlotStaysSelectedUntilItChanges()
    {
        _canvas.SelectShader(2);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.DrawLine(Root, Vector2.Zero, Vector2.One, Colors.Red, 1);

        Assert.That(_items.Created, Has.Count.EqualTo(1));
        Assert.That(_inner.Draws.Select(d => d.Item).Distinct().Single(), Is.EqualTo(_items.Created[0]));
        Assert.That(_items.Materials, Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void DifferentSlotsGetTheirOwnItems()
    {
        Draw(slot: 1);
        Draw(slot: 2);

        Assert.That(_items.Materials, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(_inner.Draws[0].Item, Is.Not.EqualTo(_inner.Draws[1].Item));
    }

    [Test]
    public void ADifferentBlendGetsItsOwnRun()
    {
        _canvas.SelectShader(1, BlendMode.Mix);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(1, BlendMode.Add);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(1, BlendMode.Add);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(2), "the same slot twice in a row shares a run");
        Assert.That(_items.Blends, Is.EqualTo(new[] { BlendMode.Mix, BlendMode.Add }));
        Assert.That(_inner.Draws[2].Item, Is.EqualTo(_inner.Draws[1].Item));
    }

    [Test]
    public void PlainDrawsWithABlendGetTheirOwnItem()
    {
        _canvas.SelectShader(0, BlendMode.Mul);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(0);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(2), "the plain draw after it needs a child of its own");
        Assert.That(_items.Blends, Is.EqualTo(new[] { BlendMode.Mul, BlendMode.Mix }));
        Assert.That(_inner.Draws[0].Item, Is.EqualTo(_items.Created[0]));
        Assert.That(_inner.Draws[1].Item, Is.EqualTo(_items.Created[1]), "selecting again resets the blend too");
    }

    [Test]
    public void SelectingAShaderAgainResetsTheBlendToMix()
    {
        _canvas.SelectShader(2, BlendMode.Add);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(2);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Blends, Is.EqualTo(new[] { BlendMode.Add, BlendMode.Mix }));
        Assert.That(_inner.Draws[0].Item, Is.Not.EqualTo(_inner.Draws[1].Item));
    }

    [Test]
    public void DifferentSourceGroupsGetTheirOwnRunsAndValues()
    {
        _canvas.SelectShader(2, BlendMode.Mix, 0);
        _canvas.SetShaderValue("tint", new Vector4(1, 0, 0, 1), 4);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(2, BlendMode.Mix, 1);
        _canvas.SetShaderValue("tint", new Vector4(0, 1, 0, 1), 4);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(2), "each group's runs are their own");
        Assert.That(
            _items.InstanceValues[0],
            Is.EqualTo((_items.Created[0], "tint", new Vector4(1, 0, 0, 1), 4)));
        Assert.That(
            _items.InstanceValues[^1],
            Is.EqualTo((_items.Created[1], "tint", new Vector4(0, 1, 0, 1), 4)));
    }

    [Test]
    public void TheSameSourceGroupSharesOneRun()
    {
        _canvas.SelectShader(2, BlendMode.Add, 3);
        _canvas.SetShaderValue("speed", new Vector4(0.5f, 0, 0, 0), 1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectShader(2, BlendMode.Add, 3);
        _canvas.SetShaderValue("speed", new Vector4(0.75f, 0, 0, 0), 1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(1), "one material and one run for the pair");
        Assert.That(_items.InstanceValues, Has.Count.EqualTo(2), "each selection writes the values");
        Assert.That(_items.InstanceValues[^1].Value.X, Is.EqualTo(0.75f));
        Assert.That(_items.InstanceValues[^1].Item, Is.EqualTo(_items.Created[0]));
    }

    [Test]
    public void ACommonShaderGetsItsOwnItemAndTakesPriorityOverASlot()
    {
        const string rainbow = "res://Assets/Shaders/Items/rainbow.slang";

        _canvas.SelectShader(1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.SelectCommonShader(rainbow);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(2));
        Assert.That(_items.Materials, Is.EqualTo(new[] { 1, 0 }));
        Assert.That(_items.CommonMaterials, Is.EqualTo(new[] { "", rainbow }));

        // Selecting a slot again drops the common shader, starting a fresh run.
        _canvas.SelectShader(1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Materials, Is.EqualTo(new[] { 1, 0, 1 }));
        Assert.That(_items.CommonMaterials, Is.EqualTo(new[] { "", rainbow, "" }));
    }

    [Test]
    public void ClearingStartsTheNextFrameFromTheRootAndReusesItems()
    {
        Draw(slot: 1);
        Draw(slot: 0);
        List<Rid> firstFrame = _items.Created.ToList();

        _inner.Calls.Clear();
        _canvas.Clear(Root);

        Assert.That(
            _inner.Calls.Where(c => c.Op == "Clear").Select(c => c.Item),
            Is.EquivalentTo(firstFrame.Prepend(Root)));

        _canvas.SelectShader(0);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        Draw(slot: 1);
        Draw(slot: 0);

        Assert.That(_items.Created, Is.EqualTo(firstFrame), "no new items for the same shape of frame");
        Assert.That(_inner.Draws[0].Item, Is.EqualTo(Root));
        Assert.That(_inner.Draws[1].Item, Is.EqualTo(firstFrame[0]));
        Assert.That(_inner.Draws[2].Item, Is.EqualTo(firstFrame[1]));
    }

    [Test]
    public void OtherCanvasItemsPassStraightThrough()
    {
        Rid other = TestRid.Make(99);
        _canvas.SelectShader(3);

        _canvas.DrawRect(other, new Rect2(), Colors.Red);

        Assert.That(_inner.Draws.Single().Item, Is.EqualTo(other));
        Assert.That(_items.Created, Is.Empty);
    }

    [Test]
    public void EveryKindOfDrawIsRouted()
    {
        _canvas.SelectShader(1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        _canvas.DrawCircle(Root, Vector2.Zero, 3, Colors.Red);
        _canvas.DrawCircleOutline(Root, Vector2.Zero, 3, Colors.Red, 1);
        _canvas.DrawLine(Root, Vector2.Zero, Vector2.One, Colors.Red, 1);
        _canvas.DrawPolygon(Root, new Vector2[] { new(0, 0), new(1, 0), new(0, 1) }, Colors.Red);
        _canvas.DrawPolyline(Root, new Vector2[] { new(0, 0), new(1, 0) }, Colors.Red, 1);
        _canvas.DrawText(Root, new Rect2(), "x", Colors.Red);
        _canvas.DrawTextStyled(Root, new Rect2(), "x", Colors.Red, 12);
        _canvas.DrawTextLeft(Root, new Rect2(), "x", Colors.Red);
        _canvas.DrawTextRight(Root, new Rect2(), "x", Colors.Red);

        Assert.That(_inner.Draws, Has.Count.GreaterThanOrEqualTo(9));
        Assert.That(_inner.Draws.Select(d => d.Item), Is.All.EqualTo(_items.Created[0]));
    }

    [Test]
    public void ALayerGetsAContainerUnderTheRootAndPlainDrawsGoIntoIt()
    {
        _canvas.BeginLayer(-1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_canvas.TryGetLayerItem(-1, out Rid layer), Is.True);
        Assert.That(_items.Parents[layer], Is.EqualTo(Root));
        Assert.That(_inner.Draws.Single().Item, Is.EqualTo(layer));
    }

    [Test]
    public void ShadedRunsInALayerAreChildrenOfItsContainer()
    {
        _canvas.BeginLayer(2);
        Draw(slot: 0);
        Draw(slot: 3);
        Draw(slot: 0);

        _canvas.TryGetLayerItem(2, out Rid layer);
        List<Rid> order = _inner.Draws.Select(d => d.Item).ToList();
        Assert.That(order[0], Is.EqualTo(layer));
        Assert.That(_items.Parents[order[1]], Is.EqualTo(layer));
        Assert.That(_items.Parents[order[2]], Is.EqualTo(layer));
        Assert.That(order[2], Is.Not.EqualTo(order[1]));
    }

    [Test]
    public void TheRootsChildrenStackInTheOrderTheFrameUsedThem()
    {
        Draw(slot: 1);                  // a root run, under the layers
        _canvas.BeginLayer(-1);
        Draw(slot: 0);
        _canvas.BeginLayer(1);
        Draw(slot: 0);
        _canvas.EndLayers();
        Draw(slot: 1);                  // same slot as the first run, but over the layers

        _canvas.TryGetLayerItem(-1, out Rid back);
        _canvas.TryGetLayerItem(1, out Rid front);
        Rid under = _inner.Draws[0].Item;
        Rid over = _inner.Draws[^1].Item;

        Assert.That(over, Is.Not.EqualTo(under), "the root's earlier run sits under the layers");
        Assert.That(_items.Parents[over], Is.EqualTo(Root));
        int[] indices = { _items.DrawIndex[under], _items.DrawIndex[back], _items.DrawIndex[front], _items.DrawIndex[over] };
        Assert.That(indices, Is.Ordered.Ascending);
        Assert.That(indices.Distinct().Count(), Is.EqualTo(4));
    }

    [Test]
    public void ALayerMadeLateStillStacksWhereItWasBegun()
    {
        _canvas.BeginLayer(1);
        Draw(slot: 0);
        _canvas.Clear(Root);

        // Next frame a layer behind it appears; it was made after, but is begun first.
        _canvas.BeginLayer(-1);
        Draw(slot: 0);
        _canvas.BeginLayer(1);
        Draw(slot: 0);

        _canvas.TryGetLayerItem(-1, out Rid back);
        _canvas.TryGetLayerItem(1, out Rid front);
        Assert.That(_items.Created.IndexOf(back), Is.GreaterThan(_items.Created.IndexOf(front)));
        Assert.That(_items.DrawIndex[back], Is.LessThan(_items.DrawIndex[front]));
    }

    [Test]
    public void ClearingEmptiesLayersAndKeepsTheirContainers()
    {
        _canvas.BeginLayer(-1);
        Draw(slot: 2);
        _canvas.TryGetLayerItem(-1, out Rid layer);
        Rid run = _inner.Draws.Single().Item;
        int made = _items.Created.Count;

        _inner.Calls.Clear();
        _canvas.Clear(Root);

        List<Rid> cleared = _inner.Calls.Where(c => c.Op == "Clear").Select(c => c.Item).ToList();
        Assert.That(cleared, Does.Contain(layer));
        Assert.That(cleared, Does.Contain(run));

        _canvas.BeginLayer(-1);
        Draw(slot: 2);
        Assert.That(_items.Created, Has.Count.EqualTo(made), "the container and its run are reused");
        Assert.That(_inner.Draws.Single().Item, Is.EqualTo(run));
    }

    [Test]
    public void ARectCopyGetsItsOwnItemAndLaterDrawsStartAfterIt()
    {
        _canvas.SelectShader(1);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
        Rid before = _items.Created.Single();

        _canvas.CopyScreen(new Rect2(10, 20, 30, 40));

        Assert.That(_items.Copies, Has.Count.EqualTo(1));
        Rid copy = _items.Copies[0].Item;
        Assert.That(_items.Copies[0].Rect, Is.EqualTo(new Rect2(10, 20, 30, 40)));
        Assert.That(copy, Is.Not.EqualTo(before), "the copy is its own canvas item");

        _canvas.DrawRect(Root, new Rect2(), Colors.Red);

        Assert.That(_items.Created, Has.Count.EqualTo(3), "before, the copy, then a fresh run");
        Assert.That(_items.Created[1], Is.EqualTo(copy));
        Assert.That(_items.Created[2], Is.Not.EqualTo(before), "the draw after the copy starts a new item");
        Assert.That(_inner.Draws.Last().Item, Is.EqualTo(_items.Created[2]));
    }

    [Test]
    public void ARectCopyIsScaledToRenderTargetPixelsWhenTheCanvasKnowsHow()
    {
        var scaled = new ShaderRoutingCanvas(_inner, Root, _items, rect => rect.Grow(1));
        scaled.Clear(Root);

        scaled.CopyScreen(new Rect2(10, 20, 30, 40));

        Assert.That(_items.Copies.Single().Rect, Is.EqualTo(new Rect2(9, 19, 32, 42)));
    }

    [Test]
    public void AnUnusedRectCopyStopsCopyingNextFrame()
    {
        _canvas.CopyScreen(new Rect2(10, 20, 30, 40));
        Rid copy = _items.Copies.Single().Item;

        _canvas.Clear(Root);

        Assert.That(_items.Disabled, Does.Contain(copy));
    }

    private void Draw(int slot)
    {
        _canvas.SelectShader(slot);
        _canvas.DrawRect(Root, new Rect2(), Colors.Red);
    }

    private sealed class FakeItems : IShaderCanvasItems
    {
        public List<Rid> Created { get; } = new();
        public List<int> Materials { get; } = new();
        public List<string> CommonMaterials { get; } = new();
        public List<BlendMode> Blends { get; } = new();
        public List<(Rid Item, string Param, Vector4 Value, int Size)> InstanceValues { get; } = new();
        public List<(Rid Item, Rect2 Rect)> Copies { get; } = new();
        public List<Rid> Disabled { get; } = new();

        public void SetInstanceUniform(Rid item, string param, Vector4 value, int size) =>
            InstanceValues.Add((item, param, value, size));

        public void CopyRectangle(Rid item, Rect2 pixelRect) => Copies.Add((item, pixelRect));

        public void DisableCopy(Rid item) => Disabled.Add(item);

        public Dictionary<Rid, Rid> Parents { get; } = new();
        public Dictionary<Rid, int> DrawIndex { get; } = new();

        public Rid CreateItem(Rid parent)
        {
            Rid item = TestRid.Make((ulong)(100 + Created.Count));
            Created.Add(item);
            Parents[item] = parent;
            return item;
        }

        public void SetDrawIndex(Rid item, int index) => DrawIndex[item] = index;

        public void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend)
        {
            Materials.Add(slot);
            CommonMaterials.Add(commonPath);
            Blends.Add(blend);
        }
    }

    [Test]
    public void ASelfContainedLayersSurfaceMovesWithTheRoot()
    {
        Rid surface = TestRid.Make(500);
        var shift = new Transform2D(0f, new Vector2(-1400, -3700));

        _canvas.SetTransform(Root, shift);
        _canvas.BeginLayer(5, surface);

        Assert.That(_inner.Transforms[surface], Is.EqualTo(shift), "a surface begun after the shift takes it");

        var moved = new Transform2D(0f, new Vector2(-10, -20));
        _canvas.SetTransform(Root, moved);
        Assert.That(_inner.Transforms[surface], Is.EqualTo(moved), "and follows the root when it moves");
    }

    [Test]
    public void ToLocalUndoesTheRootsTransform()
    {
        _canvas.SetTransform(Root, new Transform2D(0f, new Vector2(-1400, -3700)));

        Rect2 local = _canvas.ToLocal(new Rect2(0, 0, 1920, 1080));

        Assert.That(local, Is.EqualTo(new Rect2(1400, 3700, 1920, 1080)), "the composite covers the screen once the root moves it");
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<(string Op, Rid Item)> Calls { get; } = new();

        public List<(string Op, Rid Item)> Draws => Calls.Where(c => c.Op != "Clear").ToList();

        public void SetVisible(Rid canvas, bool visible) { }

        public Dictionary<Rid, Transform2D> Transforms { get; } = new();

        public void SetTransform(Rid canvas, Transform2D transform) => Transforms[canvas] = transform;

        public void Clear(Rid canvas) => Calls.Add(("Clear", canvas));

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Calls.Add(("Rect", canvas));

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) => Calls.Add(("Circle", canvas));

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) => Calls.Add(("Polygon", canvas));

        public void DrawPolyline(Rid canvas, ReadOnlySpan<Vector2> points, Color color, float width, bool antialiased = true) =>
            Calls.Add(("Polyline", canvas));

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) => Calls.Add(("Line", canvas));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("Text", canvas));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("TextLeft", canvas));

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) => Calls.Add(("TextRight", canvas));

        public void DrawTextStyled(Rid canvas, Rect2 bounds, string text, Color color, int fontSize) =>
            Calls.Add(("TextStyled", canvas));
    }
}
