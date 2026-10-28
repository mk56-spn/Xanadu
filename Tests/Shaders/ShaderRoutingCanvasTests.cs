using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NUnit.Framework;
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
    public void ACommonShaderGetsItsOwnItemAndTakesPriorityOverASlot()
    {
        const string rainbow = "res://Assets/Shaders/Items/rainbow.gdshader";

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

        public Rid CreateItem(Rid parent)
        {
            Assert.That(parent, Is.EqualTo(Root));
            Rid item = TestRid.Make((ulong)(100 + Created.Count));
            Created.Add(item);
            return item;
        }

        public void SetMaterial(Rid item, int slot, string commonPath)
        {
            Materials.Add(slot);
            CommonMaterials.Add(commonPath);
        }
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<(string Op, Rid Item)> Calls { get; } = new();

        public List<(string Op, Rid Item)> Draws => Calls.Where(c => c.Op != "Clear").ToList();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

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
