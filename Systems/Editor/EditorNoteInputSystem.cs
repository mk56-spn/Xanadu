using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Input;

namespace Writer.Systems.Editor;

/// <summary>Adds, selects, drags, and scrubs notes in playfield coordinates.</summary>
public sealed class EditorNoteInputSystem : QuerySystem<Note>
{
    private const float NoteSize = 24;
    private const float HitRadius = NoteSize / 2;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly EditorHistory _history;

    private bool _wasPressed;
    private bool _scrubbing;
    private Entity _draggedNote;
    private Vector2 _dragOffset;
    private System.Collections.Generic.List<NoteSnapshot> _dragBefore = null!;

    public EditorNoteInputSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        EditorHistory history)
    {
        _entities = entities;
        _pointer = pointer;
        _history = history;
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;

        if (pressed && !_wasPressed)
            OnPress(input.Position);
        else if (pressed)
            OnHold(input.Position);
        else if (_wasPressed)
            OnRelease();

        _wasPressed = pressed;
    }

    private void OnPress(Vector2 screenPosition)
    {
        Entity editor = FindEditor();
        if (editor.IsNull || IsPreviewing(editor))
            return;

        EditorView view = editor.GetComponent<EditorView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        EditorPlayhead playhead = editor.GetComponent<EditorPlayhead>();

        Rect2 bar = EditorLayout.RulerRect(view);
        if (bar.HasPoint(screenPosition))
        {
            _scrubbing = true;
            Scrub(screenPosition, bar, playhead);
            return;
        }

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);
        if (!IsInside(playfieldPosition, playfield))
            return;

        Entity hit = FindNoteAt(playfieldPosition, playhead.TimeMs);
        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        if (!hit.IsNull)
        {
            Note note = hit.GetComponent<Note>();
            _draggedNote = hit;
            _dragOffset = note.Position - playfieldPosition;
            _dragBefore = _history.Capture();
            selection.Note = hit;
            return;
        }

        var before = _history.Capture();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        Entity created = _entities.CreateEntity(new Note(time, playfieldPosition));
        var after = _history.Capture();
        _history.Record(before, after);
        selection.Note = created;
    }

    private void OnHold(Vector2 screenPosition)
    {
        Entity editor = FindEditor();
        if (editor.IsNull || IsPreviewing(editor))
            return;

        EditorView view = editor.GetComponent<EditorView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        EditorPlayhead playhead = editor.GetComponent<EditorPlayhead>();

        if (_scrubbing)
        {
            Scrub(screenPosition, EditorLayout.RulerRect(view), playhead);
            return;
        }

        if (_draggedNote.IsNull || !_draggedNote.HasComponent<Note>())
        {
            CancelDrag();
            return;
        }

        Note note = _draggedNote.GetComponent<Note>();
        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition) + _dragOffset;
        note.Position = new Vector2(
            Math.Clamp(playfieldPosition.X, 0, playfield.Width),
            Math.Clamp(playfieldPosition.Y, 0, playfield.Height));
        _draggedNote.GetComponent<Note>() = note;
    }

    private void OnRelease()
    {
        if (_scrubbing)
        {
            _scrubbing = false;
            return;
        }

        if (_draggedNote.IsNull)
            return;

        var after = _history.Capture();
        _history.Record(_dragBefore, after);
        _draggedNote = default;
    }

    private void Scrub(Vector2 screenPosition, Rect2 bar, EditorPlayhead playhead)
    {
        if (playhead.DurationMs <= 0)
            return;

        float t = Math.Clamp((screenPosition.X - bar.Position.X) / bar.Size.X, 0f, 1f);
        long raw = (long)(playhead.DurationMs * t);
        playhead.SeekRequestMs = EditorTiming.Snap(raw, playhead);
    }

    private Entity FindNoteAt(Vector2 position, long playheadMs)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playheadMs) <= 0)
                continue;

            if (MathF.Abs(note.Position.X - position.X) <= HitRadius &&
                MathF.Abs(note.Position.Y - position.Y) <= HitRadius)
            {
                result = entity;
            }
        }

        return result;
    }

    private void CancelDrag()
    {
        _draggedNote = default;
        _dragBefore = null!;
    }

    private static bool IsPreviewing(Entity editor) =>
        editor.HasComponent<EditorPreview>() &&
        editor.GetComponent<EditorPreview>().Active;

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<EditorView, Playfield, EditorPlayhead>().ToEntityList())
            return entity;

        return default;
    }

    private static bool IsInside(Vector2 position, Playfield playfield) =>
        position.X >= 0 &&
        position.Y >= 0 &&
        position.X <= playfield.Width &&
        position.Y <= playfield.Height;
}
