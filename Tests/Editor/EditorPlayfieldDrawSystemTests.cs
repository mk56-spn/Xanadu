using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Playfields;
using Xanadu.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

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

    private static (RecordingCanvas Canvas, Rect2 Ruler) DrawTimeline(long durationMs, float zoom, WaveformData? data)
    {
        var view = new PlayfieldView(new Vector2(30, 30), 0.6f, new Vector2(1920, 1080));
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Wave", ""),
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false, durationMs: durationMs),
            new TimelineView(zoom),
            new WaveformPreview("", data),
            view,
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));
        root.Update(new UpdateTick(1f / 60f, 0));
        return (canvas, EditorLayout.RulerRect(view));
    }

    [Test]
    public void ALongSongZoomedOutDrawsBeatTicksWithRoomBetweenThem()
    {
        (RecordingCanvas canvas, Rect2 ruler) = DrawTimeline(600_000, 1f, null);

        // The playhead sits on the first tick at time zero, so that spot is left out.
        var ticks = canvas.Lines.FindAll(line =>
            line.From.X == line.To.X && line.To.Y == ruler.End.Y && line.From.X > ruler.Position.X);
        ticks.Sort((a, b) => a.From.X.CompareTo(b.From.X));
        Assert.That(ticks, Is.Not.Empty);
        for (int i = 1; i < ticks.Count; i++)
            Assert.That(ticks[i].From.X - ticks[i - 1].From.X, Is.GreaterThanOrEqualTo(15f));
    }

    [Test]
    public void EachBandOfTheSongIsDrawnCentredInsideTheRuler()
    {
        var data = new WaveformData(
            new[] { 1f, 1f }, new[] { 1f, 1f }, new[] { 1f, 1f }, durationMs: 4_000);

        (RecordingCanvas canvas, Rect2 ruler) = DrawTimeline(4_000, 1f, data);

        foreach (Color band in new[] { Palette.WaveLow, Palette.WaveMid, Palette.WaveHigh })
        {
            var columns = canvas.RectCalls.FindAll(call => call.Color == band);
            Assert.That(columns, Is.Not.Empty);
            foreach (RecordingCanvas.RectCall column in columns)
            {
                Assert.That(ruler.Encloses(column.Bounds), Is.True);
                Assert.That(column.Bounds.GetCenter().Y, Is.EqualTo(ruler.GetCenter().Y).Within(0.01f));
            }
        }
    }

    [Test]
    public void SilenceDrawsNoBands()
    {
        var data = new WaveformData(new float[2], new float[2], new float[2], durationMs: 4_000);

        (RecordingCanvas canvas, _) = DrawTimeline(4_000, 1f, data);

        Assert.That(canvas.RectCalls.Exists(call => call.Color == Palette.WaveLow), Is.False);
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
            canvas.Lines.Any(line => line.Color == EditorTimelineDrawer.MainSnapColor),
            Is.True);
        Assert.That(
            canvas.Lines.Any(line => line.Color == EditorTimelineDrawer.HalfSnapColor),
            Is.True);
        Assert.That(
            canvas.Lines.Any(line => line.Color == EditorTimelineDrawer.QuarterSnapColor),
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
            .Where(rect => rect.Size == Vector2.One * EditorTimelineNotes.SquareSize(32f))
            .ToList();
        Assert.That(squares, Has.Count.EqualTo(2));
        Assert.That(squares[0].Position.X, Is.EqualTo(squares[1].Position.X));
        Assert.That(squares[0].Position.Y, Is.Not.EqualTo(squares[1].Position.Y));
    }

    [Test]
    public void DashNotesDrawTheirLanding()
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
            kind: NoteKind.Dash,
            dashDirection: DashDirection.Right));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        // The exit is a half-strength ring one dash to the right.
        Vector2 exit = new(350, 100);
        float radius = PlayfieldRenderer.NoteSize / 2;
        Assert.That(
            canvas.Lines.Any(line =>
                Mathf.Abs(line.From.DistanceTo(exit) - radius) < 0.5f &&
                Mathf.Abs(line.To.DistanceTo(exit) - radius) < 0.5f &&
                Mathf.IsEqualApprox(line.Color.A, PlayfieldRenderer.DashLinkAlpha)),
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
                target: Xanadu.Settings.KeybindAction.R1));
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
            Xanadu.Settings.KeybindAction.R1);
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
            .Where(rect => rect.Size == Vector2.One * EditorTimelineNotes.SquareSize(32f))
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
    public void TheMapsBackgroundColourFillsTheField()
    {
        var view = new PlayfieldView(new Vector2(276, 88), 0.404f, new Vector2(1152, 648));
        var playfield = new Playfield(1000, 1000);
        var store = new EntityStore();
        store.CreateEntity(
            new LevelInfo("Shaded", "") { Background = new Color("#204060") },
            playfield,
            new Playhead(0, 120, 0, false),
            view,
            new NoteSelection(default));

        var canvas = new RecordingCanvas();
        var root = new SystemRoot(store);
        root.Add(new EditorPlayfieldDrawSystem(store, canvas, default));

        root.Update(new UpdateTick(1f / 60f, 0));

        Rect2 field = EditorLayout.PlayfieldRect(view, playfield);
        Assert.That(
            canvas.RectCalls.Exists(call =>
                call.Bounds == field &&
                call.Color == new Color("#204060")),
            Is.True);
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
                call.Bounds.Position.IsEqualApprox(expected.Position) &&
                Mathf.IsEqualApprox(call.Bounds.Size.X, expected.Size.X)),
            Is.True);
        Assert.That(
            canvas.RectCalls.FindAll(call =>
                call.Color == PlayfieldRenderer.HandleEdge &&
                call.Bounds.Size == new Vector2(10, 10)),
            Has.Count.EqualTo(4));
    }

    [Test]
    public void ABlockWithNoOutlineIsAllInterior()
    {
        var canvas = new RecordingCanvas();
        var screen = new Rect2(10, 10, 100, 50);

        PlayfieldRenderer.DrawBlockBody(canvas, default, screen, 0f, Colors.White, Colors.Red);

        Assert.That(canvas.RectCalls, Has.Count.EqualTo(1));
        Assert.That(canvas.RectCalls[0].Bounds, Is.EqualTo(screen));
        Assert.That(canvas.RectCalls[0].Color, Is.EqualTo(Colors.White));
    }

    [Test]
    public void AnOutlineTakesItsWidthOffTheInterior()
    {
        var canvas = new RecordingCanvas();
        var screen = new Rect2(10, 10, 100, 50);

        PlayfieldRenderer.DrawBlockBody(canvas, default, screen, 4f, Colors.White, Colors.Red);

        Assert.That(canvas.RectCalls.Exists(call => call.Bounds == screen.Grow(-4f) && call.Color == Colors.White), Is.True);
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

    [TestCase(true)]
    [TestCase(false)]
    public void ThePathToggleShowsOrHidesThePreviewPath(bool visible)
    {
        var (store, field, canvas, root) = StartPointScene();
        field.AddComponent(new PlayerPath(
            new List<PlayerPathSample>
            {
                new(new Vector2(100, 100), 0, false),
                new(new Vector2(200, 100), 100, false),
            },
            new Vector2(30, 200)));
        Entity button = store.CreateEntity();
        PlayerPathSwitch.Attach(button);
        button.GetComponent<Toggle>().Value = visible;

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Exists(line => line.Color.G > line.Color.R && line.Color.G > line.Color.B && line.Color.A > 0.7f),
            Is.EqualTo(visible));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TheFrameANoteWasHitIsOutlinedInItsOwnColour(bool hit)
    {
        var (store, field, canvas, root) = StartPointScene();
        field.AddComponent(new PlayerPath(
            new List<PlayerPathSample>
            {
                new(new Vector2(100, 100), 0, false),
                new(new Vector2(200, 100), 100, false, hit),
            },
            new Vector2(30, 200)));
        Entity button = store.CreateEntity();
        PlayerPathSwitch.Attach(button);

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.RectCalls.Exists(call => call.Color == EditorPathDrawer.HitColor),
            Is.EqualTo(hit));
    }

    [Test]
    public void ASelectedNoteKeepsItsOutlineAfterItFadesOut()
    {
        var (store, field, canvas, root) = StartPointScene();
        Entity note = store.CreateEntity(new Note(0, new Vector2(100, 100)));
        field.GetComponent<NoteSelection>() = new NoteSelection(note);

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Exists(line => line.Color == PlayfieldRenderer.SelectedNoteColor),
            Is.True);
    }

    [Test]
    public void ASelectedMineKeepsItsOutlineAfterItFadesOut()
    {
        var (store, field, canvas, root) = StartPointScene();
        Entity mine = store.CreateEntity(new Mine(0, new Vector2(100, 100)));
        field.GetComponent<NoteSelection>() = new NoteSelection(mine);

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Exists(line => line.Color == PlayfieldRenderer.SelectedNoteColor),
            Is.True);
    }

    [Test]
    public void AnUnselectedNoteHasNoOutlineOnceItFadesOut()
    {
        var (store, _, canvas, root) = StartPointScene();
        store.CreateEntity(new Note(0, new Vector2(100, 100)));

        root.Update(new UpdateTick(1f / 60f, 0));

        Assert.That(
            canvas.Lines.Exists(line => line.Color == PlayfieldRenderer.SelectedNoteColor),
            Is.False);
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

        public void DrawTextWeighted(
            Rid canvas,
            Rect2 bounds,
            string text,
            Color color,
            int fontSize,
            TextAlign align,
            bool bold) =>
            Texts.Add(text);

        public readonly record struct RectCall(
            Rect2 Bounds,
            Color Color);

        public readonly record struct LineCall(
            Vector2 From,
            Vector2 To,
            Color Color);
    }
}
