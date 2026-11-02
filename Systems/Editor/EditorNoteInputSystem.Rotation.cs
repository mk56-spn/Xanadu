using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>Dragging the handle above a selection turns it around its centre.</summary>
public sealed partial class EditorNoteInputSystem
{
    private readonly List<RotationStart> _rotationStarts = new();
    private bool _rotating;
    private bool _rotationMoved;
    private Vector2 _rotationGrab;
    private EditorSnapshot _rotationBefore;

    /// <summary>Starts a rotation when the press lands on the handle. True when the drag now belongs to it.</summary>
    private bool TryBeginRotation(
        Vector2 screenPosition,
        Entity editor,
        PlayfieldView view)
    {
        if (!editor.HasComponent<NoteSelection>())
            return false;

        IReadOnlyList<Entity> selection = editor.GetComponent<NoteSelection>().AllNotes;
        if (!SelectionRotation.TryGetBounds(selection, out Rect2 bounds))
            return false;

        Vector2 handle = SelectionRotation.HandlePoint(bounds, view);
        if (handle.DistanceTo(screenPosition) > SelectionRotation.HandleHitRadius)
            return false;

        CancelDrag();
        _rotationStarts.Clear();
        foreach (Entity entity in selection)
        {
            if (EditorItems.IsItem(entity) && EditorItemPosition.TryGet(entity, out _))
                _rotationStarts.Add(SelectionRotation.Capture(entity));
        }

        _rotating = true;
        _rotationMoved = false;
        _rotationGrab = screenPosition;
        _rotationBefore = _history.Capture();
        editor.AddComponent(new EditorSelectionRotation(
            bounds.GetCenter(),
            handle - view.PlayfieldToScreen(bounds.GetCenter())));
        return true;
    }

    private void UpdateRotation(
        Vector2 screenPosition,
        bool stepped,
        Entity editor,
        PlayfieldView view,
        Playfield playfield)
    {
        ref EditorSelectionRotation rotation = ref editor.GetComponent<EditorSelectionRotation>();
        float angle = SelectionRotation.AngleBetween(
            view.PlayfieldToScreen(rotation.Pivot),
            _rotationGrab,
            screenPosition,
            stepped);
        if (angle == rotation.Angle)
            return;

        rotation.Angle = angle;
        _rotationMoved = true;
        bool snap = ViewportSnap.IsOn(_entities);
        foreach (RotationStart start in _rotationStarts)
        {
            if (EditorItems.IsItem(start.Entity))
                SelectionRotation.Apply(start, rotation.Pivot, angle, playfield, snap);
        }
    }

    private void EndRotation(Entity editor)
    {
        if (!editor.IsNull && editor.HasComponent<EditorSelectionRotation>())
            editor.RemoveComponent<EditorSelectionRotation>();
        if (_rotationMoved)
            _history.Record(_rotationBefore, _history.Capture());

        _rotating = false;
        _rotationMoved = false;
        _rotationStarts.Clear();
        _rotationBefore = default;
    }
}
