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
using Writer.Timeline;

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
            new NoteSelection(default));
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
            new TimelineView(4f));

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
            new TimelineView(32f));
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
    public void MovementNotesDrawTheirDashDestination()
    {
        var view = new PlayfieldView(
            Vector2.Zero,
            1f,
            new Vector2(1000, 1000));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Dash", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            view,
            new NoteSelection(default));
        store.CreateEntity(new Note(
            1_000,
            new Vector2(100, 100),
            kind: NoteKind.Movement,
            dashDirection: DashDirection.Right));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Any(line =>
                line.From == new Vector2(100, 100) &&
                line.To == new Vector2(250, 100)),
            Is.True);
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
            new TimelineView(32f));
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
            new TimelineView(32f));
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

    [Test]
    public void TheSelectedBlockShowsItsResizeNubs()
    {
        var (store, field, canvas, root) = StartPointScene();
        Entity block = store.CreateEntity(
            new Block(new Vector2(400, 500), new Vector2(200, 100)));
        field.GetComponent<NoteSelection>().Note = block;

        root.Update(new UpdateTick(1f / 60f, 0));

        PlayfieldView view = field.GetComponent<PlayfieldView>();
        Rect2 expected = new(
            view.PlayfieldToScreen(new Vector2(400, 500)),
            new Vector2(200, 100) * view.Scale);
        Assert.That(
            canvas.RectCalls.Exists(call =>
                call.Color == PlayfieldRenderer.BlockSelectedEdge &&
                call.Bounds.IsEqualApprox(expected)),
            Is.True);
        Assert.That(
            canvas.RectCalls.FindAll(call =>
                call.Color == PlayfieldRenderer.BlockEdge &&
                call.Bounds.Size == new Vector2(10, 10)),
            Has.Count.EqualTo(4));
    }

    [Test]
    public void PreviewDrawsAGhostWithTheGroupsColour()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        LevelGroups groups = LevelGroups.Create();
        groups.Groups[0].Effects.Add(GroupEffect.Colour(0f, 0.5f, new Color(1f, 0f, 0f, 1f)));
        EditorGroupState state = EditorGroupState.Closed;
        state.Preview = true;
        Entity field = store.CreateEntity(
            new LevelInfo("Preview", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 60_000),
            view,
            new NoteSelection(default),
            groups,
            state);
        Entity start = store.CreateEntity(Start("Intro", 1_000));
        start.AddComponent(new ItemGroups(ItemGroups.Bit(0)));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Any(line =>
                line.Color.R > 0.99f &&
                line.Color.G < 0.01f &&
                line.Color.A > 0.5f),
            Is.True);
        Assert.That(field.GetComponent<EditorGroupState>().Preview, Is.True);
    }

    private static (EntityStore Store, Entity Field, RecordingCanvas Canvas, SystemRoot Root) StartPointScene()
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1152, 648));
        var store = new EntityStore();
        Entity field = store.CreateEntity(
            new LevelInfo("Starts", ""),
            new Playfield(1000, 1000),
            new Playhead(1_000, 120, 0, false, durationMs: 60_000),
            view,
            new NoteSelection(default),
            new TimelineView(8f));
        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));
        return (store, field, canvas, root);
    }

    private static StartPoint Start(string name, long timeMs) =>
        new(name, timeMs, new Vector2(500, 500), System.Numerics.Vector2.Zero, Vector2.Zero);

    private static bool IsStartMarker(RecordingCanvas.RectCall call, Rect2 ruler, Color color) =>
        call.Color == color && call.Bounds.Size.X == 2 && call.Bounds.Size.Y == ruler.Size.Y;

    [Test]
    public void StartPointsShowOnTheTimelineWithTheirNames()
    {
        var (store, field, canvas, root) = StartPointScene();
        store.CreateEntity(Start("chorus", 2_000));
        store.CreateEntity(Start("", 1_500));

        root.Update(new UpdateTick(1f / 60f, 0));

        Rect2 ruler = EditorLayout.RulerRect(field.GetComponent<PlayfieldView>());
        Assert.That(canvas.Texts, Does.Contain("chorus"));
        Assert.That(canvas.Texts, Does.Contain("START"));
        Assert.That(canvas.RectCalls.FindAll(call => IsStartMarker(call, ruler, PlayfieldRenderer.StartPointColor)),
            Has.Count.EqualTo(2));
    }

    [Test]
    public void StartPointsOutsideTheVisibleWindowAreNotDrawn()
    {
        var (store, field, canvas, root) = StartPointScene();
        (_, long windowEnd) = EditorLayout.TimelineWindow(
            field.GetComponent<Playhead>(),
            field.GetComponent<TimelineView>().Zoom);
        store.CreateEntity(Start("outro", windowEnd + 1_000));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(canvas.Texts, Does.Not.Contain("outro"));
    }

    [Test]
    public void TheSelectedStartPointIsHighlightedOnTheTimeline()
    {
        var (store, field, canvas, root) = StartPointScene();
        Entity start = store.CreateEntity(Start("verse", 2_000));
        field.GetComponent<NoteSelection>() = new NoteSelection(start);

        root.Update(new UpdateTick(1f / 60f, 0));

        Rect2 ruler = EditorLayout.RulerRect(field.GetComponent<PlayfieldView>());
        Assert.That(canvas.RectCalls.Exists(call => IsStartMarker(call, ruler, Colors.White)), Is.True);
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
