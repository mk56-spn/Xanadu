using System;
using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Systems.Canvas;

namespace Writer.Tests.UI.Rendering;

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

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawPolyline(
            Rid canvas,
            ReadOnlySpan<Vector2> points,
            Color color,
            float width,
            bool antialiased = true) =>
            Polylines.Add(new PolylineCall(points.ToArray()));

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines++;

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct PolylineCall(Vector2[] Points);
    }
}
