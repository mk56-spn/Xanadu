using System.Collections.Generic;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Tests.UI.Rendering;

[TestFixture]
public sealed class CanvasMarqueeTests
{
    [Test]
    public void AnActiveBoxIsFilledAndOutlinedInsideItsBounds()
    {
        var canvas = new RectCanvas();
        var box = new UiMarqueeBox { Start = new Vector2(100, 80), Current = new Vector2(40, 120), Active = true };

        CanvasMarquee.Draw(canvas, default, box);

        var bounds = new Rect2(40, 80, 60, 40);
        Assert.That(canvas.Rects.Exists(call => call.Bounds == bounds && call.Color == CanvasMarquee.FillColor), Is.True);
        List<RectCall> border = canvas.Rects.FindAll(call => call.Color == CanvasMarquee.BorderColor);
        Assert.That(border, Has.Count.EqualTo(4));
        foreach (RectCall strip in border)
            Assert.That(bounds.Encloses(strip.Bounds), Is.True);
    }

    [Test]
    public void AnInactiveBoxDrawsNothing()
    {
        var canvas = new RectCanvas();

        CanvasMarquee.Draw(canvas, default, new UiMarqueeBox { Start = Vector2.Zero, Current = new Vector2(50, 50) });

        Assert.That(canvas.Rects, Is.Empty);
    }

    private readonly record struct RectCall(Rect2 Bounds, Color Color);

    private sealed class RectCanvas : IRectCanvas
    {
        public List<RectCall> Rects { get; } = new();

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(new RectCall(bounds, color));
        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
