using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Rendering;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests.Rendering;

[TestFixture]
public sealed class ShapeDrawSystemTests
{
    [Test]
    public void ShapeDrawSystemFillsAndClosesTheOutline()
    {
        var store = new EntityStore();
        var canvas = new RecordingCanvas();
        Vector2[] points = [new(0, 0), new(10, 0), new(10, 10)];
        store.CreateEntity(
            new Rectangle(new Rect2(0, 0, 10, 10)),
            new Drawable(0),
            new VisualItem(new Handle()),
            new UiShape(points, Colors.Black, Colors.White, 3),
            Tags.Get<Dirty>());

        var root = new SystemRoot(store);
        root.Add(new ShapeDrawSystem(canvas));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.PolygonPoints, Is.EqualTo(3));
        Assert.That(canvas.PolylinePoints, Is.EqualTo(4));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public Transform2D Transform;
        public int PolygonPoints;
        public int PolylinePoints;

        public void SetVisible(Handle canvas, bool visible)
        {
        }

        public void SetTransform(Handle canvas, Transform2D transform) => Transform = transform;

        public void Clear(Handle canvas)
        {
        }

        public void DrawRect(Handle canvas, Rect2 bounds, Color color)
        {
        }

        public void DrawPolygon(Handle canvas, ReadOnlySpan<Vector2> points, Color color) =>
            PolygonPoints = points.Length;

        public void DrawPolyline(
            Handle canvas,
            ReadOnlySpan<Vector2> points,
            Color color,
            float width,
            bool antialiased = true) =>
            PolylinePoints = points.Length;

        public void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width)
        {
        }

        public void DrawText(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }

        public void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }

        public void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color)
        {
        }
    }
}
