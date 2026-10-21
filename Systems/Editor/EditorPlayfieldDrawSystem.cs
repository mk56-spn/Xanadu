using System;
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
                DrawEditor(view, playfield, playhead, selection);
        });
    }

    private void DrawEditor(
        PlayfieldView view,
        Playfield playfield,
        Playhead playhead,
        NoteSelection selection)
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
        DrawRuler(view, playhead, selection);
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
    }

    private void DrawRuler(
        PlayfieldView view,
        Playhead playhead,
        NoteSelection selection)
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
                measure
                    ? PlayfieldRenderer.AxisColor
                    : PlayfieldRenderer.GridColor,
                measure ? 2 : 1);
        }

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            float x = ruler.Position.X +
                      Math.Clamp(note.TimeMs / (float)playhead.DurationMs, 0f, 1f) *
                      ruler.Size.X;

            Color color = !selection.Note.IsNull && selection.Note.Id == entity.Id
                ? PlayfieldRenderer.SelectedNoteColor
                : PlayfieldRenderer.NoteColor;

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
