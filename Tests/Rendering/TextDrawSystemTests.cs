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
public sealed class TextDrawSystemTests
{
    private EntityStore _store = null!;
    private RecordingCanvas _canvas = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _canvas = new RecordingCanvas();
        _root = new SystemRoot(_store) { new TextDrawSystem(_canvas) };
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
    public void EmptyTextIsSkipped()
    {
        Entity entity = AddText("");

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Assert.That(entity.Tags.Has<Dirty>(), Is.True);
    }

    private Entity AddText(string text) =>
        _store.CreateEntity(
            new Text(text),
            new Rectangle(new Rect2(-20, -10, 40, 20)),
            new Drawable(0),
            default(VisualRid),
            Tags.Get<Dirty>());

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<DrawCall> Draws { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
            Draws.Add(new DrawCall(canvas, bounds, text, color));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct DrawCall(Rid Canvas, Rect2 Bounds, string Text, Color Color);
    }
}
