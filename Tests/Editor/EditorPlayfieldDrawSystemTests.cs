using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Writer.Ecs;
using Writer.Systems.Canvas;
using Writer.Systems.Editor;
using Writer.Systems.Playfields;

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
    public void TimelineLabelsTheBpmMarkerAtTheBeatOffset()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Tempo", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 140, 500, false, durationMs: 5_000),
            view,
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Texts, Does.Contain("140 BPM"));
    }

    [Test]
    public void ZoomedTimelineUsesANarrowerTimeWindow()
    {
        var view = new PlayfieldView(new Vector2(276, 88), 0.404f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Zoomed", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(default),
            new EditorTimeline(4f));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Rects, Does.Contain(EditorLayout.RulerRect(view)));
        Assert.That(canvas.Lines, Is.Not.Empty);

        (long start, long end) = EditorLayout.TimelineWindow(
            new Playhead(0, 120, 0, false, durationMs: 5_000),
            4f);
        Assert.That(start, Is.EqualTo(0));
        Assert.That(end, Is.EqualTo(1_250));
    }

    [Test]
    public void TimelineSnapLinesUseWhiteRedAndBlue()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Colors", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false, durationMs: 5_000, snapDivision: 4),
            view,
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Any(line => line.Color == EditorPlayfieldDrawSystem.MainSnapColor),
            Is.True);
        Assert.That(
            canvas.Lines.Any(line => line.Color == EditorPlayfieldDrawSystem.HalfSnapColor),
            Is.True);
        Assert.That(
            canvas.Lines.Any(line => line.Color == EditorPlayfieldDrawSystem.QuarterSnapColor),
            Is.True);
    }

    [Test]
    public void NotesAtTheSameTimeStackAsSquares()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Stacked", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(default),
            new EditorTimeline(32f));
        store.CreateEntity(new Note(1_000, new Vector2(100, 100)));
        store.CreateEntity(new Note(1_000, new Vector2(200, 200)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        var squares = canvas.Rects
            .Where(rect => rect.Size == new Vector2(8, 8))
            .ToList();
        Assert.That(squares, Has.Count.EqualTo(2));
        Assert.That(squares[0].Position.X, Is.EqualTo(squares[1].Position.X));
        Assert.That(squares[0].Position.Y, Is.Not.EqualTo(squares[1].Position.Y));
    }

    [Test]
    public void TimelineMarkersUseLaneColorWithASelectionOutline()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        Entity note = store.CreateEntity(
            new Note(
                1_000,
                new Vector2(100, 100),
                target: Writer.Settings.KeybindAction.R1));
        store.CreateEntity(
            new LevelInfo("Markers", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(note));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Color lane = PlayfieldRenderer.ColorFor(
            Writer.Settings.KeybindAction.R1);
        Assert.That(
            canvas.RectCalls.Any(call => call.Color == lane),
            Is.True);
        Assert.That(
            canvas.RectCalls.Any(call =>
                call.Color == PlayfieldRenderer.SelectedNoteColor &&
                call.Bounds.Size.X > call.Bounds.Size.Y),
            Is.True);
    }

    [Test]
    public void SameTimeNotesCapAtSevenAndStayInOneColumn()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Capped", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(default),
            new EditorTimeline(32f));
        for (int i = 0; i < 9; i++)
            store.CreateEntity(new Note(1_000, new Vector2(100 + i, 100)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        var squares = canvas.Rects
            .Where(rect => rect.Size == new Vector2(8, 8))
            .ToList();
        Assert.That(squares, Has.Count.EqualTo(7));
        Assert.That(squares.Select(rect => rect.Position.X).Distinct().Count(),
            Is.EqualTo(1));
        Assert.That(squares.Select(rect => rect.Position.Y).Distinct().Count(),
            Is.EqualTo(7));
    }

    [Test]
    public void TimelineHighlightsEverySelectedNote()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        Entity editor = store.CreateEntity(
            new LevelInfo("Selected", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(default),
            new EditorTimeline(32f));
        Entity first = store.CreateEntity(new Note(1_000, new Vector2(100, 100)));
        Entity second = store.CreateEntity(new Note(1_000, new Vector2(200, 200)));
        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        selection.SetMany(new[] { first, second });

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        var selectedOutlines = canvas.RectCalls
            .Where(call =>
                call.Color == PlayfieldRenderer.SelectedNoteColor &&
                call.Bounds.Size.X > call.Bounds.Size.Y)
            .ToList();
        Assert.That(
            selectedOutlines.Select(call => call.Bounds.Position)
                .Distinct().Count(),
            Is.EqualTo(4));
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

        public List<RectCall> RectCalls { get; } = new();

        public List<string> Texts { get; } = new();

        public List<LineCall> Lines { get; } = new();

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color)
        {
            Rects.Add(bounds);
            RectCalls.Add(new RectCall(bounds, color));
        }

        public void DrawLine(
            Rid canvas,
            Vector2 from,
            Vector2 to,
            Color color,
            float width) =>
            Lines.Add(new LineCall(from, to, color));

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) { }

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
            Texts.Add(text);

        public void DrawTextVertical(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize) =>
            Texts.Add(text);

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) { }

        public readonly record struct RectCall(
            Rect2 Bounds,
            Color Color);

        public readonly record struct LineCall(
            Vector2 From,
            Vector2 To,
            Color Color);
    }
}
