using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Playfields;

namespace Writer.Systems.Editor;

/// <summary>Resize handles for the selected block.</summary>
public sealed partial class EditorPlayfieldDrawSystem
{
    /// <summary>
    /// Draws one nub at the middle of each side of the primary selected block.
    /// They are the grips the pointer grabs to resize it.
    /// </summary>
    private void DrawBlockHandles(PlayfieldView view, NoteSelection selection)
    {
        Entity primary = selection.Note;
        if (!EditorItems.IsItem(primary) || !primary.HasComponent<Block>())
            return;

        Block block = primary.GetComponent<Block>();
        foreach (BlockHandle handle in BlockEditing.Sides)
        {
            Rect2 nub = BlockEditing.ScreenRect(block, handle, view);
            _canvas.DrawRect(_editor, nub, PlayfieldRenderer.BlockEdge);
            _canvas.DrawRect(_editor, nub.Grow(-2), PlayfieldRenderer.BlockSelectedEdge);
        }
    }
}
