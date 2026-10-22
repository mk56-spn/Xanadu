using System.Collections.Generic;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Settings;
using Writer.Editor;
using Writer.Systems.Input;
using Writer.Systems.Playfields;

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
    private Entity _draggedEntity;
    private Vector2 _dragStartPointer;
    private readonly Dictionary<int, Vector2> _dragStartPositions = new();
    private EditorSnapshot _dragBefore;
    private bool _pendingCreate;
    private bool _selecting;
    private Vector2 _selectionStart;
    private Vector2 _selectionCurrent;

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
        if (editor.IsNull ||
            IsPreviewing(editor) ||
            IsWarningOpen() ||
            IsSceneConfirmationOpen())
        {
            return;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        _pendingCreate = false;
        _selecting = false;
        SetSelectionBox(editor, Vector2.Zero, Vector2.Zero, false);

        Rect2 bar = EditorLayout.RulerRect(view);
        if (bar.HasPoint(screenPosition))
        {
            _scrubbing = true;
            Scrub(screenPosition, bar, ref playhead, editor);
            return;
        }

        if (editor.HasComponent<EditorSidebar>() &&
            editor.GetComponent<EditorSidebar>().Bounds.HasPoint(screenPosition))
        {
            return;
        }

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);
        if (!IsInside(playfieldPosition, playfield))
            return;

        Entity hit = FindNoteAt(playfieldPosition, playhead.TimeMs);
        if (hit.IsNull)
            hit = FindMineAt(playfieldPosition, playhead.TimeMs);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        if (!hit.IsNull)
        {
            _draggedEntity = hit;
            _dragStartPointer = playfieldPosition;
            _dragStartPositions.Clear();
            _dragBefore = _history.Capture();

            if (!selection.Contains(hit))
                selection.Note = hit;

            foreach (Entity selected in selection.AllNotes)
            {
                if (selected.HasComponent<Note>())
                {
                    _dragStartPositions[selected.Id] =
                        selected.GetComponent<Note>().Position;
                }
                else if (selected.HasComponent<Mine>())
                {
                    _dragStartPositions[selected.Id] =
                        selected.GetComponent<Mine>().Position;
                }
            }

            if (hit.HasComponent<Note>() &&
                editor.HasComponent<EditorNoteTarget>())
            {
                editor.GetComponent<EditorNoteTarget>().Target =
                    hit.GetComponent<Note>().Target;
            }

            return;
        }

        _pendingCreate = true;
        _selectionStart = screenPosition;
        _selectionCurrent = screenPosition;
    }

    private void OnHold(Vector2 screenPosition)
    {
        Entity editor = FindEditor();
        if (editor.IsNull ||
            IsPreviewing(editor) ||
            IsWarningOpen() ||
            IsSceneConfirmationOpen())
        {
            return;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        if (_scrubbing)
        {
            Scrub(
                screenPosition,
                EditorLayout.RulerRect(view),
                ref playhead,
                editor);
            return;
        }

        if (_pendingCreate &&
            screenPosition.DistanceTo(_selectionStart) > 4f)
        {
            _pendingCreate = false;
            _selecting = true;
            editor.GetComponent<NoteSelection>().SetMany(
                System.Array.Empty<Entity>());
        }

        if (_selecting)
        {
            _selectionCurrent = screenPosition;
            SetSelectionBox(
                editor,
                _selectionStart,
                _selectionCurrent,
                true);
            editor.GetComponent<NoteSelection>().SetMany(
                FindNotesIn(
                    Rect2From(_selectionStart, _selectionCurrent),
                    playhead.TimeMs));
            return;
        }

        if (_draggedEntity.IsNull ||
            (!_draggedEntity.HasComponent<Note>() &&
             !_draggedEntity.HasComponent<Mine>()))
        {
            CancelDrag();
            return;
        }

        Vector2 pointer = view.ScreenToPlayfield(screenPosition);
        Vector2 delta = pointer - _dragStartPointer;
        foreach ((int id, Vector2 start) in _dragStartPositions)
        {
            Entity entity = _entities.GetEntityById(id);
            if (entity.IsNull ||
                (!entity.HasComponent<Note>() &&
                 !entity.HasComponent<Mine>()))
            {
                continue;
            }

            Vector2 target = new(
                Math.Clamp(start.X + delta.X, 0, playfield.Width),
                Math.Clamp(start.Y + delta.Y, 0, playfield.Height));
            if (entity.HasComponent<Note>())
            {
                ref Note note = ref entity.GetComponent<Note>();
                note.Position = target;
            }
            else
            {
                ref Mine mine = ref entity.GetComponent<Mine>();
                mine.Position = target;
            }
        }
    }

    private void OnRelease()
    {
        if (_scrubbing)
        {
            _scrubbing = false;
            return;
        }

        Entity editor = FindEditor();
        if (!editor.IsNull)
            SetSelectionBox(editor, Vector2.Zero, Vector2.Zero, false);

        if (_selecting)
        {
            _selecting = false;
            _pendingCreate = false;
            return;
        }

        if (_pendingCreate)
        {
            _pendingCreate = false;
            if (!editor.IsNull)
                CreatePlacementAt(editor, _selectionStart);
            return;
        }

        if (_draggedEntity.IsNull)
            return;

        var after = _history.Capture();
        _history.Record(_dragBefore, after);
        CancelDrag();
    }

    private void CreatePlacementAt(Entity editor, Vector2 screenPosition)
    {
        if (editor.HasComponent<EditorPlacementModeState>() &&
            editor.GetComponent<EditorPlacementModeState>().Mode ==
            EditorPlacementMode.Mines)
        {
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Playfield playfield = editor.GetComponent<Playfield>();
            Vector2 position = view.ScreenToPlayfield(screenPosition);
            if (!IsInside(position, playfield))
                return;

            Playhead playhead = editor.GetComponent<Playhead>();
            var before = _history.Capture();
            _entities.CreateEntity(new Mine(
                EditorTiming.Snap(playhead.TimeMs, playhead),
                position));
            _history.Record(before, _history.Capture());
            return;
        }

        CreateNoteAt(editor, screenPosition);
    }

    private void CreateNoteAt(Entity editor, Vector2 screenPosition)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        Playhead playhead = editor.GetComponent<Playhead>();
        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);
        if (!IsInside(playfieldPosition, playfield))
            return;

        if (string.IsNullOrEmpty(editor.GetComponent<LevelInfo>().AudioPath))
        {
            EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
            return;
        }

        var before = _history.Capture();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        KeybindAction target = editor.HasComponent<EditorNoteTarget>()
            ? editor.GetComponent<EditorNoteTarget>().Target
            : KeybindAction.M;
        EditorPlacementMode mode = editor.HasComponent<EditorPlacementModeState>()
            ? editor.GetComponent<EditorPlacementModeState>().Mode
            : EditorPlacementMode.Notes;
        DashDirection direction = editor.HasComponent<EditorDashDirectionState>()
            ? editor.GetComponent<EditorDashDirectionState>().Direction
            : DashDirection.Up;
        Entity created = _entities.CreateEntity(new Note(
            time,
            playfieldPosition,
            target: target,
            kind: mode == EditorPlacementMode.Movement
                ? NoteKind.Movement
                : NoteKind.Hit,
            dashDirection: direction));
        _history.Record(before, _history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
    }

    private List<Entity> FindNotesIn(Rect2 screenBounds, long playheadMs)
    {
        var selected = new List<Entity>();
        Entity editor = FindEditor();
        if (editor.IsNull)
            return selected;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        float noteSize = PlayfieldRenderer.NoteSize * view.Scale;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playheadMs) <= 0.05f ||
                !IsInside(note.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(note.Position);
            var noteBounds = new Rect2(
                center - new Vector2(noteSize / 2, noteSize / 2),
                new Vector2(noteSize, noteSize));
            if (screenBounds.Intersects(noteBounds))
                selected.Add(entity);
        }

        float mineSize = PlayfieldRenderer.MineSize * view.Scale;
        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0.05f ||
                !IsInside(mine.Position, playfield))
            {
                continue;
            }

            Vector2 center = view.PlayfieldToScreen(mine.Position);
            var mineBounds = new Rect2(
                center - new Vector2(mineSize / 2, mineSize / 2),
                new Vector2(mineSize, mineSize));
            if (screenBounds.Intersects(mineBounds))
                selected.Add(entity);
        }

        return selected;
    }

    private static Rect2 Rect2From(Vector2 start, Vector2 current)
    {
        Vector2 topLeft = new(
            Math.Min(start.X, current.X),
            Math.Min(start.Y, current.Y));
        return new Rect2(
            topLeft,
            new Vector2(
                Math.Abs(current.X - start.X),
                Math.Abs(current.Y - start.Y)));
    }

    private static void SetSelectionBox(
        Entity editor,
        Vector2 start,
        Vector2 current,
        bool active)
    {
        if (!editor.HasComponent<EditorSelectionBox>())
            return;

        ref EditorSelectionBox box = ref editor.GetComponent<EditorSelectionBox>();
        box.Start = start;
        box.Current = current;
        box.Active = active;
    }

    private void Scrub(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor)
    {
        if (playhead.DurationMs <= 0)
            return;

        float zoom = editor.HasComponent<EditorTimeline>()
            ? editor.GetComponent<EditorTimeline>().Zoom
            : 1f;
        (long start, long end) = EditorLayout.TimelineWindow(playhead, zoom);
        float t = Math.Clamp((screenPosition.X - bar.Position.X) / bar.Size.X, 0f, 1f);
        long raw = start + (long)((end - start) * t);
        playhead.SeekRequestMs = Math.Clamp(
            raw,
            0,
            Math.Max(0, playhead.DurationMs));
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

    private Entity FindMineAt(Vector2 position, long playheadMs)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            if (MineFade.Alpha(mine, playheadMs) <= 0)
                continue;

            if (MathF.Abs(mine.Position.X - position.X) <= HitRadius &&
                MathF.Abs(mine.Position.Y - position.Y) <= HitRadius)
            {
                result = entity;
            }
        }

        return result;
    }

    private void CancelDrag()
    {
        _draggedEntity = default;
        _dragBefore = default;
        _dragStartPositions.Clear();
    }

    private static bool IsPreviewing(Entity editor) =>
        editor.HasComponent<EditorPreview>() &&
        editor.GetComponent<EditorPreview>().Active;

    private bool IsWarningOpen()
    {
        foreach (Entity entity in _entities.Query<WarningPopup>().ToEntityList())
        {
            if (entity.GetComponent<WarningPopup>().Visible)
                return true;
        }

        return false;
    }

    private bool IsSceneConfirmationOpen()
    {
        foreach (Entity entity in _entities.Query<SceneConfirmation>().ToEntityList())
        {
            if (entity.GetComponent<SceneConfirmation>().Visible)
                return true;
        }

        return false;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities.Query<PlayfieldView, Playfield, Playhead, NoteSelection>().ToEntityList())
            return entity;

        return default;
    }

    private static bool IsInside(Vector2 position, Playfield playfield) =>
        position.X >= 0 &&
        position.Y >= 0 &&
        position.X <= playfield.Width &&
        position.Y <= playfield.Height;
}
