using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Systems.Canvas;

namespace Writer.Tests.UI.Rendering;

[TestFixture]
public sealed class CanvasGridTests
{
    [Test]
    public void TenDivisionsCentredOnTheFieldKeepBothEdgesAndOneAxisPair()
    {
        var canvas = new LineCanvas();
        var field = new Rect2(10, 20, 300, 200);

        CanvasGrid.Draw(canvas, default, field, field.Size / 10, field.Position + field.Size / 2);

        // Eleven vertical and eleven horizontal lines, edges included.
        Assert.That(canvas.Lines, Has.Count.EqualTo(22));
        Assert.That(canvas.Lines.FindAll(line => line.Color == CanvasGrid.AxisColor),
            Has.Count.EqualTo(2));
        Assert.That(canvas.Lines.Exists(line => line.From.X == 310f), Is.True);
        Assert.That(canvas.Lines.Exists(line => line.From.Y == 220f), Is.True);
    }

    [Test]
    public void LinesAreAlignedToTheAxisAndStayInsideTheBounds()
    {
        var canvas = new LineCanvas();

        CanvasGrid.Draw(
            canvas,
            default,
            new Rect2(0, 0, 100, 50),
            new Vector2(20, 20),
            new Vector2(35, 25));

        List<float> xs = canvas.Lines.FindAll(line => line.From.X == line.To.X)
            .ConvertAll(line => line.From.X);
        List<float> ys = canvas.Lines.FindAll(line => line.From.Y == line.To.Y)
            .ConvertAll(line => line.From.Y);
        Assert.That(xs, Is.EqualTo(new[] { 15f, 35f, 55f, 75f, 95f }));
        Assert.That(ys, Is.EqualTo(new[] { 5f, 25f, 45f }));
    }

    [Test]
    public void AnAxisOutsideTheBoundsDrawsNoAxisLines()
    {
        var canvas = new LineCanvas();

        CanvasGrid.Draw(
            canvas,
            default,
            new Rect2(0, 0, 100, 100),
            new Vector2(25, 25),
            new Vector2(-50, -50));

        Assert.That(canvas.Lines, Is.Not.Empty);
        Assert.That(canvas.Lines.TrueForAll(line => line.Color == CanvasGrid.LineColor), Is.True);
    }

    [Test]
    public void ANonPositiveCellDrawsNothing()
    {
        var canvas = new LineCanvas();

        CanvasGrid.Draw(canvas, default, new Rect2(0, 0, 100, 100), Vector2.Zero, Vector2.Zero);

        Assert.That(canvas.Lines, Is.Empty);
    }

    private sealed class LineCanvas : IRectCanvas
    {
        public List<LineCall> Lines { get; } = new();

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) =>
            Lines.Add(new LineCall(from, to, color));

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct LineCall(Vector2 From, Vector2 To, Color Color);
    }
}
