using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xanadu.Systems.Canvas;

namespace Xanadu.Tests.UI.Rendering;

[TestFixture]
public sealed class PolylineTests
{
    [Test]
    public void CircleOutlineUsesOneClosedPolyline()
    {
        var canvas = new RecordingCanvas();

        ((IRectCanvas)canvas).DrawCircleOutline(
            default,
            new Vector2(100, 100),
            20,
            Colors.White,
            2,
            segments: 12);

        Assert.That(canvas.Polylines, Has.Count.EqualTo(1));
        Assert.That(canvas.Polylines[0].Points, Has.Length.EqualTo(13));
        Assert.That(canvas.Polylines[0].Points[0],
            Is.EqualTo(canvas.Polylines[0].Points[^1]));
        Assert.That(canvas.Lines, Is.Zero);
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<PolylineCall> Polylines { get; } = new();
        public int Lines { get; private set; }

        public void SetVisible(Handle canvas, bool visible) { }
        public void SetTransform(Handle canvas, Transform2D transform) { }
        public void Clear(Handle canvas) { }
        public void DrawRect(Handle canvas, Rect2 bounds, Color color) { }

        public void DrawPolyline(
            Handle canvas,
            ReadOnlySpan<Vector2> points,
            Color color,
            float width,
            bool antialiased = true) =>
            Polylines.Add(new PolylineCall(points.ToArray()));

        public void DrawLine(
            Handle canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines++;

        public void DrawText(Handle canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct PolylineCall(Vector2[] Points);
    }
}
