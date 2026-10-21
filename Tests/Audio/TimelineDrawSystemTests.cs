using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;

namespace Writer.Tests.Audio;

[TestFixture]
public sealed class TimelineDrawSystemTests
{
    [Test]
    public void DriftGraphDrawsRedHistoryLine()
    {
        var store = new EntityStore();
        Entity track = store.CreateEntity(new TrackProgress(
            "res://test-track.wav",
            durationMs: 20_000,
            bpm: 128f)
        {
            NowMs = 1_000,
            AudioMs = 980,
            Started = true,
        });

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new TimelineDrawSystem(canvas, default));
        root.Update(new UpdateTick(1f / 60f, 0));

        ref TrackProgress progress = ref track.GetComponent<TrackProgress>();
        progress.NowMs = 2_000;
        progress.AudioMs = 1_930;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Lines, Is.Not.Empty);
        Assert.That(
            canvas.Lines.Any(line => line.Color.R > 0.9f && line.Color.G < 0.4f),
            Is.True);
        Assert.That(
            canvas.Lines.Any(line => line.Color.B > 0.9f && line.Color.G > 0.7f),
            Is.True);
    }

    [Test]
    public void RepeatedAudioCursorDoesNotAddDriftSamples()
    {
        var store = new EntityStore();
        Entity track = store.CreateEntity(new TrackProgress(
            "res://test-track.wav",
            durationMs: 20_000,
            bpm: 128f)
        {
            NowMs = 1_000,
            AudioMs = 980,
            Started = true,
        });

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new TimelineDrawSystem(canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        ref TrackProgress progress = ref track.GetComponent<TrackProgress>();
        progress.NowMs = 2_000;
        progress.AudioMs = 1_930;
        root.Update(new UpdateTick(1f / 60f, 0));

        int lineCount = canvas.Lines.Count;

        progress.NowMs = 2_100;
        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Lines.Count, Is.EqualTo(lineCount));
    }

    [Test]
    public void TimelineDrawsProgressTicksAndPlayhead()
    {
        var store = new EntityStore();
        store.CreateEntity(new TrackProgress(
            "res://test-track.wav",
            durationMs: 20_000,
            bpm: 128f)
        {
            NowMs = 5_000,
            AudioMs = 4_980,
            Started = true,
        });

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new TimelineDrawSystem(canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Is.Not.Empty);
        Assert.That(canvas.Texts, Does.Contain("MINIAUDIO / TIMER_PLAY TRACK TEST"));
    }

    private sealed class RecordingCanvas : IRectCanvas
    {
        public List<Rect2> Rects { get; } = new();

        public List<string> Texts { get; } = new();

        public List<LineCall> Lines { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas)
        {
            Rects.Clear();
            Texts.Clear();
            Lines.Clear();
        }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Rects.Add(bounds);

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines.Add(new LineCall(from, to, color, width));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
            Texts.Add(text);

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct LineCall(
            Vector2 From,
            Vector2 To,
            Color Color,
            float Width);
    }
}
