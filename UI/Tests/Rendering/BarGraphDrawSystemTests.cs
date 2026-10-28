using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Rendering;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.UI.Rendering;

[TestFixture]
public sealed class BarGraphDrawSystemTests : EcsSystemTest
{
    private RecordingCanvas _canvas = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingCanvas();
        Root.Add(new BarGraphDrawSystem(_canvas));
    }

    [Test]
    public void EachBarHangsFromTheLineAsTallAsItsValue()
    {
        Entity graph = AddGraph([new(0, 100), new(100, 100)], count: 3);
        graph.GetComponent<UiBarGraph>().Values = [0f, 0.5f, 1f];

        Frame();

        // Two rects per bar: the body, then the cap at its free end.
        Assert.That(_canvas.Rects, Has.Count.EqualTo(6));
        Rect2 empty = _canvas.Rects[0];
        Rect2 half = _canvas.Rects[2];
        Rect2 full = _canvas.Rects[4];
        Assert.That(full.Position.Y, Is.EqualTo(100));
        Assert.That(full.Size.Y, Is.EqualTo(50));
        Assert.That(half.Size.Y, Is.GreaterThan(empty.Size.Y));
        Assert.That(half.Size.Y, Is.LessThan(full.Size.Y));
        Assert.That(empty.Size.Y, Is.GreaterThan(0));
    }

    [Test]
    public void BarsFollowASlopedLine()
    {
        Entity graph = AddGraph([new(0, 100), new(50, 100), new(100, 40)], count: 5);
        graph.GetComponent<UiBarGraph>().Values = new float[5];

        Frame();

        float first = _canvas.Rects[0].Position.Y;
        float last = _canvas.Rects[^2].Position.Y;
        Assert.That(first, Is.EqualTo(100));
        Assert.That(last, Is.LessThan(first));
    }

    [Test]
    public void BarsOnASlopeHaveTheirTopsTiltedToTheLine()
    {
        // A 45 degree rise, so a 10 wide bar's top edge must climb 10.
        Entity graph = AddGraph([new(0, 100), new(100, 0)], count: 2, barWidth: 10);
        graph.GetComponent<UiBarGraph>().Values = [1f, 1f];

        Frame();

        Assert.That(_canvas.Polygons, Has.Count.EqualTo(2));
        Vector2[] bar = _canvas.Polygons[0];
        Assert.That(bar[0].Y - bar[1].Y, Is.EqualTo(bar[1].X - bar[0].X).Within(0.01f));
        Assert.That(bar[2].Y, Is.EqualTo(bar[3].Y));
        Assert.That(bar[2].Y, Is.GreaterThan(bar[0].Y));
    }

    [Test]
    public void OutOfRangeValuesAreClamped()
    {
        Entity graph = AddGraph([new(0, 0), new(40, 0)], count: 2);
        graph.GetComponent<UiBarGraph>().Values = [-3f, 7f];

        Frame();

        Assert.That(_canvas.Rects[2].Size.Y, Is.EqualTo(50));
        Assert.That(_canvas.Rects[0].Size.Y, Is.GreaterThan(0).And.LessThan(5));
    }

    private Entity AddGraph(Vector2[] anchor, int count, float barWidth = 4) =>
        Store.CreateEntity(
            new UiBarGraph(anchor, count, barWidth, 50, Colors.White, Colors.Red),
            new Drawable(0),
            new VisualRid(new Rid()));

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();

        public List<Vector2[]> Polygons { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas)
        {
            Rects.Clear();
            Polygons.Clear();
        }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(bounds);

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
            Polygons.Add(points.ToArray());

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
