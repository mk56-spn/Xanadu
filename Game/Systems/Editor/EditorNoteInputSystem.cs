using System.Collections.Generic;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Kinds;
using Xanadu.Editor;
using Xanadu.Editor.History;
using Xanadu.Systems.Input;
using Xanadu.Model.Playfields;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Interaction;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Editor;

namespace Xanadu.Systems.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): viewport: placing and dragging on the playfield, until the viewport has its own design.
#pragma warning disable RS0030

/// <summary>
/// Lays route and hold notes (through <see cref="EditorSpanTool"/>), drags out dash notes, and selects and drags notes,
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
    private readonly EditorSpanTool _spanTool;

    private bool _wasPressed;
    private bool _wasSecondary;

    /// <summary>
    /// The item a placement just selected. It stays selected for editing, but
    /// the next click on empty space places again rather than deselecting it.
    /// </summary>
    private Entity _placed;
    private Entity _draggedEntity;
    private readonly UiDragController _drag = new();
    private readonly Dictionary<int, Vector2> _dragStartPositions = new();
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
        _spanTool = new EditorSpanTool(_scene, history);
        _box = new NoteBox(this);
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressed = input.Pressed;
        Entity editor = _scene.Editor();
        bool picked = HasPickedSelection(editor);
        _spanTool.Update(editor, FieldPointer(editor, input.Position));
        if (input.SecondaryPressed && !_wasSecondary)
            OnSecondaryPress(editor, input.Position);
        _wasSecondary = input.SecondaryPressed;
        TakeSpanPlaced();

        if (pressed && !_wasPressed)
            OnPress(input.Position, input.Shift);
        else if (pressed)
            OnHold(input.Position, input.Shift);
        else if (_wasPressed)
            OnRelease();

        _hover.Update(input.Position, pressed, picked);
        _wasPressed = pressed;
    }

    /// <summary>The pointer in playfield units when it is over the open field, not over any editor UI.</summary>
    private Vector2? FieldPointer(Entity editor, Vector2 screenPosition)
    {
        if (editor.IsNull || _scene.IsModalOpen() || IsOverEditorUi(editor, screenPosition))
            return null;
        Vector2 point = editor.GetComponent<PlayfieldView>().ScreenToPlayfield(screenPosition);
        return PlayfieldGrid.IsInside(point, editor.GetComponent<Playfield>()) ? point : null;
    }

    private bool IsOverEditorUi(Entity editor, Vector2 screenPosition) =>
        EditorLayout.RulerRect(editor.GetComponent<PlayfieldView>()).HasPoint(screenPosition) ||
        (editor.TryGetComponent(out EditorSidebar sidebar) && sidebar.Bounds.HasPoint(screenPosition)) ||
        _scene.IsOverNoteBar(screenPosition) ||
        _scene.IsOverPanel(screenPosition);

    /// <summary>Right click over the field ends the route or hold being laid, as in osu!'s editor.</summary>
    private void OnSecondaryPress(Entity editor, Vector2 screenPosition)
    {
        if (editor.IsNull || _scene.IsModalOpen() || IsOverEditorUi(editor, screenPosition))
            return;
        _spanTool.Finish(editor, screenPosition, editor.GetComponent<PlayfieldView>(), editor.GetComponent<Playfield>());
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

        // The ruler strip is the shared timeline's (TimelineInputSystem, through EditorTimelineHost).
        if (IsOverEditorUi(editor, screenPosition))
            return;

        Vector2 playfieldPosition = view.ScreenToPlayfield(screenPosition);

        // A route or hold being laid takes every press, even over notes: each one marks a time.
        if (_spanTool.Drafting(editor) && _spanTool.TryDraft(editor, screenPosition, view, playfield))
        {
            TakeSpanPlaced();
            return;
        }

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

            // The key picker shows the picked note's key, so the next curve matches it.
            if (hit.HasComponent<Note>() && editor.HasComponent<EditorPlacement>())
                editor.GetComponent<EditorPlacement>().Note = hit.GetComponent<Note>().Target;

            return;
        }

        // Shift-dragging adds to the selection; a plain press starts a dash, or
        // (released without dragging) clears the selection or starts a route or hold,
        // or box-selects when dragged.
        if (shift)
            _box.BeginAdditive(screenPosition, selection.AllNotes);
        else if (IsDragTool(editor) && !HasPickedSelection(editor))
            StartDragDraft(editor, playfieldPosition, playhead, playfield);
        else
            _box.Arm(screenPosition);
    }

    /// <summary>Whether the current tool lays its note by pressing and dragging out (dash).</summary>
    private static bool IsDragTool(Entity editor) =>
        editor.TryGetComponent(out EditorPlacement placement) &&
        placement.Mode is EditorPlacementMode.Dash;

    /// <summary>A note the span tool just laid counts as just placed: the next empty click places again.</summary>
    private void TakeSpanPlaced()
    {
        if (_spanTool.Placed.IsNull)
            return;
        _placed = _spanTool.Placed;
        _spanTool.Placed = default;
    }

    /// <summary>Puts a dash's head down at the snapped playhead; dragging then sets its length.</summary>
    private void StartDragDraft(Entity editor, Vector2 point, Playhead playhead, Playfield playfield)
    {
        if (editor.GetComponent<LevelInfo>().Song is null)
        {
            EditorWarningPopup.Request(editor, EditorWarningPopup.NoSongMessage);
            return;
        }

        if (ViewportSnap.IsOn(_entities))
            point = PlayfieldGrid.NearestCrossing(point, playfield);
        ref EditorDragDraft draft = ref editor.GetComponent<EditorDragDraft>();
        draft.Active = true;
        draft.Mode = editor.GetComponent<EditorPlacement>().Mode;
        draft.Head = point;
        draft.Current = point;
        draft.StartMs = EditorTiming.Snap(playhead.TimeMs, playhead);
    }

    /// <summary>Turns the dragged-out draft into a selected note, as one undo step.</summary>
    private void FinishDragDraft(Entity editor, ref EditorDragDraft draft)
    {
        draft.Active = false;
        if (EditorItems.WouldOverfill(_entities, editor))
            return;

        Note note = EditorPlacementNote.Build(editor, draft.StartMs, draft.Head, draft.Current);
        EditorSnapshot before = _history.Capture();
        Entity created = EditorItems.Place(_entities, note);
        _history.Record(before, _history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
        _placed = created;
    }

    private void OnHold(Vector2 screenPosition, bool shift)
    {
        Entity editor = _scene.Editor();
        if (editor.IsNull || _scene.IsModalOpen())
            return;

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        if (editor.TryGetComponent(out EditorDragDraft dragDraft) && dragDraft.Active)
        {
            Vector2 to = view.ScreenToPlayfield(screenPosition);
            if (ViewportSnap.IsOn(_entities))
                to = PlayfieldGrid.NearestCrossing(to, playfield);
            editor.GetComponent<EditorDragDraft>().Current = to;
            return;
        }

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

        if (!editor.IsNull && editor.TryGetComponent(out EditorDragDraft dragDraft) && dragDraft.Active)
        {
            FinishDragDraft(editor, ref editor.GetComponent<EditorDragDraft>());
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

            // A click on empty field starts a route or hold there.
            if (!EditorSpanTool.Lays(editor.GetComponent<EditorPlacement>().Mode))
                return;
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            _spanTool.TryDraft(editor, _box.Start, view, editor.GetComponent<Playfield>());
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
