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

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();

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
            Lines.Add(new LineCall(from, to, color));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct LineCall(Vector2 From, Vector2 To, Color Color);
    }
}
