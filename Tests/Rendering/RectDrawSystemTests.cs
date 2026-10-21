using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Rendering;

namespace Writer.Tests.Rendering;

[TestFixture]
public sealed class RectDrawSystemTests
{
    private EntityStore _store = null!;
    private RecordingRectCanvas _canvas = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _canvas = new RecordingRectCanvas();
        _root = new SystemRoot(_store) { new RectDrawSystem(_canvas) };
    }

    [Test]
    public void DirtyRectDrawsAndClearsDirtyTag()
    {
        var bounds = new Rect2(10, 20, 30, 40);
        Entity entity = AddRect(bounds);

        Frame();

        Assert.That(_canvas.Clears, Has.Count.EqualTo(1));
        Assert.That(_canvas.Draws, Has.Count.EqualTo(1));
        Assert.That(_canvas.Draws[0].Bounds, Is.EqualTo(bounds));
        Assert.That(_canvas.Draws[0].Color, Is.EqualTo(Colors.White));
        Assert.That(entity.Tags.Has<Dirty>(), Is.False);
    }

    [Test]
    public void CleanRectDoesNotRedraw()
    {
        AddRect(new Rect2(10, 20, 30, 40));

        Frame();
        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(1));
    }

    [Test]
    public void RedirtyingRectDrawsAgain()
    {
        Entity entity = AddRect(new Rect2(10, 20, 30, 40));

        Frame();
        entity.AddTag<Dirty>();
        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(2));
    }

    [Test]
    public void MultipleDirtyRectsAllDraw()
    {
        AddRect(new Rect2(0, 0, 10, 10));
        AddRect(new Rect2(20, 20, 10, 10));

        Frame();

        Assert.That(_canvas.Draws, Has.Count.EqualTo(2));
    }

    [Test]
    public void UsesEntityFillColor()
    {
        AddRect(new Rect2(0, 0, 10, 10), Colors.Red);

        Frame();

        Assert.That(_canvas.Draws[0].Color, Is.EqualTo(Colors.Red));
    }

    [Test]
    public void ZeroSizedRectIsNotDrawn()
    {
        Entity entity = AddRect(new Rect2(0, 0, 0, 10));

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Assert.That(entity.Tags.Has<Dirty>(), Is.True);
    }

    private Entity AddRect(Rect2 bounds) => AddRect(bounds, Colors.White);

    private Entity AddRect(Rect2 bounds, Color color) =>
        _store.CreateEntity(
            new Rectangle(bounds),
            new Drawable(0),
            default(VisualRid),
            new FillColor(color),
            Tags.Get<Dirty>());

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class RecordingRectCanvas : IRectCanvas
    {
        public List<Rid> Clears { get; } = new();

        public List<DrawCall> Draws { get; } = new();

        public List<TextCall> TextDraws { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) => Clears.Add(canvas);

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
            Draws.Add(new DrawCall(canvas, bounds, color));

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
            TextDraws.Add(new TextCall(canvas, bounds, text, color));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct DrawCall(Rid Canvas, Rect2 Bounds, Color Color);

        public readonly record struct TextCall(Rid Canvas, Rect2 Bounds, string Text, Color Color);
    }
}
