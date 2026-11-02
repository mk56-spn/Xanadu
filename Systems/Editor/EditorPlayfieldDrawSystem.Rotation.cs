using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>The rotate handle above a selection that can turn.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    private void DrawRotationHandle(PlayfieldView view, NoteSelection selection)
    {
        Vector2 pivot;
        Vector2 handle;
        EntityList rotating = _entities.Query<EditorSelectionRotation>().ToEntityList();
        if (rotating.Count > 0)
        {
            // Mid-drag the selection's box keeps changing, so the handle swings
            // around the pivot that was fixed when the drag began.
            EditorSelectionRotation rotation = rotating[0].GetComponent<EditorSelectionRotation>();
            pivot = view.PlayfieldToScreen(rotation.Pivot);
            handle = pivot + rotation.Arm.Rotated(rotation.Angle);
        }
        else
        {
            if (!SelectionRotation.TryGetBounds(selection.AllNotes, out Rect2 bounds))
                return;

            pivot = view.PlayfieldToScreen(bounds.GetCenter());
            handle = SelectionRotation.HandlePoint(bounds, view);
        }

        Color accent = PlayfieldRenderer.BlockSelectedEdge;
        _canvas.DrawLine(_editor, pivot, handle, accent, 1);
        _canvas.DrawCircle(_editor, pivot, 3, accent);
        _canvas.DrawCircle(_editor, handle, SelectionRotation.HandleRadius, accent);
        _canvas.DrawCircleOutline(
            _editor, handle, SelectionRotation.HandleRadius, PlayfieldRenderer.HandleEdge, 2);
    }
}
