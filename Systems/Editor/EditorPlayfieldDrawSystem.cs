using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Shaders;
using Xanadu.Timeline;
using Xanadu.Levels;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>Draws editor chrome, the shared playfield, and the editor timeline.</summary>
public sealed partial class EditorPlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, NoteSelection>
{
    public static readonly Color MainSnapColor = Colors.White;
    public static readonly Color HalfSnapColor = new("#ef4444");
    public static readonly Color QuarterSnapColor = new("#3b82f6");

    private static readonly Color EditorBackground = new("#1e1e1e");
    private static readonly Color PanelBackground = new("#111827");

    private readonly ShaderRoutingCanvas _canvas;
    private readonly GodotShaderCanvasItems _shaderItems = new();
    private readonly EntityStore _entities;
    private readonly Rid _editor;

    public EditorPlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Rid editor)
    {
        _entities = entities;
        _canvas = new ShaderRoutingCanvas(canvas, editor, _shaderItems);
        _editor = editor;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_editor);

        Query.ForEachEntity((
            ref view,
            ref playfield,
            ref playhead,
            ref selection,
            entity) =>
        {
            if (entity.TryGetComponent(out LevelShaders shaders))
            {
                LevelShaderMaterials.Ensure(shaders);
                _shaderItems.MaterialOf = slot => LevelShaderMaterials.MaterialOf(shaders, slot);
            }

            _analysis = entity.HasComponent<EditorAnalysis>()
                ? entity.GetComponent<EditorAnalysis>()
                : default;
            DrawEditor(
                    view,
                    playfield,
                    playhead,
                    selection,
                    entity.HasComponent<TimelineView>()
                        ? entity.GetComponent<TimelineView>()
                        : new TimelineView(1f),
                    entity.HasComponent<WaveformPreview>()
                        ? entity.GetComponent<WaveformPreview>()
                        : default,
                    entity.HasComponent<UiMarqueeBox>()
                        ? entity.GetComponent<UiMarqueeBox>()
                        : default,
                    entity.HasComponent<LevelGroups>()
                        ? entity.GetComponent<LevelGroups>()
                        : default,
                    entity.HasComponent<EditorGroupState>()
                        ? entity.GetComponent<EditorGroupState>()
                        : EditorGroupState.Closed);
        });
    }

    private void DrawEditor(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        NoteSelection selection,
        TimelineView timeline,
        WaveformPreview preview,
        UiMarqueeBox selectionBox,
        LevelGroups groups,
        EditorGroupState groupState)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        DrawChrome(view);

        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        PlayfieldRenderer.DrawField(_canvas, _editor, field, 1);
        PlayfieldRenderer.DrawGrid(_canvas, _editor, playfield, field);
        PlayfieldRenderer.DrawBlocks(_canvas, _editor, _entities, view, selection.Note);
        PlayfieldRenderer.DrawNotes(
            _canvas,
            _editor,
            _entities,
            view,
            playfield,
            playhead,
            selection.Note);
        PlayfieldRenderer.DrawMines(
            _canvas,
            _editor,
            _entities,
            view,
            playhead);
        DrawStartPoints(view, selection);
        DrawDashDestinations(view, playhead);
        if (groupState.Preview)
            DrawGroupPreview(view, playhead, groups, groupState);
        DrawSelectionOutlines(view, playhead, selection);
        if (groupState.TargetEdit)
            DrawGroupTarget(view, playfield, groups, groupState, selection);
        DrawBlockHandles(view, selection);
        DrawRuler(view, playhead, selection, timeline, preview);
        DrawSelectionBox(selectionBox);
    }

    private void DrawDashDestinations(
        PlayfieldView view,
        Playhead playhead)
    {
        float size = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind != NoteKind.Movement ||
                NoteFade.Alpha(note, playhead.TimeMs) <= 0)
            {
                continue;
            }

            Vector2 from = view.PlayfieldToScreen(note.Position);
            Vector2 target = PlayerMovementSystem.DashTarget(
                note.Position,
                note.DashDirection);
            Vector2 to = view.PlayfieldToScreen(target);
            Color color = PlayfieldRenderer.ColorFor(note.Target);
            color.A = 0.55f;

            _canvas.DrawLine(_editor, from, to, color, 2);
            _canvas.DrawCircleOutline(
                _editor,
                to,
                size / 2,
                color,
                2);
            _canvas.DrawTextStyled(
                _editor,
                new Rect2(
                    to - new Vector2(size / 2, size / 2),
                    new Vector2(size, size)),
                PlayfieldRenderer.DashArrow(note.DashDirection),
                color,
                Mathf.Max(10, (int)(size * 0.7f)));
        }
    }

    private void DrawStartPoints(PlayfieldView view, NoteSelection selection)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(start.Position);
            _canvas.DrawCircleOutline(
                _editor,
                center,
                13,
                PlayfieldRenderer.StartPointColor,
                2);
            _canvas.DrawTextStyled(
                _editor,
                new Rect2(center - new Vector2(10, 10), new Vector2(20, 20)),
                "S",
                PlayfieldRenderer.StartPointColor,
                12);
            if (selection.Contains(entity))
            {
                _canvas.DrawCircleOutline(
                    _editor,
                    center,
                    17,
                    Colors.White,
                    2);
            }

            if (entity.TryGetComponent(out ItemGroups groups) && groups.Mask != 0)
            {
                PlayfieldRenderer.DrawGroupBadge(
                    _canvas,
                    _editor,
                    center + new Vector2(15, -15),
                    groups.Mask,
                    1f);
            }
        }
    }

    private void DrawSelectionOutlines(
        PlayfieldView view,
        Playhead playhead,
        NoteSelection selection)
    {
        float size = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playhead.TimeMs) <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(note.Position);
            _canvas.DrawCircleOutline(
                _editor,
                center,
                size / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            if (!selection.Contains(entity))
                continue;

            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playhead.TimeMs) <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(mine.Position);
            _canvas.DrawCircleOutline(
                _editor,
                center,
                mineSize / 2 + 1.5f,
                PlayfieldRenderer.SelectedNoteColor,
                2);
        }
    }

    private void DrawSelectionBox(UiMarqueeBox box) =>
        CanvasMarquee.Draw(_canvas, _editor, box);

    private void DrawChrome(PlayfieldView view)
    {
        Rect2 header = EditorLayout.HeaderRect(view);
        _canvas.DrawRect(_editor, header, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(header.Position.X, header.End.Y - 1, header.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        Rect2 footer = EditorLayout.FooterRect(view);
        _canvas.DrawRect(_editor, footer, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(footer.Position.X, footer.Position.Y, footer.Size.X, 1),
            PlayfieldRenderer.BorderColor);

        DrawZoomLens(view);
    }

    private void DrawBpmMarker(
        Rect2 ruler,
        Playhead playhead,
        long windowStart,
        long windowEnd)
    {
        if (playhead.OffsetMs < windowStart ||
            playhead.OffsetMs > windowEnd)
        {
            return;
        }

        float x = XForTime(
            ruler,
            windowStart,
            windowEnd,
            playhead.OffsetMs);
        _canvas.DrawRect(
            _editor,
            new Rect2(x, ruler.Position.Y, 2, ruler.Size.Y),
            PlayfieldRenderer.SelectedNoteColor);

        string label = $"{playhead.Bpm} BPM";
        float labelX = Mathf.Clamp(
            x + 4,
            ruler.Position.X + 2,
            ruler.End.X - 20);
        var labelBounds = new Rect2(
            labelX,
            ruler.Position.Y + 2,
            18,
            Mathf.Max(ruler.Size.Y - 4, label.Length * 11));
        _canvas.DrawRect(
            _editor,
            labelBounds,
            new Color(0.04f, 0.06f, 0.1f, 0.85f));
        _canvas.DrawTextVertical(
            _editor,
            labelBounds,
            label,
            PlayfieldRenderer.SelectedNoteColor,
            11);
    }

    /// <summary>
    /// Each start point as a full-height line on the ruler with its name
    /// tagged to the left, so it does not cover a BPM label at the same time.
    /// </summary>
    private void DrawStartMarkers(
        Rect2 ruler,
        NoteSelection selection,
        long windowStart,
        long windowEnd)
    {
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            if (start.TimeMs < windowStart || start.TimeMs > windowEnd)
                continue;

            Color color = selection.Contains(entity)
                ? Colors.White
                : PlayfieldRenderer.StartPointColor;
            float x = XForTime(ruler, windowStart, windowEnd, start.TimeMs);
            _canvas.DrawRect(
                _editor,
                new Rect2(x - 1, ruler.Position.Y, 2, ruler.Size.Y),
                color);

            string label = StartLabel(start);
            float labelX = Mathf.Clamp(
                x - 20,
                ruler.Position.X + 2,
                ruler.End.X - 20);
            var labelBounds = new Rect2(
                labelX,
                ruler.Position.Y + 2,
                18,
                Mathf.Min(ruler.Size.Y - 4, label.Length * 11));
            _canvas.DrawRect(
                _editor,
                labelBounds,
                new Color(0.04f, 0.06f, 0.1f, 0.85f));
            _canvas.DrawTextVertical(_editor, labelBounds, label, color, 11);
        }
    }

    /// <summary>The start point's name, or "START" when it has none.</summary>
    public static string StartLabel(StartPoint start) =>
        string.IsNullOrWhiteSpace(start.Name) ? "START" : start.Name;

    private void DrawBeatBands(
        Rect2 ruler,
        Playhead playhead,
        long windowStart,
        long windowEnd)
    {
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        double first = Math.Floor(
            (windowStart - playhead.OffsetMs) / beatMs);
        double last = Math.Ceiling(
            (windowEnd - playhead.OffsetMs) / beatMs);

        for (int beat = (int)first; beat < last; beat++)
        {
            double start = playhead.OffsetMs + beat * beatMs;
            double end = start + beatMs;
            if (end < windowStart || start > windowEnd)
                continue;

            float x0 = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Max(start, windowStart));
            float x1 = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Min(end, windowEnd));
            bool accent = beat % 4 == 0;
            _canvas.DrawRect(
                _editor,
                new Rect2(
                    x0,
                    ruler.Position.Y + 1,
                    Math.Max(1f, x1 - x0 - 1),
                    ruler.Size.Y - 2),
                new Color(1f, 1f, 1f, accent ? 0.028f : 0.012f));
        }
    }

    private void DrawWaveform(
        Rect2 ruler,
        WaveformPreview preview,
        long windowStart,
        long windowEnd)
    {
        if (preview.Left is null ||
            preview.Right is null ||
            preview.Left.Length == 0 ||
            preview.Right.Length == 0 ||
            preview.DurationMs <= 0)
        {
            return;
        }

        float centerY = ruler.Position.Y + ruler.Size.Y / 2;
        float maxHeight = (ruler.Size.Y - 12f) / 2f;
        int columns = Math.Min(480, Math.Max(32, (int)ruler.Size.X));
        float columnWidth = ruler.Size.X / columns;
        var color = new Color(0.35f, 0.68f, 0.98f, 0.16f);

        for (int i = 0; i < columns; i++)
        {
            long sampleStart = windowStart +
                (long)((windowEnd - windowStart) * i / (double)columns);
            long sampleEnd = windowStart +
                (long)((windowEnd - windowStart) * (i + 1) / (double)columns);
            int first = Math.Clamp(
                (int)(sampleStart * preview.Left.Length / preview.DurationMs),
                0,
                preview.Left.Length - 1);
            int last = Math.Clamp(
                (int)(sampleEnd * preview.Left.Length / preview.DurationMs),
                0,
                preview.Left.Length - 1);
            if (last < first)
                last = first;

            float left = 0f;
            float right = 0f;
            int stride = Math.Max(1, (last - first + 1) / 16);
            for (int sample = first; sample <= last; sample += stride)
            {
                left = Math.Max(left, preview.Left[sample]);
                right = Math.Max(
                    right,
                    preview.Right[Math.Min(sample, preview.Right.Length - 1)]);
            }

            float top = centerY - left * maxHeight;
            float bottom = centerY + right * maxHeight;
            _canvas.DrawRect(
                _editor,
                new Rect2(
                    ruler.Position.X + i * columnWidth,
                    top,
                    Math.Max(1f, columnWidth),
                    Math.Max(1f, bottom - top)),
                color);
        }

        _canvas.DrawLine(
            _editor,
            new Vector2(ruler.Position.X, centerY),
            new Vector2(ruler.End.X, centerY),
            new Color(color.R, color.G, color.B, 0.22f),
            1);
    }

    private void DrawZoomLens(PlayfieldView view)
    {
        Vector2 center = EditorLayout.ZoomLensCenter(view);
        Color color = PlayfieldRenderer.DimTextColor;
        const float radius = 5f;

        _canvas.DrawLine(
            _editor,
            center + new Vector2(-radius, -radius),
            center + new Vector2(radius, -radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(radius, -radius),
            center + new Vector2(radius, radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(radius, radius),
            center + new Vector2(-radius, radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(-radius, radius),
            center + new Vector2(-radius, -radius),
            color,
            1);
        _canvas.DrawLine(
            _editor,
            center + new Vector2(4, 4),
            center + new Vector2(9, 9),
            color,
            2);
    }

    private void DrawRuler(
        PlayfieldView view,
        Playhead playhead,
        NoteSelection selection,
        TimelineView timeline,
        WaveformPreview preview)
    {
        Rect2 ruler = EditorLayout.RulerRect(view);
        _canvas.DrawRect(_editor, ruler, PlayfieldRenderer.PlayfieldBackground);
        PlayfieldRenderer.DrawBorder(
            _canvas,
            _editor,
            ruler,
            PlayfieldRenderer.BorderColor,
            1);

        if (playhead.DurationMs <= 0)
        {
            _canvas.DrawText(
                _editor,
                ruler,
                "Load a song to enable the timeline",
                PlayfieldRenderer.DimTextColor);
            return;
        }

        (long windowStart, long windowEnd) = timeline.Scrubbing
            ? (timeline.ScrubStartMs, timeline.ScrubEndMs)
            : EditorLayout.TimelineWindow(playhead, timeline.Zoom);

        DrawAnalysis(playhead, EditorLayout.AnalysisRect(view), windowStart, windowEnd);
        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        DrawWaveform(ruler, preview, windowStart, windowEnd);
        DrawStartMarkers(ruler, selection, windowStart, windowEnd);
        int subdivisions = Math.Max(1, playhead.SnapDivision);
        double tickMs = TimingGrid.TickMs(playhead.Bpm, subdivisions);

        float pixelsPerTick = ruler.Size.X *
                              (float)tickMs /
                              Math.Max(1f, windowEnd - windowStart);
        int skip = 1;
        while (pixelsPerTick / skip < 3f)
            skip *= 2;

        int firstTick = TimingGrid.FirstTick(
            windowStart, playhead.OffsetMs, tickMs);
        int lastTick = TimingGrid.LastTick(
            windowEnd, playhead.OffsetMs, tickMs);

        for (int tick = firstTick; tick <= lastTick; tick++)
        {
            int sub = tick % subdivisions;
            if (sub < 0)
                sub += subdivisions;

            bool main = sub == 0;
            bool half = sub * 2 == subdivisions;
            bool quarter = sub * 4 == subdivisions ||
                           sub * 4 == subdivisions * 3;

            if (!main && !half && !quarter && sub % skip != 0)
                continue;

            double tickTime = TimingGrid.TickTime(
                tick, playhead.OffsetMs, tickMs);
            if (tickTime < windowStart - 1 || tickTime > windowEnd + 1)
                continue;

            float x = XForTime(
                ruler,
                windowStart,
                windowEnd,
                (long)Math.Round(tickTime));
            float height = main ? 22f : half ? 15f : quarter ? 10f : 5f;
            Color color = main
                ? MainSnapColor
                : half
                    ? HalfSnapColor
                    : quarter
                        ? QuarterSnapColor
                        : PlayfieldRenderer.GridColor;

            _canvas.DrawLine(
                _editor,
                new Vector2(x, ruler.End.Y - height),
                new Vector2(x, ruler.End.Y),
                color,
                main || half ? 2 : 1);
        }

        foreach ((Entity entity, Rect2 bounds) in EditorTimelineNotes.Slots(
                     _entities, ruler, windowStart, windowEnd, timeline.Zoom))
        {
            _canvas.DrawRect(
                _editor,
                bounds,
                PlayfieldRenderer.ColorFor(entity.GetComponent<Note>()));

            if (selection.Contains(entity))
            {
                PlayfieldRenderer.DrawBorder(
                    _canvas,
                    _editor,
                    new Rect2(
                        bounds.Position - new Vector2(1.5f, 1.5f),
                        bounds.Size + new Vector2(3, 3)),
                    PlayfieldRenderer.SelectedNoteColor,
                    1.5f);
            }
        }

        float playheadX = XForTime(
            ruler,
            windowStart,
            windowEnd,
            Math.Clamp(playhead.TimeMs, windowStart, windowEnd));

        _canvas.DrawLine(
            _editor,
            new Vector2(playheadX, ruler.Position.Y),
            new Vector2(playheadX, ruler.End.Y),
            Colors.White,
            2);
        _canvas.DrawRect(
            _editor,
            new Rect2(
                playheadX - 5f,
                ruler.Position.Y - 4f,
                10f,
                10f),
            Colors.White);
    }

    private static float XForTime(
        Rect2 ruler,
        long windowStart,
        long windowEnd,
        long timeMs) =>
        new TimeWindow(windowStart, windowEnd).XForTime(ruler, timeMs);
}
