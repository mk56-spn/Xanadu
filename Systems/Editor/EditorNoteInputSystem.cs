using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Input;

namespace Writer.Systems.Editor;

/// <summary>Adds, selects, and hit-tests notes in playfield coordinates.</summary>
public sealed class EditorNoteInputSystem : QuerySystem<Note>
{
    private const float NoteSize = 24;
    private const float HitRadius = NoteSize / 2;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;

    private bool _wasPressed;

    public EditorNoteInputSystem(EntityStore entities, IPointerInputSource pointer)
    {
        _entities = entities;
        _pointer = pointer;
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;

        if (pressed && !_wasPressed)
            HandleClick(input.Position);

        _wasPressed = pressed;
    }

    private void HandleClick(Vector2 screenPosition)
    {
        Entity editor = FindEditor();
        if (editor.IsNull)
            return;

        EditorView view = editor.GetComponent<EditorView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        EditorPlayhead playhead = editor.GetComponent<EditorPlayhead>();

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);
        if (playfieldPosition.X < 0 ||
            playfieldPosition.Y < 0 ||
            playfieldPosition.X > playfield.Width ||
            playfieldPosition.Y > playfield.Height)
        {
            return;
        }

        Entity hit = FindNoteAt(playfieldPosition);
        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        if (!hit.IsNull)
        {
            selection.Note = hit;
            return;
        }

        long time = SnapTime(playhead);
        Entity note = _entities.CreateEntity(new Note(time, playfieldPosition));
        selection.Note = note;
    }

    private Entity FindNoteAt(Vector2 position)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (MathF.Abs(note.Position.X - position.X) <= HitRadius &&
                MathF.Abs(note.Position.Y - position.Y) <= HitRadius)
            {
                result = entity;
            }
        }

        return result;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<EditorView, Playfield, EditorPlayhead>().ToEntityList())
            return entity;

        return default;
    }

    private static long SnapTime(EditorPlayhead playhead)
    {
        double beatMs = 60_000.0 / Math.Max(1, playhead.Bpm);
        double step = beatMs * 4.0 / Math.Max(1, playhead.SnapDivision);
        double relative = playhead.TimeMs - playhead.OffsetMs;
        double snapped = Math.Round(relative / step) * step + playhead.OffsetMs;
        return Math.Max(0, (long)snapped);
    }
}
