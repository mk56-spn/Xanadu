using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Rendering;

namespace Xanadu.Tests.UI.Rendering;

[TestFixture]
public sealed class WheelDrawSystemTests : EcsSystemTest
{
    private static readonly Color Blue = new("#2563eb");
    private static readonly Color Yellow = new("#facc15");

    private RecordingWheelCanvas _canvas = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingWheelCanvas();
        Root.Add(new WheelDrawSystem(_canvas));
    }

    [Test]
    public void DirtyWheelDrawsWedgesHubAndLabels()
    {
        Entity wheel = AddWheel(selected: 2);

        Frame();

        Assert.That(_canvas.Clears, Is.EqualTo(1));
        Assert.That(_canvas.Polygons, Has.Count.EqualTo(8));
        Assert.That(_canvas.Circles, Has.Count.EqualTo(1));
        Assert.That(_canvas.Texts, Has.Count.EqualTo(9));
        Assert.That(_canvas.Texts.Exists(text => text.Text == "→"), Is.True);
        Assert.That(wheel.Tags.Has<Dirty>(), Is.False);
    }

    [Test]
    public void SelectedWedgeIsYellowAndReachesTheRim()
    {
        AddWheel(selected: 2);

        Frame();

        Assert.That(_canvas.Polygons[2].Color, Is.EqualTo(Yellow));
        Assert.That(_canvas.Polygons[0].Color, Is.EqualTo(Blue));
        Assert.That(MaxRadius(_canvas.Polygons[2].Points),
            Is.GreaterThan(MaxRadius(_canvas.Polygons[0].Points)));
    }

    [Test]
    public void SelectedLabelIsDarkAndUnselectedLabelsAreLight()
    {
        AddWheel(selected: 2);

        Frame();

        Assert.That(_canvas.Texts[2].Color, Is.EqualTo(new Color("#1e293b")));
        Assert.That(_canvas.Texts[0].Color, Is.EqualTo(new Color("#f8fafc")));
    }

    [Test]
    public void HoverLightensAnUnselectedWedge()
    {
        Entity wheel = AddWheel(selected: 2);
        Frame();
        Color normal = _canvas.Polygons[0].Color;

        wheel.GetComponent<UiWheel>().Hover = 0;
        wheel.AddTag<Dirty>();
        _canvas.Polygons.Clear();
        Frame();

        Assert.That(_canvas.Polygons[0].Color, Is.Not.EqualTo(normal));
        Assert.That(_canvas.Polygons[0].Color.R, Is.GreaterThan(normal.R));
    }

    [Test]
    public void CleanWheelIsNotPaintedAgain()
    {
        AddWheel(selected: 0);

        Frame();
        Frame();

        Assert.That(_canvas.Clears, Is.EqualTo(1));
    }

    private Entity AddWheel(int selected) =>
        Store.CreateEntity(
            new Rectangle(new Rect2(-100, -100, 200, 200)),
            new Drawable(0),
            default(VisualRid),
            new UiWheel
            {
                Selected = selected,
                Hover = -1,
                Labels = new[] { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" },
                HubRadius = 0.28f,
                StartAngle = UiWheelGeometry.DefaultStartAngle,
                Gap = 0.04f,
            },
            Tags.Get<Dirty>());

    private static float MaxRadius(Vector2[] points)
    {
        float max = 0;
        foreach (Vector2 point in points)
            max = Mathf.Max(max, point.Length());
        return max;
    }

    private sealed class RecordingWheelCanvas : IRectCanvas
    {
        public int Clears { get; private set; }

        public List<WedgeCall> Polygons { get; } = new();

        public List<Vector2> Circles { get; } = new();

        public List<TextCall> Texts { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) => Clears++;

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) { }

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
            Circles.Add(center);

        public void DrawPolygon(
            Rid canvas,
            ReadOnlySpan<Vector2> points,
            Color color) =>
            Polygons.Add(new WedgeCall(points.ToArray(), color));

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
            Texts.Add(new TextCall(bounds, text, color));

        public void DrawTextStyled(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize) =>
            Texts.Add(new TextCall(bounds, text, color));

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct WedgeCall(Vector2[] Points, Color Color);

        public readonly record struct TextCall(Rect2 Bounds, string Text, Color Color);
    }
}
