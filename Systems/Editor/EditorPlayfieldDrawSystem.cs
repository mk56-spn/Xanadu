using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
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
                    entity.HasComponent<EditorTimeline>()
                        ? entity.GetComponent<EditorTimeline>().Zoom
                        : 1f,
                    entity.HasComponent<WaveformPreview>()
                        ? entity.GetComponent<WaveformPreview>()
                        : default);
        });
    }

    private void DrawEditor(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        NoteSelection selection,
        float timelineZoom,
        WaveformPreview preview)
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
        DrawRuler(view, playhead, selection, timelineZoom, preview);
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
        float timelineZoom,
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

        (long windowStart, long windowEnd) =
            EditorLayout.TimelineWindow(playhead, timelineZoom);

        DrawBeatBands(ruler, playhead, windowStart, windowEnd);
        DrawWaveform(ruler, preview, windowStart, windowEnd);
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        int subdivisions = Math.Max(1, playhead.SnapDivision);
        double tickMs = beatMs / subdivisions;

        float pixelsPerTick = ruler.Size.X *
                              (float)tickMs /
                              Math.Max(1f, windowEnd - windowStart);
        int skip = 1;
        while (pixelsPerTick / skip < 3f)
            skip *= 2;

        int firstTick = (int)Math.Floor(
            (windowStart - playhead.OffsetMs) / tickMs);
        int lastTick = (int)Math.Ceiling(
            (windowEnd - playhead.OffsetMs) / tickMs);

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

            double tickTime = playhead.OffsetMs + tick * tickMs;
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
            6f + timelineZoom * 0.0625f,
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
                Color color =
                    !selection.Note.IsNull &&
                    selection.Note.Id == notes[i].Id
                        ? PlayfieldRenderer.SelectedNoteColor
                        : PlayfieldRenderer.NoteColor;

                _canvas.DrawRect(
                    _editor,
                    new Rect2(
                        x - noteSquare / 2,
                        startY + i * (noteSquare + noteGap),
                        noteSquare,
                        noteSquare),
                    color);
            }
        }

        foreach (Entity entity in _entities.Query<MovementEvent>().ToEntityList())
        {
            MovementEvent movement = entity.GetComponent<MovementEvent>();
            if (movement.TimeMs < windowStart || movement.TimeMs > windowEnd)
                continue;

            float x = XForTime(
                ruler,
                windowStart,
                windowEnd,
                movement.TimeMs);

            _canvas.DrawRect(
                _editor,
                new Rect2(x - 3f, ruler.Position.Y + 2, 6, 6),
                PlayfieldRenderer.QueueColor);
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
    }

    private static float XForTime(
        Rect2 ruler,
        long windowStart,
        long windowEnd,
        long timeMs)
    {
        float span = Math.Max(1, windowEnd - windowStart);
        float t = Math.Clamp((timeMs - windowStart) / span, 0f, 1f);
        return ruler.Position.X + t * ruler.Size.X;
    }

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
