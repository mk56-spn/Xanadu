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
public sealed class DebugPressedOverlaySystemTests
{
    private EntityStore _store = null!;
    private RecordingCanvas _canvas = null!;
    private ScriptedToggle _toggle = null!;
    private SystemRoot _root = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _canvas = new RecordingCanvas();
        _toggle = new ScriptedToggle();
        _root = new SystemRoot(_store) { new DebugPressedOverlaySystem(_canvas, default, _toggle) };
    }

    [Test]
    public void ToggleOnlyControlsTheOverlay()
    {
        _toggle.Pressed = true;
        Frame();
        Assert.That(_canvas.Visible, Is.True);
        Assert.That(_canvas.Rects, Is.Empty);
    }

    [Test]
    public void TogglingAgainHidesOverlay()
    {
        AddPressedRect(new Vector2(100, 100), new Vector2(50, 40));

        _toggle.Pressed = true;
        Frame();
        _toggle.Pressed = false;
        Frame();
        _toggle.Pressed = true;
        Frame();

        Assert.That(_canvas.Visible, Is.False);
    }

    [Test]
    public void MetricsPanelDrawsWhenStoreProvided()
    {
        var store = new EntityStore();
        var canvas = new RecordingCanvas();
        var toggle = new ScriptedToggle { Pressed = true };
        var root = new SystemRoot(store) { new DebugPressedOverlaySystem(canvas, default, toggle, store) };

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Texts, Is.Not.Empty);
        Assert.That(canvas.Rects, Is.Not.Empty);
        Assert.That(canvas.RightTexts, Does.Contain("last ms"));
        Assert.That(canvas.RightTexts, Does.Contain("avg ms"));
    }

    [Test]
    public void HoldingToggleDoesNotRepeat()
    {
        AddPressedRect(new Vector2(100, 100), new Vector2(50, 40));

        _toggle.Pressed = true;
        Frame();
        Frame();

        Assert.That(_canvas.Visible, Is.True);
    }

    private void AddPressedRect(Vector2 center, Vector2 size) =>
        _store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(center),
            Tags.Get<Pressed>());

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;

        public bool IsTogglePressed() => Pressed;
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public bool Visible { get; private set; }

        public List<Rect2> Rects { get; } = new();

        public List<string> Texts { get; } = new();

        public List<string> RightTexts { get; } = new();

        public void SetVisible(Rid canvas, bool visible) => Visible = visible;

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(bounds);

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) { }

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
            Texts.Add(text);

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
            RightTexts.Add(text);
    }
}
