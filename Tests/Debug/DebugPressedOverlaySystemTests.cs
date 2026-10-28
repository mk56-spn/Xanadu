using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Debug;

[TestFixture]
public sealed class DebugPressedOverlaySystemTests : EcsSystemTest
{
    private RecordingCanvas _canvas = null!;
    private ScriptedToggle _toggle = null!;

    protected override void OnEcsSetUp()
    {
        _canvas = new RecordingCanvas();
        _toggle = new ScriptedToggle();
        Root.Add(new DebugPressedOverlaySystem(_canvas, default, _toggle));
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
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)));
        store.CreateEntity(new Text("label"), new Position(Vector2.Zero));
        var canvas = new RecordingCanvas();
        var toggle = new ScriptedToggle { Pressed = true };
        var root = new SystemRoot(store) { new DebugPressedOverlaySystem(canvas, default, toggle, store) };

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Texts, Is.Not.Empty);
        Assert.That(canvas.Rects, Is.Not.Empty);
        Assert.That(canvas.RightTexts, Does.Contain("last ms"));
        Assert.That(canvas.RightTexts, Does.Contain("avg ms"));
        Assert.That(
            canvas.Texts,
            Has.Some.StartsWith("ARCHETYPES "));
        Assert.That(canvas.Texts, Does.Contain("components"));
        Assert.That(canvas.Texts, Does.Contain("tags"));
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

    [Test]
    public void ArchetypePanelDrawsTheTableColumns()
    {
        var store = new EntityStore();
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)));
        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store)
        {
            new DebugPressedOverlaySystem(
                canvas,
                default,
                new ScriptedToggle { Pressed = true },
                store),
        };

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.RightTexts, Does.Contain("n"));
        Assert.That(canvas.RightTexts, Does.Contain("Δ"));
        Assert.That(canvas.RightTexts, Does.Contain("cap"));
        Assert.That(canvas.RightTexts, Does.Contain("use"));
        Assert.That(canvas.RightTexts, Does.Contain("KB"));
        Assert.That(canvas.Texts, Does.Contain("components"));
        Assert.That(canvas.Texts, Does.Contain("tags"));
    }

    [Test]
    public void SelectingARowShowsItsFullSignatureAndSample()
    {
        var store = new EntityStore();
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), new Text("x"));
        var canvas = new RecordingCanvas();
        var paging = new ScriptedPaging();
        var root = new SystemRoot(store)
        {
            new DebugPressedOverlaySystem(
                canvas,
                default,
                new ScriptedToggle { Pressed = true },
                store,
                paging: paging),
        };

        root.Update(new UpdateTick(1f / 60f, 0));
        Assert.That(canvas.Texts, Has.None.StartsWith("COMPONENTS "), "no row is selected yet");

        paging.SelectionDown = true;
        root.Update(new UpdateTick(1f / 60f, 1f / 60f));

        string components = canvas.Texts.Single(text => text.StartsWith("COMPONENTS "));
        Assert.That(components, Does.Contain("Rectangle"));
        Assert.That(components, Does.Not.Contain("..."), "the detail line is not truncated");
        Assert.That(canvas.Texts, Has.Some.StartsWith("TAGS "));
        Assert.That(canvas.Texts, Has.Some.StartsWith("ENTITIES "));
    }

    [Test]
    public void SelectionDoesNotRepeatWhileTheKeyIsHeld()
    {
        var store = new EntityStore();
        // Creation order puts the single-entity archetype first; the second holds two.
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)));
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), Tags.Get<Dirty>());
        store.CreateEntity(new Rectangle(new Rect2(0, 0, 10, 10)), Tags.Get<Dirty>());
        var canvas = new RecordingCanvas();
        var paging = new ScriptedPaging();
        var root = new SystemRoot(store)
        {
            new DebugPressedOverlaySystem(
                canvas,
                default,
                new ScriptedToggle { Pressed = true },
                store,
                paging: paging),
        };

        root.Update(new UpdateTick(1f / 60f, 0));
        paging.SelectionDown = true;
        root.Update(new UpdateTick(1f / 60f, 1f / 60f));
        root.Update(new UpdateTick(1f / 60f, 2f / 60f));
        root.Update(new UpdateTick(1f / 60f, 3f / 60f));

        string entities = canvas.Texts.Last(text => text.StartsWith("ENTITIES "));
        Assert.That(
            entities.Split(", ").Length,
            Is.EqualTo(1),
            "the held key selected the first row once and did not advance to the second");
    }

    private void AddPressedRect(Vector2 center, Vector2 size) =>
        Store.CreateEntity(
            new Rectangle(new Rect2(-size / 2, size)),
            new Position(center),
            Tags.Get<Pressed>());

    private sealed class ScriptedToggle : IOverlayToggleSource
    {
        public bool Pressed;

        public bool IsTogglePressed() => Pressed;
    }

    private sealed class ScriptedPaging : IOverlayPageSource
    {
        public bool Previous;
        public bool Next;
        public bool SelectionUp;
        public bool SelectionDown;
        public bool Sort;

        public bool PreviousPressed() => Previous;

        public bool NextPressed() => Next;

        public bool SelectionUpPressed() => SelectionUp;

        public bool SelectionDownPressed() => SelectionDown;

        public bool SortTogglePressed() => Sort;
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
