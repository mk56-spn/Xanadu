using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Editor.History;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Levels;
using Xanadu.Model.Playfields;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Interaction;

namespace Xanadu.Logic.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): viewport: placing and dragging on the playfield, until the viewport has its own design.
#pragma warning disable RS0030

/// <summary>
/// Places notes with a click on empty field, and selects and drags notes,
/// in playfield coordinates; the timeline strip is the shared timeline's.
/// </summary>
public sealed class EditorNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly EditorHistory _history;
    private readonly EditorSceneQuery _scene;
    private readonly EditorItemPicker _picker;
    private readonly EditorHoverProbe _hover;

    private bool _wasPressed;

    /// <summary>
    /// The item a placement just selected. It stays selected for editing, but
    /// the next click on empty space places again rather than deselecting it.
    /// </summary>
    private Entity _placed;
    private Entity _draggedEntity;
    private readonly UiDragController _drag = new();
    private readonly Dictionary<int, Vector2> _dragStartPositions = [];
    private EditorSnapshot _dragBefore;
    private readonly NoteBox _box;

    public EditorNoteInputSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        EditorHistory history)
    {
        _entities = entities;
        _pointer = pointer;
        _history = history;
        _scene = new EditorSceneQuery(entities);
        _picker = new EditorItemPicker(_scene);
        _hover = new EditorHoverProbe(_scene, _picker);
        _box = new NoteBox(this);
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;
        Entity editor = _scene.Editor();
        bool picked = HasPickedSelection(editor);

        if (pressed && !_wasPressed)
            OnPress(input.Position, input.Shift);
        else if (pressed)
            OnHold(input.Position, input.Shift);
        else if (_wasPressed)
            OnRelease();

        _hover.Update(input.Position, pressed, picked);
        _wasPressed = pressed;
    }

    private bool IsOverEditorUi(Entity editor, Vector2 screenPosition) =>
        EditorLayout.RulerRect(editor.GetComponent<PlayfieldView>()).HasPoint(screenPosition) ||
        (editor.TryGetComponent(out EditorSidebar sidebar) && sidebar.Bounds.HasPoint(screenPosition)) ||
        _scene.IsOverNoteBar(screenPosition) ||
        _scene.IsOverPanel(screenPosition);

    private void OnPress(Vector2 screenPosition, bool shift)
    {
        Entity editor = _scene.Editor();
        if (editor.IsNull || _scene.IsModalOpen())
            return;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        _box.Cancel(editor);

        // The ruler strip is the shared timeline's (TimelineInputSystem, answered by EditorTimeline).
        if (IsOverEditorUi(editor, screenPosition))
            return;

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);

        if (!PlayfieldGrid.IsInside(playfieldPosition, playfield))
            return;

        Entity hit = _picker.FindItemAt(playfieldPosition, playhead.TimeMs);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

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
                    _dragStartPositions[selected.Id] = selectedPosition;
                }
            }

            // The key picker shows the picked note's key, so the next note matches it.
            if (hit.HasComponent<Note>() && editor.HasComponent<EditorPlacement>())
                editor.GetComponent<EditorPlacement>().Note = hit.GetComponent<Note>().Target;

            return;
        }

        // Shift-dragging adds to the selection; a plain press, released without
        // dragging, clears the selection or places a note, and box-selects when dragged.
        if (shift)
            _box.BeginAdditive(screenPosition, selection.AllNotes);
        else
            _box.Arm(screenPosition);
    }

    /// <summary>Places the tool's note where clicked, at the snapped playhead, as one undo step; it stays selected.</summary>
    private void Place(Entity editor, Vector2 screenPosition)
    {
        if (editor.GetComponent<LevelInfo>().Song is null)
        {
            EditorWarnings.Request(editor, EditorWarnings.NoSongMessage);
            return;
        }

        if (EditorItems.WouldOverfill(_entities, editor))
            return;

        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 point = editor.GetComponent<PlayfieldView>().ScreenToPlayfield(screenPosition);
        if (!PlayfieldGrid.IsInside(point, playfield))
            return;
        if (ViewportSnap.IsOn(_entities))
            point = PlayfieldGrid.NearestCrossing(point, playfield);

        Playhead playhead = editor.GetComponent<Playhead>();
        long time = EditorTiming.Snap(playhead.TimeMs, playhead);
        EditorSnapshot before = _history.Capture();
        EditorPlacementMode mode = editor.GetComponent<EditorPlacement>().Mode;
        if ((mode == EditorPlacementMode.Orb && ShapeOrb(editor, time, point)) ||
            (mode == EditorPlacementMode.Entrance && ShapeExit(editor, time, point)) ||
            (mode == EditorPlacementMode.Draft && ShapeDraft(editor, point)))
        {
            _history.Record(before, _history.Capture());
            return;
        }

        Note placed = EditorPlacementNote.Build(editor, time, point);
        if (placed.IsOrb)
            placed.EndMs = time + (long)TempoMap.BeatMs(playhead.Tempo.At(time).Bpm) * 2;
        Entity created = mode switch
        {
            EditorPlacementMode.Chord => EditorItems.Place(_entities, new Chord(time, point)),
            EditorPlacementMode.Pad => EditorItems.Place(_entities, new Pad(point, Pads.DefaultSpeed)),
            EditorPlacementMode.TimedPad => EditorItems.Place(_entities, new Pad(point, Pads.DefaultSpeed, time)),
            EditorPlacementMode.Draft => EditorItems.Place(_entities, new Draft(point, Drafts.DefaultHeight)),
            EditorPlacementMode.DashStop => EditorItems.Place(_entities, new DashStop(point)),
            EditorPlacementMode.Grass => EditorItems.Place(_entities, new Deco(point, Named(Decos.Grass()))),
            EditorPlacementMode.Flare => EditorItems.Place(_entities, new Deco(point, Named(Decos.Flare()))),
            _ => EditorItems.Place(_entities, placed),
        };
        _history.Record(before, _history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
        _placed = created;
    }

    /// <summary>
    /// With an orb still selected from placing it, a click after its head sets a
    /// height key there (at the click's height), stretching the tail out to it.
    /// </summary>
    private bool ShapeOrb(Entity editor, long time, Vector2 point)
    {
        Entity orb = editor.GetComponent<NoteSelection>().Note;
        if (orb.IsNull || orb != _placed || !orb.TryGetComponent(out Note note) || !note.IsOrb || time <= note.TimeMs)
            return false;

        ref Note shaped = ref orb.GetComponent<Note>();
        shaped.Path = Orbs.WithKey(shaped.Path, time - shaped.TimeMs, point.Y - shaped.Position.Y);
        shaped.EndMs = Math.Max(shaped.EndMs, time);
        return true;
    }

    /// <summary>
    /// With an entrance still selected from placing it, a click sets its exit.
    /// At its own time: on the rift is a fall, off to a side is a dash that way.
    /// After its time: it comes out on an orb, and the click sets a height key
    /// there, as the orb tool does.
    /// </summary>
    private bool ShapeExit(Entity editor, long time, Vector2 point)
    {
        Entity picked = editor.GetComponent<NoteSelection>().Note;
        if (picked.IsNull || picked != _placed || !picked.TryGetComponent(out Note note) || !note.IsEntrance || time < note.TimeMs)
            return false;

        ref Note entrance = ref picked.GetComponent<Note>();
        if (time == entrance.TimeMs)
        {
            Vector2 away = point - entrance.Position;
            entrance.Path = null;
            entrance.EndMs = entrance.TimeMs;
            if (away.Length() < PlayRules.BodyHeight * 0.8f)
            {
                entrance.Exit = EntranceExit.Fall;
                return true;
            }

            entrance.Exit = EntranceExit.Dash;
            entrance.Target = MathF.Abs(away.X) >= MathF.Abs(away.Y)
                ? away.X > 0 ? KeybindAction.Right : KeybindAction.Left
                : away.Y > 0 ? KeybindAction.Down : KeybindAction.Up;
            return true;
        }

        if (entrance.Exit != EntranceExit.Orb)
        {
            entrance.Exit = EntranceExit.Orb;
            entrance.Path = null;
            entrance.EndMs = time;
        }

        // The orb starts with the body's top at the rift.
        float startY = entrance.Position.Y - Orbs.Hang.Y;
        entrance.Path = Orbs.WithKey(entrance.Path, time - entrance.TimeMs, point.Y - startY);
        entrance.EndMs = Math.Max(entrance.EndMs, time);
        return true;
    }

    /// <summary>With an updraft still selected from placing it, a click above its emitter, in its column, sets its hover height there.</summary>
    private bool ShapeDraft(Entity editor, Vector2 point)
    {
        Entity picked = editor.GetComponent<NoteSelection>().Note;
        if (picked.IsNull || picked != _placed || !picked.TryGetComponent(out Draft draft))
            return false;
        if (point.Y >= draft.Position.Y - Drafts.EmitterHeight || MathF.Abs(point.X - draft.Position.X) > Drafts.Diameter / 2f)
            return false;

        picked.GetComponent<Draft>().Height = draft.Position.Y - point.Y;
        return true;
    }

    private void OnHold(Vector2 screenPosition, bool shift)
    {
        Entity editor = _scene.Editor();
        if (editor.IsNull || _scene.IsModalOpen())
            return;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        if (_box.Drag(editor, screenPosition))
            return;

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

            Vector2 target = new(
                Math.Clamp(start.X + delta.X, 0, playfield.Width),
                Math.Clamp(start.Y + delta.Y, 0, playfield.Height));
            // Notes follow the snap button, landing on grid crossings.
            if (ViewportSnap.IsOn(_entities))
                target = PlayfieldGrid.NearestCrossing(target, playfield);
            EditorItemPosition.Set(entity, target);
        }
    }

    private void OnRelease()
    {
        Entity editor = _scene.Editor();

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

            Place(editor, _box.Start);
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

    /// <summary>Drag-to-select on the playfield; the timeline strip's box is the shared timeline's.</summary>
    private sealed class NoteBox(EditorNoteInputSystem system) : UiBoxSelector<Entity>
    {
        protected override IEnumerable<Entity> HitsIn(Entity owner, Rect2 bounds) =>
            system._picker.FindNotesIn(bounds, owner.GetComponent<Playhead>().TimeMs);

        protected override void Select(Entity owner, List<Entity> selection) =>
            owner.GetComponent<NoteSelection>().SetMany(selection);
    }

    private void CancelDrag()
    {
        _draggedEntity = default;
        _dragBefore = default;
        _drag.End();
        _dragStartPositions.Clear();
    }


    /// <summary>A new deco's Xif with an id no deco on the field has, so groups can hold it.</summary>
    private Xanadu.Core.Items.Xif Named(Xanadu.Core.Items.Xif xif)
    {
        var taken = new System.Collections.Generic.HashSet<string>();
        _entities.Query<Deco>().ForEachEntity((ref deco, _) => taken.Add(deco.Xif.Id));
        xif.Id = LevelItemIds.Fresh(taken, xif.Name);
        return xif;
    }
}
