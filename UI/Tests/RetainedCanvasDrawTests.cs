using NUnit.Framework;
using Xanadu.Systems.Canvas;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class RetainedCanvasDrawTests
{
    private sealed class CountingCanvas : IRectCanvas
    {
        public int Clears;
        public int Rects;
        public int Texts;
        public void SetVisible(Handle canvas, bool visible) { }
        public void SetTransform(Handle canvas, Transform2D transform) { }
        public void Clear(Handle canvas) => Clears++;
        public void DrawRect(Handle canvas, Rect2 bounds, Color color) => Rects++;
        public void DrawLine(Handle canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Handle canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Handle canvas, Rect2 bounds, string text, Color color) => Texts++;
        public void DrawTextRight(Handle canvas, Rect2 bounds, string text, Color color) => Texts++;
    }

    private readonly Rect2 _box = new(0, 0, 10, 10);

    private void Frame(RetainedCanvasDraw draw, string text = "a", Color? color = null)
    {
        draw.Begin();
        draw.Rect(_box, color ?? Colors.Red);
        draw.TextLeft(_box, text, Colors.White);
        draw.End();
    }

    [Test]
    public void TheSamePictureIsSentOnce()
    {
        var canvas = new CountingCanvas();
        var draw = new RetainedCanvasDraw(canvas, default);

        Frame(draw);
        Frame(draw);
        Frame(draw);

        Assert.That(canvas.Clears, Is.EqualTo(1));
        Assert.That(canvas.Rects, Is.EqualTo(1));
    }

    [TestCase("b", false)]
    [TestCase("a", true)]
    public void AnyChangeSendsTheWholePictureAgain(string text, bool recolour)
    {
        var canvas = new CountingCanvas();
        var draw = new RetainedCanvasDraw(canvas, default);
        Frame(draw);

        Frame(draw, text, recolour ? Colors.Blue : null);

        Assert.That(canvas.Clears, Is.EqualTo(2));
        Assert.That(canvas.Rects, Is.EqualTo(2));
        Assert.That(canvas.Texts, Is.EqualTo(2));
    }

    [Test]
    public void AFewerCommandsPictureIsAChange()
    {
        var canvas = new CountingCanvas();
        var draw = new RetainedCanvasDraw(canvas, default);
        Frame(draw);

        draw.Begin();
        draw.Rect(_box, Colors.Red);
        draw.End();

        Assert.That(canvas.Clears, Is.EqualTo(2));
    }

    [Test]
    public void AfterAResetThePictureIsSentAgain()
    {
        var canvas = new CountingCanvas();
        var draw = new RetainedCanvasDraw(canvas, default);
        Frame(draw);

        draw.Reset();
        Frame(draw);

        Assert.That(canvas.Rects, Is.EqualTo(2));
    }
}
