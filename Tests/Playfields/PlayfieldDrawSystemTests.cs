using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.Playfields;

[TestFixture]
public sealed class PlayfieldDrawSystemTests
{
    [Test]
    public void PlayModeDrawsSharedPlayfield()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Play));
        store.CreateEntity(new Note(1_000, new Vector2(100, 100)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Is.Not.Empty);
    }

    [Test]
    public void PlayModeUsesTheMapsBackgroundColour()
    {
        var view = new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000));
        var playfield = new Playfield(1000, 1000);
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Shaded", "") { Background = new Color("#204060") },
            view,
            playfield,
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Play));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        Assert.That(
            canvas.RectCalls.Exists(call =>
                call.Bounds == field &&
                call.Color == new Color("#204060")),
            Is.True);
    }

    [Test]
    public void PlayModeDrawsBlocks()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Play));
        store.CreateEntity(
            new Block(new Vector2(100, 200), new Vector2(200, 100)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Rects,
            Does.Contain(new Rect2(new Vector2(150, 200), new Vector2(100, 50))));
    }

    [Test]
    public void PlayModeDoesNotDrawTheEditorGrid()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Play));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        foreach (RecordingCanvas.LineCall line in canvas.Lines)
        {
            Assert.That(line.Color, Is.Not.EqualTo(PlayfieldRenderer.GridColor));
            Assert.That(line.Color, Is.Not.EqualTo(PlayfieldRenderer.AxisColor));
        }
    }

    [Test]
    public void EditorModeDoesNotDrawPlayMode()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Editor));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Is.Empty);
        Assert.That(canvas.Lines, Is.Empty);
    }

    [Test]
    public void HitPopsSwellAndFadeWhereTheNoteWas()
    {
        var store = new EntityStore();
        store.CreateEntity(
            new PlayfieldView(new Vector2(100, 100), 0.5f, new Vector2(1000, 1000)),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, true, durationMs: 5_000),
            new PlayfieldMode(PlayfieldKind.Play));
        store.CreateEntity(
            new NotePop(new Note(0, Vector2.Zero), new Vector2(200, 200), Colors.Cyan, 36f));
        store.CreateEntity(
            new NotePop(
                new Note(0, Vector2.Zero),
                new Vector2(400, 400),
                Colors.Cyan,
                36f,
                PlayfieldRenderer.NotePopLifetime)
            {
                Age = PlayfieldRenderer.NotePopLifetime * 0.5f,
            });

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new PlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        RecordingCanvas.CircleCall fresh = canvas.Circles.Find(
            circle => circle.Center == new Vector2(200, 200));
        RecordingCanvas.CircleCall aged = canvas.Circles.Find(
            circle => circle.Center == new Vector2(400, 400));

        Assert.That(fresh.Radius, Is.EqualTo(36f).Within(0.001f));
        Assert.That(aged.Radius, Is.GreaterThan(fresh.Radius));
        Assert.That(aged.Color.A, Is.LessThan(fresh.Color.A));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();

        public List<RectCall> RectCalls { get; } = new();

        public List<LineCall> Lines { get; } = new();

        public List<CircleCall> Circles { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color)
        {
            Rects.Add(bounds);
            RectCalls.Add(new RectCall(bounds, color));
        }

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) =>
            Circles.Add(new CircleCall(center, radius, color));

        public void DrawCircleOutline(
            Rid canvas,
            Vector2 center,
            float radius,
            Color color,
            float width) =>
            Circles.Add(new CircleCall(center, radius, color));

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines.Add(new LineCall(from, to, color));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct RectCall(Rect2 Bounds, Color Color);

        public readonly record struct LineCall(Vector2 From, Vector2 To, Color Color);

        public readonly record struct CircleCall(Vector2 Center, float Radius, Color Color);
    }
}
