using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Rendering;

namespace Xanadu.Tests.UI.Rendering;

[TestFixture]
public sealed class GradientDrawSystemTests
{
    [Test]
    public void DrawsOneSmoothRampFromStartToEnd()
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

        Assert.That(canvas.Ramps, Has.Count.EqualTo(1), "bands would overlap and blend twice when see-through");
        Assert.That(canvas.Ramps[0], Is.EqualTo((new Rect2(-100, -25, 200, 50), Colors.Black, Colors.White, true)));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<(Rect2 Bounds, Color Start, Color End, bool Horizontal)> Ramps { get; } = new();

        public void DrawGradientRect(Rid canvas, Rect2 bounds, Color start, Color end, bool horizontal) =>
            Ramps.Add((bounds, start, end, horizontal));

        public void SetVisible(Rid canvas, bool visible) { }
        public void SetTransform(Rid canvas, Transform2D transform) { }
        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }
        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }
        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
