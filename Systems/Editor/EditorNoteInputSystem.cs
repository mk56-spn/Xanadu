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
    private readonly Dictionary<int, Block> _dragStartBlocks = new();
    private readonly Dictionary<int, Spike> _dragStartSpikes = new();
    private EditorSnapshot _dragBefore;
    private readonly NoteBox _box;

    // A paint stroke laying spike tiles; the whole stroke is one undo step.
    private bool _paintingSpikes;
    private bool _spikeStrokeAdded;
    private EditorSnapshot _spikeBefore;

    // A resize drag started on one of the primary block's side handles.
    private Entity _resizeEntity;
    private BlockHandle _resizeHandle;
    private Block _resizeStart;
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
            _wasPressed = pressed;
            return;
        }

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

        if (IsOverNoteBar(screenPosition))
            return;

        if (editor.HasComponent<EditorDirectionWheel>())
        {
            EditorDirectionWheel wheel =
                editor.GetComponent<EditorDirectionWheel>();
            if (!wheel.Wheel.IsNull &&
                UiWheelGeometry.Contains(wheel.Wheel, screenPosition))
            {
                return;
            }
        }

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);

        // Handles hang over the field's edge, so they get the press before the
        // inside test would turn an edge drag away.
        if (TryBeginResize(screenPosition, editor, view))
            return;

        if (!IsInside(playfieldPosition, playfield))
            return;

        Entity hit = FindNoteAt(playfieldPosition, playhead.TimeMs);
        if (hit.IsNull)
            hit = FindMineAt(playfieldPosition, playhead.TimeMs);
        if (hit.IsNull)
            hit = FindStartAt(playfieldPosition);
        if (hit.IsNull)
            hit = FindSpikeAt(playfieldPosition);
        if (hit.IsNull)
            hit = FindBlockAt(playfieldPosition);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        // An empty cell in spike mode starts a paint stroke instead of arming a
        // placement, so dragging lays a run of tiles.
        if (hit.IsNull && IsSpikeMode(editor))
        {
            _paintingSpikes = true;
            _spikeStrokeAdded = false;
            _spikeBefore = _history.Capture();
            PaintSpikeAt(playfieldPosition, playfield);
            return;
        }

        if (!hit.IsNull)
        {
            if (shift)
            {
                selection.Toggle(hit);
                return;
            }

            _draggedEntity = hit;
            _drag.Begin(hit, playfieldPosition);
            _dragStartPositions.Clear();
            _dragStartBlocks.Clear();
            _dragStartSpikes.Clear();
            _dragBefore = _history.Capture();

            if (!selection.Contains(hit))
                selection.Note = hit;

            foreach (Entity selected in selection.AllNotes)
            {
                if (EditorItems.IsItem(selected) &&
                    EditorItemPosition.TryGet(selected, out Vector2 selectedPosition))
                {
                    _dragStartPositions[selected.Id] = selectedPosition;
                    if (selected.HasComponent<Block>())
                        _dragStartBlocks[selected.Id] = selected.GetComponent<Block>();
                    if (selected.HasComponent<Spike>())
                        _dragStartSpikes[selected.Id] = selected.GetComponent<Spike>();
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

        // Shift-dragging adds to the selection; a plain press may place a note
        // when released without dragging, or box-select when dragged.
        _box.OnTimeline = false;
        if (shift)
            _box.BeginAdditive(screenPosition, selection.AllNotes);
        else
            _box.Arm(screenPosition);
    }

    private void OnHold(Vector2 screenPosition)
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

        if (_paintingSpikes)
        {
            PaintSpikeAt(view.ScreenToPlayfield(screenPosition), playfield);
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

            if (_dragStartBlocks.TryGetValue(id, out Block block))
            {
                entity.GetComponent<Block>() =
                    BlockEditing.Move(block, delta, playfield);
                continue;
            }

            if (_dragStartSpikes.TryGetValue(id, out Spike spike))
            {
                entity.GetComponent<Spike>() =
                    SpikeRules.Move(spike, delta, playfield);
                continue;
            }

            Vector2 target = new(
                Math.Clamp(start.X + delta.X, 0, playfield.Width),
                Math.Clamp(start.Y + delta.Y, 0, playfield.Height));
            EditorItemPosition.Set(entity, target);
        }
    }

    private void OnRelease()
    {
        Entity editor = FindEditor();

        if (_paintingSpikes)
        {
            _paintingSpikes = false;
            if (_spikeStrokeAdded && !editor.IsNull)
                _history.Record(_spikeBefore, _history.Capture());
            _spikeStrokeAdded = false;
            _spikeBefore = default;
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
            if (!editor.IsNull)
                CreatePlacementAt(editor, _box.Start);
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
            EditorPlacementMode.Blocks)
        {
            PlaceBlock(editor, screenPosition);
            return;
        }

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
        CharacterKind character = editor.HasComponent<EditorCharacterTarget>()
            ? editor.GetComponent<EditorCharacterTarget>().Target
            : CharacterKind.Contact;
        EditorPlacementMode mode = editor.HasComponent<EditorPlacementModeState>()
            ? editor.GetComponent<EditorPlacementModeState>().Mode
            : EditorPlacementMode.Notes;
        DashDirection direction = editor.HasComponent<EditorDashDirectionState>()
            ? editor.GetComponent<EditorDashDirectionState>().Direction
            : DashDirection.Up;
        DashStrength strength = editor.HasComponent<EditorDashStrengthState>()
            ? editor.GetComponent<EditorDashStrengthState>().Strength
            : DashStrength.Single;
        Entity created = EditorItems.Create(_entities, new Note(
            time,
            playfieldPosition,
            target: mode == EditorPlacementMode.CharacterSwitch
                ? KeybindAction.M
                : target,
            kind: mode switch
            {
                EditorPlacementMode.Movement => NoteKind.Movement,
                EditorPlacementMode.Contact => NoteKind.Contact,
                EditorPlacementMode.CharacterSwitch => NoteKind.CharacterSwitch,
                _ => NoteKind.Hit,
            },
            dashDirection: direction,
            character: character,
            dashStrength: strength));
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
        float zoom = editor.HasComponent<TimelineView>()
            ? editor.GetComponent<TimelineView>().Zoom
            : 1f;
        (_scrubWindowStart, _scrubWindowEnd) =
            EditorLayout.TimelineWindow(playhead, zoom);
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

        float zoom = editor.HasComponent<TimelineView>()
            ? editor.GetComponent<TimelineView>().Zoom
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
            if (bounds.HasPoint(screenPosition))
                return note;
        }

        return default;
    }

    private List<(Entity Note, Rect2 Bounds)> TimelineSlots(
        Entity editor,
        Rect2 ruler,
        Playhead playhead)
    {
        float zoom = editor.HasComponent<TimelineView>()
            ? editor.GetComponent<TimelineView>().Zoom
            : 1f;
        (long start, long end) = EditorLayout.TimelineWindow(playhead, zoom);
        return EditorTimelineNotes.Slots(_entities, ruler, start, end, zoom);
    }

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
    private bool IsOverNoteBar(Vector2 screenPosition)
    {
        foreach (Entity panel in _entities
                     .Query<EditorNoteBarPanel, Position, Rectangle>()
                     .ToEntityList())
        {
            Vector2 center = panel.GetComponent<Position>().Value;
            Rect2 local = panel.GetComponent<Rectangle>().Bounds;
            if (center != UiLayout.Hidden &&
                new Rect2(center + local.Position, local.Size).HasPoint(screenPosition))
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
        _dragStartBlocks.Clear();
        _dragStartSpikes.Clear();
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
