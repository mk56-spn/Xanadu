using System.Collections.Generic;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Systems.Input;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): viewport: placing and dragging on the playfield, until the viewport has its own design.
#pragma warning disable RS0030

/// <summary>Adds, selects and drags notes in playfield coordinates; the timeline strip is the shared timeline's.</summary>
public sealed class EditorNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly EditorHistory _history;
    private readonly EditorSceneQuery _scene;
    private readonly EditorItemPicker _picker;
    private readonly EditorHoverProbe _hover;
    private readonly EditorPolygonTool _polygonTool;

    private bool _wasPressed;

    /// <summary>
    /// The item a placement just selected. It stays selected for editing, but
    /// the next click on empty space places again rather than deselecting it.
    /// </summary>
    private Entity _placed;
    private readonly EditorPlacementCreator _creator;
    private Entity _draggedEntity;
    private readonly UiDragController _drag = new();
    private readonly Dictionary<int, Vector2> _dragStartPositions = new();
    private EditorSnapshot _dragBefore;
    private readonly NoteBox _box;
    private readonly EditorPaintTool _paint;
    private readonly EditorResizeTool _resize;
    private readonly EditorRotationTool _rotation;

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
        _polygonTool = new EditorPolygonTool(_scene, _picker, history);
        _paint = new EditorPaintTool(_scene, history);
        _resize = new EditorResizeTool(_scene, history);
        _rotation = new EditorRotationTool(_scene, history);
        _creator = new EditorPlacementCreator(_scene, history);
        _box = new NoteBox(this);
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;
        Entity editor = _scene.Editor();
        bool picked = HasPickedSelection(editor);
        _polygonTool.Update(editor);

        if (_scene.IsGroupTargetEditing())
        {
            _hover.Update(input.Position, pressed, picked);
            _wasPressed = pressed;
            return;
        }

        if (pressed && !_wasPressed)
            OnPress(input.Position, input.Shift);
        else if (pressed)
            OnHold(input.Position, input.Shift);
        else if (_wasPressed)
            OnRelease();

        _hover.Update(input.Position, pressed, picked);
        _wasPressed = pressed;
    }

    private void OnPress(Vector2 screenPosition, bool shift)
    {
        Entity editor = _scene.Editor();
        if (editor.IsNull || _scene.IsModalOpen())
            return;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        _box.Cancel(editor);
        _resize.Cancel();

        // The ruler strip is the shared timeline's (TimelineInputSystem, through EditorTimelineHost).
        if (EditorLayout.RulerRect(view).HasPoint(screenPosition))
            return;

        if (editor.HasComponent<EditorSidebar>() &&
            editor.GetComponent<EditorSidebar>().Bounds.HasPoint(screenPosition))
        {
            return;
        }

        if (_scene.IsOverNoteBar(screenPosition) || _scene.IsOverPanel(screenPosition))
            return;

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);

        // The polygon tool takes every press on the field, even over items, so outlines can cross them.
        if (_polygonTool.TryDraft(editor, screenPosition, view, playfield))
            return;

        // Handles hang over the field's edge, so they get the press before the
        // inside test would turn an edge drag away.
        if (_rotation.TryBegin(screenPosition, editor, view) || _resize.TryBegin(screenPosition, editor, view))
        {
            CancelDrag();
            return;
        }

        if (!EditorItemPicker.IsInside(playfieldPosition, playfield))
            return;

        Entity hit = _picker.FindItemAt(playfieldPosition, playhead.TimeMs);

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();

        // An empty spot with a block or spike tool starts a paint stroke instead
        // of arming a placement, so dragging lays a run. Shift still box-selects,
        // and a selection has to be cleared before anything new goes down.
        if (hit.IsNull && !shift && EditorPaintTool.IsGridMode(editor) && !HasPickedSelection(editor))
        {
            Entity painted = _paint.Begin(editor, playfieldPosition, playfield);
            if (!painted.IsNull)
                _placed = painted;
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
                if (selected.Kind == NoteKind.Hit)
                    editor.GetComponent<EditorPlacement>().Note = selected.Target;
            }

            return;
        }

        // Shift-dragging adds to the selection; a plain press may place a note or
        // clear the selection when released without dragging, or box-select when dragged.
        if (shift)
            _box.BeginAdditive(screenPosition, selection.AllNotes);
        else
            _box.Arm(screenPosition);
    }

    private void OnHold(Vector2 screenPosition, bool shift)
    {
        Entity editor = _scene.Editor();
        if (editor.IsNull || _scene.IsModalOpen())
            return;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        if (_rotation.Active)
        {
            _rotation.Update(screenPosition, shift, editor, view, playfield);
            return;
        }

        if (_resize.Active)
        {
            _resize.Update(screenPosition, shift, view, playfield);
            return;
        }

        if (_box.Drag(editor, screenPosition))
            return;

        if (_paint.Active)
        {
            Entity painted = _paint.Paint(editor, view.ScreenToPlayfield(screenPosition), playfield);
            if (!painted.IsNull)
                _placed = painted;
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
                bool snap = ViewportSnap.IsOn(_entities);
                Vector2 corner = ItemKinds.Of(entity)!.SettleDrag(start + delta, box.Size, playfield, snap);
                GridPlacement.SetBounds(entity, new Rect2(corner, box.Size));
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
        Entity editor = _scene.Editor();

        if (_paint.Active)
        {
            _paint.End(editor);
            return;
        }

        if (_rotation.Active)
        {
            _rotation.End(editor);
            return;
        }

        if (_resize.Active)
        {
            _resize.End();
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

            _creator.Create(editor, _box.Start);
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

}
