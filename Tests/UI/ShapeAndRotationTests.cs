using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Rendering;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

[TestFixture]
public sealed class ShapeAndRotationTests
{
    [Test]
    public void TransformSystemAppliesRotationAboutPosition()
    {
        var store = new EntityStore();
        var canvas = new RecordingCanvas();
        Entity entity = store.CreateEntity(
            new VisualRid(new Rid()),
            new Position(new Vector2(10, 20)),
            new UiRotation(0.5f),
            Tags.Get<Moved>());

        var root = new SystemRoot(store);
        root.Add(new TransformSystem(canvas));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Transform.Rotation, Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(canvas.Transform.Origin, Is.EqualTo(new Vector2(10, 20)));
        Assert.That(entity.Tags.Has<Moved>(), Is.False);
    }

    [Test]
    public void ShapeDrawSystemFillsAndClosesTheOutline()
    {
        var store = new EntityStore();
        var canvas = new RecordingCanvas();
        Vector2[] points = [new(0, 0), new(10, 0), new(10, 10)];
        store.CreateEntity(
            new Rectangle(new Rect2(0, 0, 10, 10)),
            new Drawable(0),
            new VisualRid(new Rid()),
            new UiShape(points, Colors.Black, Colors.White, 3),
            Tags.Get<Dirty>());

        var root = new SystemRoot(store);
        root.Add(new ShapeDrawSystem(canvas));
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.PolygonPoints, Is.EqualTo(3));
        Assert.That(canvas.PolylinePoints, Is.EqualTo(4));
    }

    [Test]
    public void RaisedEntitiesDrawAboveButtonsAndPlainText()
    {
        var store = new EntityStore();
        Entity button = store.CreateEntity(new Text("row"), Tags.Get<TakesInput>());
        Entity label = store.CreateEntity(new Text("label"));
        Entity raised = store.CreateEntity(new Text("raised"), Tags.Get<UiRaised>());

        int buttonZ = DrawableInitialiseSystem.LocalZIndex(button);
        Assert.That(DrawableInitialiseSystem.LocalZIndex(label), Is.EqualTo(buttonZ));
        Assert.That(DrawableInitialiseSystem.LocalZIndex(raised), Is.GreaterThan(buttonZ));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public Transform2D Transform;
        public int PolygonPoints;
        public int PolylinePoints;

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) => Transform = transform;

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
            PolygonPoints = points.Length;

        public void DrawPolyline(
            Rid canvas,
            ReadOnlySpan<Vector2> points,
            Color color,
            float width,
            bool antialiased = true) =>
            PolylinePoints = points.Length;

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }
    }
}
