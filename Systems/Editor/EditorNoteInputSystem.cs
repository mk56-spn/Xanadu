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
    private long _scrubWindowStart;
    private long _scrubWindowEnd;
    private bool _timelineDragging;
    private bool _timelineDragMoved;
    private float _timelineDragStartX;
    private Entity _timelinePrimary;
    private EditorSnapshot _timelineBefore;
    private readonly Dictionary<int, long> _timelineStartTimes = new();
    private Entity _draggedEntity;
    private Vector2 _dragStartPointer;
    private readonly Dictionary<int, Vector2> _dragStartPositions = new();
    private EditorSnapshot _dragBefore;
    private bool _pendingCreate;
    private bool _selecting;
    private bool _additiveSelection;
    private readonly List<Entity> _selectionBase = new();
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
            OnPress(input.Position, input.Shift);
        else if (pressed)
            OnHold(input.Position);
        else if (_wasPressed)
            OnRelease();

        _wasPressed = pressed;
    }

    private void OnPress(Vector2 screenPosition, bool shift)
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
        _timelineDragging = false;
        _additiveSelection = false;
        _selectionBase.Clear();
        SetSelectionBox(editor, Vector2.Zero, Vector2.Zero, false);

        Rect2 bar = EditorLayout.RulerRect(view);
        if (bar.HasPoint(screenPosition))
        {
            Entity timelineHit = FindTimelineNoteAt(
                screenPosition, bar, playhead, editor);
            if (!timelineHit.IsNull)
            {
                ref NoteSelection timelineSelection =
                    ref editor.GetComponent<NoteSelection>();
                if (shift)
                {
                    ToggleSelection(ref timelineSelection, timelineHit);
                }
                else
                {
                    if (!timelineSelection.Contains(timelineHit))
                        timelineSelection.Note = timelineHit;
                    BeginTimelineDrag(
                        screenPosition, bar, ref playhead, editor, timelineHit);
                }

                playhead.SeekRequestMs = timelineHit.GetComponent<Note>().TimeMs;
                return;
            }

            BeginScrub(screenPosition, bar, ref playhead, editor);
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
        if (hit.IsNull)
            hit = FindStartAt(playfieldPosition);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        if (!hit.IsNull)
        {
            if (shift)
            {
                ToggleSelection(ref selection, hit);
                return;
            }

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
                else if (selected.HasComponent<StartPoint>())
                {
                    _dragStartPositions[selected.Id] =
                        selected.GetComponent<StartPoint>().Position;
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

        _selectionStart = screenPosition;
        _selectionCurrent = screenPosition;
        if (shift)
        {
            _selectionBase.Clear();
            foreach (Entity selected in selection.AllNotes)
                _selectionBase.Add(selected);
            _selecting = true;
            _additiveSelection = true;
        }
        else
        {
            _pendingCreate = true;
        }
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

        if (_timelineDragging)
        {
            DragTimeline(
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
            _additiveSelection = false;
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
            var selected = new List<Entity>();
            if (_additiveSelection)
                selected.AddRange(_selectionBase);
            foreach (Entity entity in FindNotesIn(
                         Rect2From(_selectionStart, _selectionCurrent),
                         playhead.TimeMs))
            {
                if (!selected.Contains(entity))
                    selected.Add(entity);
            }

            editor.GetComponent<NoteSelection>().SetMany(selected);
            return;
        }

        if (_draggedEntity.IsNull ||
            (!_draggedEntity.HasComponent<Note>() &&
             !_draggedEntity.HasComponent<Mine>() &&
             !_draggedEntity.HasComponent<StartPoint>()))
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
                 !entity.HasComponent<Mine>() &&
                 !entity.HasComponent<StartPoint>()))
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
            else if (entity.HasComponent<Mine>())
            {
                ref Mine mine = ref entity.GetComponent<Mine>();
                mine.Position = target;
            }
            else if (entity.HasComponent<StartPoint>())
            {
                ref StartPoint startPoint = ref entity.GetComponent<StartPoint>();
                startPoint.Position = target;
            }
        }
    }

    private void OnRelease()
    {
        Entity editor = FindEditor();

        if (_timelineDragging)
        {
            _timelineDragging = false;
            if (!editor.IsNull)
                SetScrubWindow(editor, false);
            if (_timelineDragMoved && !editor.IsNull)
                _history.Record(_timelineBefore, _history.Capture());
            _timelineStartTimes.Clear();
            _timelinePrimary = default;
            return;
        }

        if (_scrubbing)
        {
            _scrubbing = false;
            if (!editor.IsNull)
                SetScrubWindow(editor, false);
            return;
        }

        if (!editor.IsNull)
            SetSelectionBox(editor, Vector2.Zero, Vector2.Zero, false);

        if (_selecting)
        {
            _selecting = false;
            _additiveSelection = false;
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

        EditorSnapshot after = _history.Capture();
        _history.Record(_dragBefore, after);
        CancelDrag();
    }

    private void CreatePlacementAt(Entity editor, Vector2 screenPosition)
    {
        if (editor.HasComponent<EditorPlacementModeState>() &&
            editor.GetComponent<EditorPlacementModeState>().Mode ==
            EditorPlacementMode.Start)
        {
            PlayfieldView startView = editor.GetComponent<PlayfieldView>();
            Playfield startField = editor.GetComponent<Playfield>();
            Vector2 startPosition = startView.ScreenToPlayfield(screenPosition);
            if (!IsInside(startPosition, startField))
                return;

            Playhead startPlayhead = editor.GetComponent<Playhead>();
            EditorSnapshot startBefore = _history.Capture();
            EditorItems.Create(_entities, new StartPoint(
                "",
                EditorTiming.Snap(startPlayhead.TimeMs, startPlayhead),
                startPosition,
                System.Numerics.Vector2.Zero,
                new Vector2(0, -1)));
            _history.Record(startBefore, _history.Capture());
            return;
        }

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
            EditorSnapshot before = _history.Capture();
            EditorItems.Create(_entities, new Mine(
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

        EditorSnapshot before = _history.Capture();
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
        Entity created = EditorItems.Create(_entities, new Note(
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

        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            Vector2 center = view.PlayfieldToScreen(start.Position);
            if (screenBounds.HasPoint(center))
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

    private static void ToggleSelection(ref NoteSelection selection, Entity entity)
    {
        var notes = new List<Entity>(selection.AllNotes);
        int index = notes.FindIndex(selected => selected.Id == entity.Id);
        if (index >= 0)
            notes.RemoveAt(index);
        else
            notes.Add(entity);

        selection.SetMany(notes);
    }

    private const float ScrubDeadZone = 0.55f;
    private const float ScrubPanRate = 0.45f;

    private void BeginTimelineDrag(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor,
        Entity primary)
    {
        float zoom = editor.HasComponent<EditorTimeline>()
            ? editor.GetComponent<EditorTimeline>().Zoom
            : 1f;
        (_scrubWindowStart, _scrubWindowEnd) =
            EditorLayout.TimelineWindow(playhead, zoom);
        _timelineDragging = true;
        _timelineDragMoved = false;
        _timelineDragStartX = screenPosition.X;
        _timelinePrimary = primary;
        _timelineBefore = _history.Capture();
        _timelineStartTimes.Clear();

        if (editor.HasComponent<NoteSelection>())
        {
            foreach (Entity selected in editor.GetComponent<NoteSelection>().AllNotes)
            {
                if (!selected.IsNull && selected.Tags.Has<EditorItem>() &&
                    EditorItemTiming.TryGet(selected, out long timeMs))
                {
                    _timelineStartTimes[selected.Id] = timeMs;
                }
            }
        }

        SetScrubWindow(editor, true);
    }

    private void DragTimeline(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor)
    {
        long span = Math.Max(1, _scrubWindowEnd - _scrubWindowStart);
        float delta = (screenPosition.X - _timelineDragStartX) /
                      Math.Max(1f, bar.Size.X) * span;
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        bool moved = false;

        foreach ((int id, long startTime) in _timelineStartTimes)
        {
            Entity entity = _entities.GetEntityById(id);
            if (entity.IsNull)
                continue;

            long target = EditorTiming.Snap(
                startTime + (long)Math.Round(delta),
                playhead);
            target = Math.Clamp(target, 0, maxTime);
            if (target == startTime)
                continue;

            EditorItemTiming.Set(entity, target);
            moved = true;
        }

        if (moved)
        {
            _timelineDragMoved = true;
            if (!_timelinePrimary.IsNull &&
                EditorItemTiming.TryGet(_timelinePrimary, out long primaryTime))
            {
                playhead.SeekRequestMs = primaryTime;
            }
        }

        SetScrubWindow(editor, true);
    }

    private void BeginScrub(
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
        (_scrubWindowStart, _scrubWindowEnd) =
            EditorLayout.TimelineWindow(playhead, zoom);
        _scrubbing = true;
        SetScrubWindow(editor, true);
        Scrub(screenPosition, bar, ref playhead, editor);
    }

    private void Scrub(
        Vector2 screenPosition,
        Rect2 bar,
        ref Playhead playhead,
        Entity editor)
    {
        if (!_scrubbing || playhead.DurationMs <= 0)
            return;

        long span = Math.Max(1, _scrubWindowEnd - _scrubWindowStart);
        float center = bar.Position.X + bar.Size.X / 2;
        float normalized = (screenPosition.X - center) / (bar.Size.X / 2);
        float overshoot = Math.Max(
            0,
            Math.Abs(normalized) - ScrubDeadZone) / (1 - ScrubDeadZone);

        if (overshoot > 0)
        {
            float direction = Math.Sign(normalized);
            long shift = (long)(span * ScrubPanRate * overshoot * overshoot *
                                Tick.deltaTime * direction);
            long maxStart = Math.Max(0, playhead.DurationMs - span);
            _scrubWindowStart = Math.Clamp(
                _scrubWindowStart + shift, 0, maxStart);
            _scrubWindowEnd = _scrubWindowStart + span;
            SetScrubWindow(editor, true);
        }

        float t = Math.Clamp(
            (screenPosition.X - bar.Position.X) / bar.Size.X,
            0f,
            1f);
        long raw = _scrubWindowStart +
                   (long)((_scrubWindowEnd - _scrubWindowStart) * t);
        playhead.SeekRequestMs = Math.Clamp(
            raw,
            0,
            Math.Max(0, playhead.DurationMs));
    }

    private void SetScrubWindow(Entity editor, bool active)
    {
        if (!editor.HasComponent<EditorTimeline>())
            return;

        ref EditorTimeline timeline = ref editor.GetComponent<EditorTimeline>();
        timeline.Scrubbing = active;
        timeline.ScrubStartMs = _scrubWindowStart;
        timeline.ScrubEndMs = _scrubWindowEnd;
    }

    private Entity FindTimelineNoteAt(
        Vector2 screenPosition,
        Rect2 ruler,
        Playhead playhead,
        Entity editor)
    {
        if (playhead.DurationMs <= 0)
            return default;

        float zoom = editor.HasComponent<EditorTimeline>()
            ? editor.GetComponent<EditorTimeline>().Zoom
            : 1f;
        (long start, long end) = EditorLayout.TimelineWindow(playhead, zoom);
        float span = Math.Max(1, end - start);
        float noteSquare = Math.Clamp(6f + zoom * 0.0625f, 6f, 8f);
        const float noteGap = 1f;

        var notesByTime = new Dictionary<long, List<Entity>>();
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.TimeMs < start || note.TimeMs > end)
                continue;

            if (!notesByTime.TryGetValue(note.TimeMs, out List<Entity>? notes))
            {
                notes = new List<Entity>();
                notesByTime.Add(note.TimeMs, notes);
            }

            notes.Add(entity);
        }

        foreach ((long timeMs, List<Entity> notes) in notesByTime)
        {
            float x = ruler.Position.X +
                      (timeMs - start) / span * ruler.Size.X;
            int visible = Math.Min(notes.Count, 7);
            float totalHeight = visible * noteSquare +
                                Math.Max(0, visible - 1) * noteGap;
            float top = ruler.Position.Y +
                        (ruler.Size.Y - totalHeight) / 2;

            for (int i = 0; i < visible; i++)
            {
                var bounds = new Rect2(
                    x - noteSquare / 2,
                    top + i * (noteSquare + noteGap),
                    noteSquare,
                    noteSquare);
                if (bounds.HasPoint(screenPosition))
                    return notes[i];
            }
        }

        return default;
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

    private Entity FindStartAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint startPoint = entity.GetComponent<StartPoint>();
            if (startPoint.Position.DistanceTo(position) <= 14f)
                result = entity;
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
