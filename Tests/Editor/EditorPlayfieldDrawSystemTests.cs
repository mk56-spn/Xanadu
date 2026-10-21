using System.Collections.Generic;
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
    public void DrawsPlayfieldAndInspector()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Test Level", ""),
            new Playfield(1000, 1000),
            new EditorPlayhead(0, 120, 0, false),
            new EditorView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648)),
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Is.Not.Empty);
        Assert.That(canvas.Lines, Is.Not.Empty);
        Assert.That(canvas.Texts, Does.Contain("EDITOR"));
        Assert.That(canvas.Texts, Does.Contain("Level: Test Level"));
        Assert.That(canvas.Texts, Does.Contain("Playfield 1000 x 1000"));
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
