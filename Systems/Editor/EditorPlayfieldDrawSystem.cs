using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Editor;

/// <summary>Draws the editor playfield, header/footer chrome, and centered timeline.</summary>
public sealed class EditorPlayfieldDrawSystem :
    QuerySystem<EditorView, Playfield, EditorPlayhead, NoteSelection>
{
    private static readonly Color EditorBackground = new("#1e1e1e");
    private static readonly Color PlayfieldBackground = new("#0f172a");
    private static readonly Color PanelBackground = new("#111827");
    private static readonly Color BorderColor = new("#475569");
    private static readonly Color GridColor = new("#273449");
    private static readonly Color AxisColor = new("#64748b");
    private static readonly Color TitleColor = new("#facc15");
    private static readonly Color DimTextColor = new("#94a3b8");

    private const float NoteSize = 24;

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

            DrawEditor(view, playfield, playhead, selection, preview);
        });
    }

    private void DrawEditor(
        EditorView view,
        Playfield playfield,
        EditorPlayhead playhead,
        NoteSelection selection,
        EditorPreview preview)
    {
        if (preview.Active)
        {
            DrawPreview(view, playfield, playhead, selection);
            return;
        }

        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            EditorBackground);

        DrawChrome(view);

        var field = new Rect2(
            view.ScreenOrigin,
            new Vector2(playfield.Width * view.Scale, playfield.Height * view.Scale));

        _canvas.DrawRect(_editor, field, PlayfieldBackground);
        DrawBorder(field, BorderColor, 1);

        DrawGrid(view, playfield, field);
        DrawNotes(view, playhead, selection);
        DrawRuler(view, playhead, selection);
    }

    private void DrawChrome(EditorView view)
    {
        Rect2 header = EditorLayout.HeaderRect(view);
        _canvas.DrawRect(_editor, header, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(header.Position.X, header.End.Y - 1, header.Size.X, 1),
            BorderColor);

        Rect2 footer = EditorLayout.FooterRect(view);
        _canvas.DrawRect(_editor, footer, PanelBackground);
        _canvas.DrawRect(
            _editor,
            new Rect2(footer.Position.X, footer.Position.Y, footer.Size.X, 1),
            BorderColor);
    }

    private void DrawRuler(
        EditorView view,
        EditorPlayhead playhead,
        NoteSelection selection)
    {
        Rect2 ruler = EditorLayout.RulerRect(view);
        _canvas.DrawRect(_editor, ruler, PlayfieldBackground);
        DrawBorder(ruler, BorderColor, 1);

        if (playhead.DurationMs <= 0)
        {
            _canvas.DrawText(
                _editor,
                ruler,
                "Load a song to enable the timeline",
                DimTextColor);
            return;
        }

        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        int firstBeat = (int)Math.Ceiling(
            (0 - playhead.OffsetMs) / beatMs);
        int lastBeat = (int)Math.Floor(
            (playhead.DurationMs - playhead.OffsetMs) / beatMs);
        int beatTotal = Math.Max(1, lastBeat - firstBeat + 1);
        int step = Math.Max(1, (int)Math.Ceiling(beatTotal / 160.0));

        for (int beat = firstBeat; beat <= lastBeat; beat++)
        {
            bool measure = beat % 4 == 0;
            if (!measure && beat % step != 0)
                continue;

            double beatTime = playhead.OffsetMs + beat * beatMs;
            float x = ruler.Position.X +
                      (float)(beatTime / playhead.DurationMs) * ruler.Size.X;
            float height = measure ? 20 : 9;

            _canvas.DrawLine(
                _editor,
                new Vector2(x, ruler.End.Y - height),
                new Vector2(x, ruler.End.Y),
                measure ? AxisColor : GridColor,
                measure ? 2 : 1);
        }

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float x = ruler.Position.X +
                      Math.Clamp(note.TimeMs / (float)playhead.DurationMs, 0f, 1f) *
                      ruler.Size.X;

            Color color = !selection.Note.IsNull && selection.Note.Id == entity.Id
                ? new Color(1f, 0.9f, 0.3f)
                : new Color(1f, 0.55f, 0.15f);

            _canvas.DrawRect(
                _editor,
                new Rect2(x - 1.5f, ruler.Position.Y + 8, 3, ruler.Size.Y - 10),
                color);
        }

        float playheadX = ruler.Position.X +
                          Math.Clamp(
                              playhead.TimeMs / (float)playhead.DurationMs,
                              0f,
                              1f) * ruler.Size.X;

        _canvas.DrawLine(
            _editor,
            new Vector2(playheadX, ruler.Position.Y),
            new Vector2(playheadX, ruler.End.Y),
            Colors.White,
            2);
    }

    private void DrawPreview(
        EditorView view,
        Playfield playfield,
        EditorPlayhead playhead,
        NoteSelection selection)
    {
        _canvas.DrawRect(
            _editor,
            new Rect2(Vector2.Zero, view.ScreenSize),
            new Color("#090d16"));

        float size = MathF.Min(view.ScreenSize.X - 120, view.ScreenSize.Y - 100);
        float scale = size / playfield.Width;
        var previewView = new EditorView(
            new Vector2((view.ScreenSize.X - size) / 2, 50),
            scale,
            view.ScreenSize);
        var field = new Rect2(
            previewView.ScreenOrigin,
            new Vector2(playfield.Width * scale, playfield.Height * scale));

        _canvas.DrawRect(_editor, field, PlayfieldBackground);
        DrawBorder(field, BorderColor, 1);
        DrawGrid(previewView, playfield, field);
        DrawNotes(previewView, playhead, default);
        DrawTransport(field, playhead);
    }

    private void DrawNotes(EditorView view, EditorPlayhead playhead, NoteSelection selection)
    {
        float size = NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float alpha = NoteFade.Alpha(note, playhead.TimeMs);
            if (alpha <= 0)
                continue;

            Vector2 center = view.PlayfieldToScreen(note.Position);
            var bounds = new Rect2(
                center - new Vector2(size / 2, size / 2),
                new Vector2(size, size));

            _canvas.DrawRect(
                _editor,
                bounds,
                new Color(1f, 0.55f, 0.15f, alpha));

            if (!selection.Note.IsNull && selection.Note.Id == entity.Id)
            {
                DrawBorder(bounds, new Color(1f, 0.9f, 0.3f, alpha), 2);
            }
        }
    }

    private void DrawTransport(Rect2 field, EditorPlayhead playhead)
    {
        float barY = field.End.Y + 10;
        var bar = new Rect2(field.Position.X, barY, field.Size.X, 8);
        _canvas.DrawRect(_editor, bar, PlayfieldBackground);
        DrawBorder(bar, BorderColor, 1);

        if (playhead.DurationMs <= 0)
            return;

        float progress = Math.Clamp(
            playhead.TimeMs / (float)playhead.DurationMs,
            0f,
            1f);

        _canvas.DrawRect(
            _editor,
            new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)),
            TitleColor);

        float x = bar.Position.X + bar.Size.X * progress;
        _canvas.DrawLine(
            _editor,
            new Vector2(x, bar.Position.Y - 2),
            new Vector2(x, bar.End.Y + 2),
            Colors.White,
            2);
    }

    private void DrawGrid(EditorView view, Playfield playfield, Rect2 field)
    {
        const int divisions = 10;

        for (int i = 0; i <= divisions; i++)
        {
            float t = i / (float)divisions;
            float x = field.Position.X + field.Size.X * t;
            float y = field.Position.Y + field.Size.Y * t;
            bool center = i == divisions / 2;
            Color color = center ? AxisColor : GridColor;
            float width = center ? 2 : 1;

            _canvas.DrawLine(
                _editor,
                new Vector2(x, field.Position.Y),
                new Vector2(x, field.End.Y),
                color,
                width);

            _canvas.DrawLine(
                _editor,
                new Vector2(field.Position.X, y),
                new Vector2(field.End.X, y),
                color,
                width);
        }

        float labelY = field.End.Y - 20;
        DrawLeftText("0", field.Position.X + 6, labelY, DimTextColor, field.Size.X);
        DrawLeftText(
            (playfield.Width / 2).ToString(),
            field.Position.X + field.Size.X / 2 - 18,
            labelY,
            DimTextColor,
            field.Size.X);
        DrawLeftText(
            playfield.Width.ToString(),
            field.End.X - 46,
            labelY,
            DimTextColor,
            field.Size.X);
    }

    private void DrawLeftText(string text, float x, float y, Color color, float width) =>
        _canvas.DrawTextLeft(
            _editor,
            new Rect2(x, y, width - 32, 20),
            text,
            color);

    private void DrawBorder(Rect2 bounds, Color color, float width)
    {
        _canvas.DrawRect(_editor,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_editor,
            new Rect2(new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)), color);
        _canvas.DrawRect(_editor,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)), color);
        _canvas.DrawRect(_editor,
            new Rect2(new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)), color);
    }
}
