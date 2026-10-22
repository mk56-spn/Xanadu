using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Input;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Debug;

[TestFixture]
public sealed class DebugElementOverlaySystemTests
{
    [Test]
    public void DrawsButtonsAndContainers()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new Position(new Vector2(100, 100)),
            new Rectangle(new Rect2(-25, -20, 50, 40)),
            new ButtonVisual(Colors.Red, Colors.Red, Colors.White));
        Entity panel = store.CreateEntity(
            new Position(new Vector2(300, 200)),
            new Rectangle(new Rect2(-100, -80, 200, 160)),
            new UiContainer(StackDirection.Vertical),
            new UiAnchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)));

        var canvas = new RecordingCanvas();
        var toggle = new ScriptedToggle { Pressed = true };
        var root = new SystemRoot(store)
        {
            new DebugElementOverlaySystem(canvas, default, toggle),
        };
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Visible, Is.True);
        Assert.That(canvas.Rects, Has.Count.EqualTo(11));
        Assert.That(canvas.Circles, Has.Count.EqualTo(2));
        Assert.That(canvas.Circles[0].Center, Is.EqualTo(new Vector2(300, 280)));
        Assert.That(canvas.Texts, Does.Contain("GREEN PINS"));
    }

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;
        public bool IsTogglePressed() => Pressed;
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public bool Visible { get; private set; }
        public List<Rect2> Rects { get; } = new();
        public List<(Vector2 Center, float Radius)> Circles { get; } = new();
        public List<string> Texts { get; } = new();
        public void SetVisible(Rid canvas, bool visible) => Visible = visible;
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }
        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(bounds);
        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
            Circles.Add((center, radius));
        public void DrawCircleOutline(
            Rid canvas,
            Vector2 center,
            float radius,
            Color color,
            float width,
            int segments = 40) =>
            Circles.Add((center, radius));
        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) => Texts.Add(text);
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
