using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Timeline;
using Writer.Levels;
using Writer.Systems.Canvas;
using Writer.Systems.Playfields;

namespace Writer.Systems.Editor;

/// <summary>Draws editor chrome, the shared playfield, and the editor timeline.</summary>
public sealed class EditorPlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, NoteSelection>
{
    public static readonly Color MainSnapColor = Colors.White;
    public static readonly Color HalfSnapColor = new("#ef4444");
    public static readonly Color QuarterSnapColor = new("#3b82f6");

    private static readonly Color EditorBackground = new("#1e1e1e");
    private static readonly Color PanelBackground = new("#111827");

    private readonly IRectCanvas _canvas;
    private readonly EntityStore _entities;
    private readonly Rid _editor;

    public EditorPlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Rid editor)
    {
        _entities = entities;
        _canvas = canvas;
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
            EditorPreview preview = entity.HasComponent<EditorPreview>()
                ? entity.GetComponent<EditorPreview>()
                : default;

            if (preview.Active)
                DrawPreview(view, playfield, playhead);
            else
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
                    entity.HasComponent<EditorSelectionBox>()
                        ? entity.GetComponent<EditorSelectionBox>()
                        : default);
        });
    }

    private void DrawEditor(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        NoteSelection selection,
        TimelineView timeline,
        WaveformPreview preview,
        EditorSelectionBox selectionBox)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        DrawChrome(view);

        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        PlayfieldRenderer.DrawField(_canvas, _editor, field, 1);
        PlayfieldRenderer.DrawGrid(_canvas, _editor, playfield, field);
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
        DrawSelectionOutlines(view, playhead, selection);
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
                PlayfieldRenderer.SelectedNoteColor,
                2);
            _canvas.DrawTextStyled(
                _editor,
                new Rect2(center - new Vector2(10, 10), new Vector2(20, 20)),
                "S",
                PlayfieldRenderer.SelectedNoteColor,
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

    private void DrawSelectionBox(EditorSelectionBox box)
    {
        if (!box.Active)
            return;

        Vector2 topLeft = new(
            Math.Min(box.Start.X, box.Current.X),
            Math.Min(box.Start.Y, box.Current.Y));
        var bounds = new Rect2(
            topLeft,
            new Vector2(
                Math.Abs(box.Current.X - box.Start.X),
                Math.Abs(box.Current.Y - box.Start.Y)));

        _canvas.DrawRect(
            _editor,
            bounds,
            new Color(0.25f, 0.55f, 1f, 0.12f));
        PlayfieldRenderer.DrawBorder(
            _canvas,
            _editor,
            bounds,
            new Color(0.45f, 0.7f, 1f, 0.9f),
            1);
    }

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

        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        DrawBpmMarker(ruler, playhead, windowStart, windowEnd);
        DrawWaveform(ruler, preview, windowStart, windowEnd);
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

        var notesByTime = new Dictionary<long, List<Entity>>();
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.TimeMs < windowStart || note.TimeMs > windowEnd)
                continue;

            long key = note.TimeMs;
            if (!notesByTime.TryGetValue(key, out List<Entity>? notes))
            {
                notes = new List<Entity>();
                notesByTime.Add(key, notes);
            }

            notes.Add(entity);
        }

        const int maxStack = 7;
        const float noteGap = 1f;
        float noteSquare = Math.Clamp(
            6f + timeline.Zoom * 0.0625f,
            6f,
            8f);

        foreach ((long timeMs, List<Entity> notes) in notesByTime)
        {
            float x = XForTime(ruler, windowStart, windowEnd, timeMs);
            int visible = Math.Min(notes.Count, maxStack);
            float totalHeight =
                visible * noteSquare + Math.Max(0, visible - 1) * noteGap;
            float startY = ruler.Position.Y +
                           (ruler.Size.Y - totalHeight) / 2;

            for (int i = 0; i < visible; i++)
            {
                var bounds = new Rect2(
                    x - noteSquare / 2,
                    startY + i * (noteSquare + noteGap),
                    noteSquare,
                    noteSquare);
                Note note = notes[i].GetComponent<Note>();
                _canvas.DrawRect(
                    _editor,
                    bounds,
                    PlayfieldRenderer.ColorFor(note));

                if (selection.Contains(notes[i]))
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

    private void DrawPreview(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            PlayfieldRenderer.PlayBackground);

        float size = MathF.Min(view.ScreenSize.X - 120, view.ScreenSize.Y - 100);
        float scale = size / playfield.Width;
        var previewView = new PlayfieldView(
            new Vector2((view.ScreenSize.X - size) / 2, 50),
            scale,
            view.ScreenSize);
        Rect2 field = PlayfieldRenderer.FieldRect(previewView, playfield);

        PlayfieldRenderer.DrawField(_canvas, _editor, field, 1);
        PlayfieldRenderer.DrawGrid(_canvas, _editor, playfield, field);
        PlayfieldRenderer.DrawNotes(
            _canvas,
            _editor,
            _entities,
            previewView,
            playfield,
            playhead,
            default);
        PlayfieldRenderer.DrawTransport(_canvas, _editor, field, playhead);
    }
}
