using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Rendering;

namespace Writer.Tests.UI.Rendering;

[TestFixture]
public sealed class GradientDrawSystemTests
{
    [Test]
    public void DrawsGradientBandsFromStartToEnd()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new Rectangle(new Rect2(-100, -25, 200, 50)),
            new Drawable(0),
            new VisualRid(default),
            new LinearGradient(Colors.Black, Colors.White),
            Tags.Get<Dirty>());

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new GradientDrawSystem(canvas));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Has.Count.EqualTo(32));
        Assert.That(canvas.Colors[0], Is.EqualTo(Colors.Black));
        Assert.That(canvas.Colors[^1], Is.EqualTo(Colors.White));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();
        public List<Color> Colors { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color)
        {
            Rects.Add(bounds);
            Colors.Add(color);
        }

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
