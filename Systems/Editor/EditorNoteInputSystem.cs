using System.Collections.Generic;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>Adds, selects, drags, and scrubs notes in playfield coordinates.</summary>
public sealed partial class EditorNoteInputSystem : QuerySystem<Note>
{
    private const float NoteHitRadius = PlayfieldRenderer.NoteSize / 2;
    private const float MineHitRadius = PlayfieldRenderer.MineSize / 2;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly EditorHistory _history;

    private bool _wasPressed;

    /// <summary>
    /// The item a placement just selected. It stays selected for editing, but
    /// the next click on empty space places again rather than deselecting it.
    /// </summary>
    private Entity _placed;
    private bool _scrubbing;
    private long _scrubWindowStart;
    private long _scrubWindowEnd;
    private bool _timelineDragging;
    private bool _timelineDragMoved;
    private readonly UiDragController _timelineDrag = new();
    private Entity _timelinePrimary;
    private EditorSnapshot _timelineBefore;
    private readonly Dictionary<int, long> _timelineStartTimes = new();
    private Entity _draggedEntity;
    private readonly UiDragController _drag = new();
    private readonly Dictionary<int, Vector2> _dragStartPositions = new();
    private EditorSnapshot _dragBefore;
    private readonly NoteBox _box;

    // A paint stroke laying blocks or spikes; the whole stroke is one undo step.
    private bool _painting;
    private bool _paintAdded;
    private EditorSnapshot _paintBefore;

    // A resize drag started on one of the primary block's or spike's side handles.
    private Entity _resizeEntity;
    private GridHandle _resizeHandle;
    private Rect2 _resizeStart;
    private float _resizeDegrees;
    private EditorSnapshot _resizeBefore;

    public EditorNoteInputSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        EditorHistory history)
    {
        _entities = entities;
        _pointer = pointer;
        _history = history;
        _box = new NoteBox(this);
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;

        if (IsGroupTargetEditing())
        {
            UpdateGhost(input.Position, pressed);
        _wasPressed = pressed;
            return;
        }

        if (pressed && !_wasPressed)
            OnPress(input.Position, input.Shift);
        else if (pressed)
            OnHold(input.Position, input.Shift);
        else if (_wasPressed)
            OnRelease();

        UpdateGhost(input.Position, pressed);
        _wasPressed = pressed;
    }

    private void OnPress(Vector2 screenPosition, bool shift)
    {
        Entity editor = FindEditor();
        if (editor.IsNull ||
            IsWarningOpen() ||
            IsGroupOverlayOpen() ||
            IsSceneConfirmationOpen())
        {
            return;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        _box.Cancel(editor);
        _timelineDragging = false;
        _resizeEntity = default;

        Rect2 bar = EditorLayout.RulerRect(view);
        if (bar.HasPoint(screenPosition))
        {
            Entity timelineHit = FindTimelineNoteAt(
                screenPosition, bar, playhead, editor);
            if (!timelineHit.IsNull)
            {
                _placed = default;
                ref NoteSelection timelineSelection =
                    ref editor.GetComponent<NoteSelection>();
                if (shift)
                {
                    timelineSelection.Toggle(timelineHit);
                }
                else
                {
                    if (!timelineSelection.Contains(timelineHit))
                        timelineSelection.Note = timelineHit;
                    BeginTimelineDrag(
                        screenPosition, bar, ref playhead, editor, timelineHit);
                }

                if (EditorItemTiming.TryGet(timelineHit, out long timelineTime))
                    playhead.SeekRequestMs = timelineTime;
                return;
            }

            // Shift on empty strip drags a box over the notes, as on the
            // playfield; a plain press scrubs.
            if (shift && playhead.DurationMs > 0)
            {
                _box.OnTimeline = true;
                _box.BeginAdditive(
                    screenPosition, editor.GetComponent<NoteSelection>().AllNotes);
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

        if (IsOverNoteBar(screenPosition) || IsOverPanel(screenPosition))
            return;

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);

        // Handles hang over the field's edge, so they get the press before the
        // inside test would turn an edge drag away.
        if (TryBeginRotation(screenPosition, editor, view) ||
            TryBeginResize(screenPosition, editor, view))
        {
            return;
        }

        if (!IsInside(playfieldPosition, playfield))
            return;

        Entity hit = FindItemAt(playfieldPosition, playhead.TimeMs);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        // An empty spot with a block or spike tool starts a paint stroke instead
        // of arming a placement, so dragging lays a run. Shift still box-selects,
        // and a selection has to be cleared before anything new goes down.
        if (hit.IsNull && !shift && IsGridMode(editor) && !HasPickedSelection(editor))
        {
            _painting = true;
            _paintAdded = false;
            _paintBefore = _history.Capture();
            PaintAt(playfieldPosition, playfield, editor);
            return;
        }

        if (!hit.IsNull)
        {
            _placed = default;
            if (shift)
            {
                selection.Toggle(hit);
                return;
            }

            _draggedEntity = hit;
            _drag.Begin(hit, playfieldPosition);
            _dragStartPositions.Clear();
            _dragBefore = _history.Capture();

            if (!selection.Contains(hit))
                selection.Note = hit;

            foreach (Entity selected in selection.AllNotes)
            {
                if (EditorItems.IsItem(selected) &&
                    EditorItemPosition.TryGet(selected, out Vector2 selectedPosition))
                {
                    // Blocks and spikes are dragged by their corner, which is what snaps.
                    _dragStartPositions[selected.Id] =
                        GridPlacement.TryGet(selected, out Rect2 box, out _) ? box.Position : selectedPosition;
                }
            }

            if (hit.HasComponent<Note>() &&
                editor.HasComponent<EditorPlacement>())
            {
                Note selected = hit.GetComponent<Note>();
                editor.GetComponent<EditorPlacement>().Note =
                    selected.Kind == NoteKind.Pair
                        ? NoteRules.ActionFor(selected.PairIndex)
                        : selected.Target;
            }

            return;
        }

        // Shift-dragging adds to the selection; a plain press may place a note or
        // clear the selection when released without dragging, or box-select when dragged.
        _box.OnTimeline = false;
        if (shift)
            _box.BeginAdditive(screenPosition, selection.AllNotes);
        else
            _box.Arm(screenPosition);
    }

    private void OnHold(Vector2 screenPosition, bool shift)
    {
        Entity editor = FindEditor();
        if (editor.IsNull ||
            IsWarningOpen() ||
            IsGroupOverlayOpen() ||
            IsSceneConfirmationOpen())
        {
            return;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        if (_rotating)
        {
            UpdateRotation(screenPosition, shift, editor, view, playfield);
            return;
        }

        if (!_resizeEntity.IsNull)
        {
            UpdateResize(screenPosition, view, playfield);
            return;
        }

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
            if (_timelineDrag.Update(screenPosition))
            {
                DragTimeline(
                    screenPosition,
                    EditorLayout.RulerRect(view),
                    ref playhead,
                    editor);
            }

            return;
        }

        if (_box.Drag(editor, screenPosition))
            return;

        if (_painting)
        {
            PaintAt(view.ScreenToPlayfield(screenPosition), playfield, editor);
            return;
        }

        if (!EditorItems.IsItem(_draggedEntity) ||
            !EditorItemPosition.TryGet(_draggedEntity, out _))
        {
            CancelDrag();
            return;
        }

        Vector2 pointer = view.ScreenToPlayfield(screenPosition);
        if (!_drag.Update(pointer))
            return;

        Vector2 delta = _drag.Delta;
        foreach ((int id, Vector2 start) in _dragStartPositions)
        {
            Entity entity = _entities.GetEntityById(id);
            if (!EditorItems.IsItem(entity))
                continue;

            if (GridPlacement.TryGet(entity, out Rect2 box, out _))
            {
                GridPlacement.SetBounds(entity, new Rect2(
                    GridPlacement.Move(start, box.Size, delta, playfield, ViewportSnap.IsOn(_entities)),
                    box.Size));
                continue;
            }

            Vector2 target = new(
                Math.Clamp(start.X + delta.X, 0, playfield.Width),
                Math.Clamp(start.Y + delta.Y, 0, playfield.Height));
            // Notes follow the snap button like blocks do, landing on grid crossings.
            if (entity.HasComponent<Note>() && ViewportSnap.IsOn(_entities))
                target = PlayfieldGrid.NearestCrossing(target, playfield);
            EditorItemPosition.Set(entity, target);
        }
    }

    private void OnRelease()
    {
        Entity editor = FindEditor();

        if (_painting)
        {
            _painting = false;
            if (_paintAdded && !editor.IsNull)
                _history.Record(_paintBefore, _history.Capture());
            _paintAdded = false;
            _paintBefore = default;
            return;
        }

        if (_rotating)
        {
            EndRotation(editor);
            return;
        }

        if (!_resizeEntity.IsNull)
        {
            _history.Record(_resizeBefore, _history.Capture());
            _resizeEntity = default;
            return;
        }

        if (_timelineDragging)
        {
            _timelineDragging = false;
            _timelineDrag.End();
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

        MarqueeEnd end = _box.End(editor);
        if (end == MarqueeEnd.Box)
            return;

        if (end == MarqueeEnd.Click)
        {
            if (editor.IsNull)
                return;

            if (HasPickedSelection(editor))
            {
                editor.GetComponent<NoteSelection>().Clear();
                _placed = default;
                return;
            }

            CreatePlacementAt(editor, _box.Start);
            _placed = editor.GetComponent<NoteSelection>().Note;
            return;
        }

        if (_draggedEntity.IsNull)
            return;

        EditorSnapshot after = _history.Capture();
        _history.Record(_dragBefore, after);
        CancelDrag();
    }

    /// <summary>Whether anything is selected other than the item a placement just selected.</summary>
    private bool HasPickedSelection(Entity editor)
    {
        if (!editor.TryGetComponent(out NoteSelection selection))
            return false;

        IReadOnlyList<Entity> notes = selection.AllNotes;
        return notes.Count > 1 || (notes.Count == 1 && notes[0] != _placed);
    }

    private void CreatePlacementAt(Entity editor, Vector2 screenPosition)
    {
        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
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

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
            EditorPlacementMode.Text)
        {
            PlayfieldView textView = editor.GetComponent<PlayfieldView>();
            Vector2 textPosition = textView.ScreenToPlayfield(screenPosition);
            if (!IsInside(textPosition, editor.GetComponent<Playfield>()))
                return;

            EditorSnapshot textBefore = _history.Capture();
            Entity text = EditorItems.Create(_entities, new LevelText(textPosition, LevelText.Placeholder));
            _history.Record(textBefore, _history.Capture());
            editor.GetComponent<NoteSelection>().Note = text;
            return;
        }

        if (editor.HasComponent<EditorPlacement>() &&
            editor.GetComponent<EditorPlacement>().Mode ==
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
        if (EditorPlacementNote.NeedsShape(editor))
            return;

        if (ViewportSnap.IsOn(_entities))
            playfieldPosition = PlayfieldGrid.NearestCrossing(playfieldPosition, playfield);
        Note note = EditorPlacementNote.Build(editor, time, playfieldPosition);
        bool isFormStep = note.Kind == NoteKind.ConstructionStep;
        if (isFormStep)
            note.ShapePiece = NextShapePiece(note.ShapeId);

        Entity created = EditorItems.Create(_entities, note);
        if (isFormStep)
            ReindexConstructionSteps(note.ShapeId);
        _history.Record(before, _history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
        EditorAutoAdvance.AfterPlacing(_entities, editor, time);
    }

    private int NextShapePiece(int shapeId)
    {
        int count = 0;
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind == NoteKind.ConstructionStep &&
                note.ShapeId == shapeId)
            {
                count++;
            }
        }

        return count;
    }

    private void ReindexConstructionSteps(int shapeId)
    {
        var steps = new List<Entity>();
        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (note.Kind == NoteKind.ConstructionStep &&
                note.ShapeId == shapeId)
            {
                steps.Add(entity);
            }
        }

        steps.Sort((a, b) =>
            a.GetComponent<Note>().TimeMs.CompareTo(
                b.GetComponent<Note>().TimeMs));
        for (int i = 0; i < steps.Count; i++)
        {
            ref Note note = ref steps[i].GetComponent<Note>();
            note.ShapePiece = i;
        }
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
            if (NoteRules.IsRail(note.Kind) ||
                NoteRules.IsChannelEnd(note.Kind) ||
                NoteFade.Alpha(note, playheadMs) <= 0.05f ||
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

        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            if (screenBounds.Intersects(PlayfieldRenderer.TextScreenBounds(entity.GetComponent<LevelText>(), view)))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            Spike spike = entity.GetComponent<Spike>();
            var spikeBounds = new Rect2(
                view.PlayfieldToScreen(spike.Position),
                spike.Size * view.Scale);
            if (screenBounds.Intersects(spikeBounds))
                selected.Add(entity);
        }

        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            Block block = entity.GetComponent<Block>();
            var blockBounds = new Rect2(
                view.PlayfieldToScreen(block.Position),
                block.Size * view.Scale);
            if (screenBounds.Intersects(blockBounds))
                selected.Add(entity);
        }

        return selected;
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
        (_scrubWindowStart, _scrubWindowEnd) =
            EditorLayout.TimelineWindow(playhead, TimelineOf(editor));
        _timelineDragging = true;
        _timelineDragMoved = false;
        _timelineDrag.Begin(primary, screenPosition);
        _timelinePrimary = primary;
        _timelineBefore = _history.Capture();
        _timelineStartTimes.Clear();

        if (editor.HasComponent<NoteSelection>())
        {
            foreach (Entity selected in editor.GetComponent<NoteSelection>().AllNotes)
            {
                if (EditorItems.IsItem(selected) &&
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
        float delta = _timelineDrag.Delta.X /
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

        (_scrubWindowStart, _scrubWindowEnd) =
            EditorLayout.TimelineWindow(playhead, TimelineOf(editor));
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
        if (!editor.HasComponent<TimelineView>())
            return;

        ref TimelineView timeline = ref editor.GetComponent<TimelineView>();
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

        foreach ((Entity note, Rect2 bounds) in TimelineSlots(editor, ruler, playhead))
        {
            if (EditorTimelineNotes.HitBounds(bounds).HasPoint(screenPosition))
                return note;
        }

        return default;
    }

    private List<(Entity Note, Rect2 Bounds)> TimelineSlots(
        Entity editor,
        Rect2 ruler,
        Playhead playhead)
    {
        TimelineView view = TimelineOf(editor);
        (long start, long end) = EditorLayout.TimelineWindow(playhead, view);
        return EditorTimelineNotes.Slots(_entities, ruler, start, end, view.Zoom);
    }

    /// <summary>The editor's timeline view, or a whole-song one when it has none.</summary>
    private static TimelineView TimelineOf(Entity editor) =>
        editor.HasComponent<TimelineView>() ? editor.GetComponent<TimelineView>() : new TimelineView(1f);

    /// <summary>Drag-to-select for the playfield and for the timeline strip; they differ only in what a box covers.</summary>
    private sealed class NoteBox(EditorNoteInputSystem system) : UiBoxSelector<Entity>
    {
        /// <summary>The box was started on the timeline strip rather than the playfield.</summary>
        public bool OnTimeline { get; set; }

        protected override IEnumerable<Entity> HitsIn(Entity owner, Rect2 bounds)
        {
            Playhead playhead = owner.GetComponent<Playhead>();
            if (!OnTimeline)
                return system.FindNotesIn(bounds, playhead.TimeMs);

            var hits = new List<Entity>();
            Rect2 ruler = EditorLayout.RulerRect(owner.GetComponent<PlayfieldView>());
            foreach ((Entity note, Rect2 slot) in system.TimelineSlots(owner, ruler, playhead))
            {
                if (bounds.Intersects(slot))
                    hits.Add(note);
            }

            return hits;
        }

        protected override void Select(Entity owner, List<Entity> selection) =>
            owner.GetComponent<NoteSelection>().SetMany(selection);
    }

    /// <summary>The note-type bar floats over the playfield on small windows; presses on it are not placements.</summary>
    /// <summary>
    /// Whether the press lands on a panel or control that takes input. The
    /// field runs on under the editor's panels once it is bigger than the
    /// screen, so a press on a panel must not also place a note beneath it.
    /// </summary>
    private bool IsOverPanel(Vector2 screenPosition)
    {
        foreach (Entity entity in _entities
                     .Query<Position, Rectangle>()
                     .AllTags(Tags.Get<TakesInput>())
                     .ToEntityList())
        {
            Vector2 local = entity.GetComponent<Position>().Value;
            Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
            Vector2 center = UiTransform.WorldPosition(entity, local);
            if (new Rect2(center + bounds.Position, bounds.Size).HasPoint(screenPosition))
                return true;
        }

        return false;
    }

    private bool IsOverNoteBar(Vector2 screenPosition)
    {
        foreach (Entity panel in _entities
                     .Query<EditorNoteBarPanel, Position, Rectangle>()
                     .ToEntityList())
        {
            Vector2 center = panel.GetComponent<Position>().Value;
            Rect2 local = panel.GetComponent<Rectangle>().Bounds;
            if (new Rect2(center + local.Position, local.Size).HasPoint(screenPosition))
            {
                return true;
            }
        }

        return false;
    }

    private Entity FindNoteAt(Vector2 position, long playheadMs)
    {
        Entity result = default;

        foreach (Entity entity in _entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            if (NoteFade.Alpha(note, playheadMs) <= 0)
                continue;

            if (MathF.Abs(note.Position.X - position.X) <= NoteHitRadius &&
                MathF.Abs(note.Position.Y - position.Y) <= NoteHitRadius)
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

            if (MathF.Abs(mine.Position.X - position.X) <= MineHitRadius &&
                MathF.Abs(mine.Position.Y - position.Y) <= MineHitRadius)
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
        _drag.End();
        _dragStartPositions.Clear();
    }

    private bool IsGroupOverlayOpen()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().Open >= 0)
                return true;
        }

        return false;
    }

    private bool IsGroupTargetEditing()
    {
        foreach (Entity entity in _entities.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().TargetEdit)
                return true;
        }

        return false;
    }

    private bool IsWarningOpen()
    {
        return _entities.Query<WarningPopup>().Count > 0;
    }

    private bool IsSceneConfirmationOpen()
    {
        return _entities.Query<SceneConfirmation>().Count > 0;
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
