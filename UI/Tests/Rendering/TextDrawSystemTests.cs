using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Tests.Support;
using Writer.Systems.Canvas;
using Writer.Systems.Rendering;

namespace Writer.Tests.UI.Rendering;

[TestFixture]
public sealed class TextDrawSystemTests : EcsSystemTest
{
    private RecordingCanvas _canvas = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingCanvas();
        Root.Add(new TextDrawSystem(_canvas));
    }

    [Test]
    public void DirtyTextDrawsAndClearsDirtyTag()
    {
        Entity entity = AddText("Hello");

        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(1));
        Assert.That(_canvas.Draws[0].Text, Is.EqualTo("Hello"));
        Assert.That(_canvas.Draws[0].Color, Is.EqualTo(Colors.White));
        Assert.That(entity.Tags.Has<Dirty>(), Is.False);
    }

    [Test]
    public void CleanTextDoesNotRedraw()
    {
        AddText("Hello");

        Frame();
        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(1));
    }

    [Test]
    public void TextStyleOverridesFontSizeAndColor()
    {
        Entity entity = AddText("Title");
        entity.AddComponent(new TextStyle(32, Colors.Gold));

        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(1));
        Assert.That(_canvas.Draws[0].FontSize, Is.EqualTo(32));
        Assert.That(_canvas.Draws[0].Color, Is.EqualTo(Colors.Gold));
    }

    [Test]
    public void AlignmentIsPassedToTheCanvas()
    {
        Entity left = AddText("Left");
        left.AddComponent(new TextStyle(13, Colors.White));
        left.AddComponent(new TextAlignment(TextAlign.Left));
        Entity right = AddText("Right");
        right.AddComponent(new TextStyle(13, Colors.White));
        right.AddComponent(new TextAlignment(TextAlign.Right));

        Frame();

        Assert.That(
            _canvas.Draws.Any(draw => draw.Align == TextAlign.Left),
            Is.True);
        Assert.That(
            _canvas.Draws.Any(draw => draw.Align == TextAlign.Right),
            Is.True);
    }

    [Test]
    public void EmptyTextIsSkipped()
    {
        Entity entity = AddText("");

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Assert.That(entity.Tags.Has<Dirty>(), Is.True);
    }

    private Entity AddText(string text) =>
        Store.CreateEntity(
            new Text(text),
            new Rectangle(new Rect2(-20, -10, 40, 20)),
            new Drawable(0),
            default(VisualRid),
            Tags.Get<Dirty>());

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<DrawCall> Draws { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
            Draws.Add(new DrawCall(canvas, bounds, text, color));

        public void DrawTextStyled(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize) =>
            Draws.Add(new DrawCall(canvas, bounds, text, color, fontSize));

        public void DrawTextStyledLeft(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize) =>
            Draws.Add(new DrawCall(
                canvas,
                bounds,
                text,
                color,
                fontSize,
                TextAlign.Left));

        public void DrawTextStyledRight(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize) =>
            Draws.Add(new DrawCall(
                canvas,
                bounds,
                text,
                color,
                fontSize,
                TextAlign.Right));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct DrawCall(
            Rid Canvas,
            Rect2 Bounds,
            string Text,
            Color Color,
            int FontSize = 0,
            TextAlign? Align = null);
    }
}
