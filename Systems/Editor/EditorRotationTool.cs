using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>Dragging the handle above a selection turns it around its centre.</summary>
public sealed class EditorRotationTool(EditorSceneQuery scene, EditorHistory history)
{
    private readonly List<RotationStart> _starts = new();
    private bool _moved;
    private Vector2 _grab;
    private EditorSnapshot _before;

    public bool Active { get; private set; }

    /// <summary>Starts a rotation when the press lands on the handle. True when the drag now belongs to it.</summary>
    public bool TryBegin(Vector2 screenPosition, Entity editor, PlayfieldView view)
    {
        if (!editor.HasComponent<NoteSelection>())
            return false;

        IReadOnlyList<Entity> selection = editor.GetComponent<NoteSelection>().AllNotes;
        if (!SelectionRotation.TryGetBounds(selection, out Rect2 bounds))
            return false;

        // The handle is shown with the primary item's group move; the pivot stays placed.
        view = GroupOffset.View(editor.GetComponent<NoteSelection>().Note, view);

        Vector2 handle = SelectionRotation.HandlePoint(bounds, view);
        if (handle.DistanceTo(screenPosition) > SelectionRotation.HandleHitRadius)
            return false;

        _starts.Clear();
        foreach (Entity entity in selection)
        {
            if (EditorItems.IsItem(entity) && EditorItemPosition.TryGet(entity, out _))
                _starts.Add(SelectionRotation.Capture(entity));
        }

        Active = true;
        _moved = false;
        _grab = screenPosition;
        _before = history.Capture();
        editor.AddComponent(new EditorSelectionRotation(
            bounds.GetCenter(),
            handle - view.PlayfieldToScreen(bounds.GetCenter())));
        return true;
    }

    public void Update(
        Vector2 screenPosition,
        bool stepped,
        Entity editor,
        PlayfieldView view,
        Playfield playfield)
    {
        ref EditorSelectionRotation rotation = ref editor.GetComponent<EditorSelectionRotation>();
        view = GroupOffset.View(editor.GetComponent<NoteSelection>().Note, view);
        float angle = SelectionRotation.AngleBetween(
            view.PlayfieldToScreen(rotation.Pivot),
            _grab,
            screenPosition,
            stepped);
        if (angle == rotation.Angle)
            return;

        rotation.Angle = angle;
        _moved = true;
        bool snap = ViewportSnap.IsOn(scene.Store);
        foreach (RotationStart start in _starts)
        {
            if (EditorItems.IsItem(start.Entity))
                SelectionRotation.Apply(start, rotation.Pivot, angle, playfield, snap);
        }
    }

    public void End(Entity editor)
    {
        if (!editor.IsNull && editor.HasComponent<EditorSelectionRotation>())
            editor.RemoveComponent<EditorSelectionRotation>();
        if (_moved)
            history.Record(_before, history.Capture());

        Active = false;
        _moved = false;
        _starts.Clear();
        _before = default;
    }
}
