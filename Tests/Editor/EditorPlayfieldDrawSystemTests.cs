using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Editor;

namespace Writer.Tests.Editor;

[TestFixture]
public sealed class EditorPlayfieldDrawSystemTests
{
    [Test]
    public void RulerDrawsBeatTicksAndNoteMarkersInFooter()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Ruler", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            view,
            new NoteSelection(default),
            new EditorPreview(false, 0, false));
        store.CreateEntity(new Note(1_000, new Vector2(100, 100)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Lines.Count, Is.GreaterThan(10));
        Assert.That(canvas.Rects, Does.Contain(EditorLayout.RulerRect(view)));
    }

    [Test]
    public void PreviewDrawsWithoutEditorChrome()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Preview Level", ""),
            new Playfield(1000, 1000),
            new Playhead(500, 120, 0, true),
            view,
            new NoteSelection(default),
            new EditorPreview(true, 500, true));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Does.Contain(new Rect2(Vector2.Zero, view.ScreenSize)));
        Assert.That(canvas.Rects, Does.Not.Contain(EditorLayout.RulerRect(view)));
        Assert.That(canvas.Rects, Is.Not.Empty);
    }

    [Test]
    public void DrawsPlayfieldHeaderFooterAndTimeline()
    {
        var view = new PlayfieldView(new Vector2(276, 88), 0.404f, new Vector2(1152, 648));
        var playfield = new Playfield(1000, 1000);
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Test Level", ""),
            playfield,
            new Playhead(0, 120, 0, false),
            view,
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Does.Contain(EditorLayout.PlayfieldRect(view, playfield)));
        Assert.That(canvas.Rects, Does.Contain(EditorLayout.HeaderRect(view)));
        Assert.That(canvas.Rects, Does.Contain(EditorLayout.FooterRect(view)));
        Assert.That(canvas.Rects, Does.Contain(EditorLayout.RulerRect(view)));
        Assert.That(canvas.Texts, Does.Contain("500"));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();

        public List<string> Texts { get; } = new();

        public List<LineCall> Lines { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(bounds);

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines.Add(new LineCall(from, to));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
            Texts.Add(text);

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct LineCall(Vector2 From, Vector2 To);
    }
}
