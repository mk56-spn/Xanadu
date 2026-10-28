using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Rendering;

namespace Xanadu.Tests.UI.Rendering;

[TestFixture]
public sealed class RectDrawSystemTests : EcsSystemTest
{
    private RecordingRectCanvas _canvas = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingRectCanvas();
        Root.Add(new RectDrawSystem(_canvas));
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
    public void SkewedContainerDrawsAParallelogramInsteadOfARect()
    {
        float skew = Mathf.DegToRad(6);
        var bounds = new Rect2(-50, -50, 100, 100);
        Store.CreateEntity(
            new Rectangle(bounds),
            new Drawable(0),
            new VisualRid(new Rid()),
            new FillColor(Colors.Red),
            new UiContainer(StackDirection.Vertical, skew: skew),
            Tags.Get<Dirty>());

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Assert.That(_canvas.Polygons, Has.Count.EqualTo(1));
        Vector2[] corners = _canvas.Polygons[0];
        Assert.That(corners[0].X, Is.GreaterThan(bounds.Position.X));
        Assert.That(corners[3].X, Is.LessThan(bounds.Position.X));
        Assert.That(corners[0].Y, Is.EqualTo(bounds.Position.Y));
    }

    [Test]
    public void ChamferedRectDrawsAFivePointPolygonCutAtTheTopRight()
    {
        var bounds = new Rect2(-100, -20, 200, 40);
        Store.CreateEntity(
            new Rectangle(bounds),
            new Drawable(0),
            new VisualRid(new Rid()),
            new FillColor(Colors.Red),
            new UiChamfer(12),
            Tags.Get<Dirty>());

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Vector2[] points = _canvas.Polygons[0];
        Assert.That(points, Has.Length.EqualTo(5));
        Assert.That(points[0], Is.EqualTo(new Vector2(-100, -20)));
        Assert.That(points[1], Is.EqualTo(new Vector2(88, -20)));
        Assert.That(points[2], Is.EqualTo(new Vector2(100, -8)));
        Assert.That(points[3], Is.EqualTo(new Vector2(100, 20)));
    }

    [Test]
    public void DiamondDrawsTheMidpointsOfItsSides()
    {
        var bounds = new Rect2(-50, -30, 100, 60);
        Store.CreateEntity(
            new Rectangle(bounds),
            new Drawable(0),
            new VisualRid(new Rid()),
            new FillColor(Colors.Red),
            new UiDiamond(),
            Tags.Get<Dirty>());

        Frame();

        Assert.That(_canvas.Draws, Is.Empty);
        Assert.That(_canvas.Polygons[0], Is.EqualTo(new[]
        {
            new Vector2(0, -30),
            new Vector2(50, 0),
            new Vector2(0, 30),
            new Vector2(-50, 0),
        }));
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
        Store.CreateEntity(
            new Rectangle(bounds),
            new Drawable(0),
            default(VisualRid),
            new FillColor(color),
            Tags.Get<Dirty>());

    private sealed class RecordingRectCanvas : IRectCanvas
    {
        public List<Rid> Clears { get; } = new();

        public List<DrawCall> Draws { get; } = new();

        public List<Vector2[]> Polygons { get; } = new();

        public List<TextCall> TextDraws { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) => Clears.Add(canvas);

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) =>
            Draws.Add(new DrawCall(canvas, bounds, color));

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawPolygon(Rid canvas, ReadOnlySpan<Vector2> points, Color color) =>
            Polygons.Add(points.ToArray());

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
            TextDraws.Add(new TextCall(canvas, bounds, text, color));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct DrawCall(Rid Canvas, Rect2 Bounds, Color Color);

        public readonly record struct TextCall(Rid Canvas, Rect2 Bounds, string Text, Color Color);
    }
}
