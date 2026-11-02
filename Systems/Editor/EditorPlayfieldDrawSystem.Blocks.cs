using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>Resize handles for the selected block or spike.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    /// <summary>
    /// Draws one nub at the middle of each side of the primary selected block or
    /// spike. They are the grips the pointer grabs to resize it.
    /// </summary>
    private void DrawGridHandles(PlayfieldView view, NoteSelection selection)
    {
        Entity primary = selection.Note;
        if (!EditorItems.IsItem(primary) || !GridPlacement.TryGet(primary, out Rect2 bounds, out float degrees))
            return;

        foreach (GridHandle handle in GridPlacement.Sides)
        {
            Rect2 nub = GridPlacement.HandleScreenRect(bounds, handle, degrees, view);
            _canvas.DrawRect(_editor, nub, PlayfieldRenderer.HandleEdge);
            _canvas.DrawRect(_editor, nub.Grow(-2), PlayfieldRenderer.BlockSelectedEdge);
        }
    }
}
