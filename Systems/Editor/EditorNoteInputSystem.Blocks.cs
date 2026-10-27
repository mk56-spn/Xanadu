using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Block placement, picking and side-handle resizing. Split out of the note
/// input system because blocks are grid geometry, not timed items.
/// </summary>
public sealed partial class EditorNoteInputSystem
{
    /// <summary>Topmost block under a playfield point, or null.</summary>
    private Entity FindBlockAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<Block>().ToEntityList())
        {
            if (entity.GetComponent<Block>().Bounds.HasPoint(position))
                result = entity;
        }

        return result;
    }

    /// <summary>
    /// Starts a resize when the press lands on a handle of the primary
    /// selected block. Returns true when the drag now belongs to the handle.
    /// </summary>
    private bool TryBeginResize(
        Vector2 screenPosition,
        Entity editor,
        PlayfieldView view)
    {
        if (!editor.HasComponent<NoteSelection>())
            return false;

        Entity primary = editor.GetComponent<NoteSelection>().Note;
        if (!EditorItems.IsItem(primary) || !primary.HasComponent<Block>())
            return false;

        Block block = primary.GetComponent<Block>();
        if (!BlockEditing.TryPick(block, view, screenPosition, out BlockHandle handle))
            return false;

        CancelDrag();
        _resizeEntity = primary;
        _resizeHandle = handle;
        _resizeStart = block;
        _resizeBefore = _history.Capture();
        return true;
    }

    /// <summary>Drags one side of the block being resized, snapped to the grid.</summary>
    private void UpdateResize(
        Vector2 screenPosition,
        PlayfieldView view,
        Playfield playfield)
    {
        if (_resizeEntity.IsNull || !_resizeEntity.HasComponent<Block>())
            return;

        _resizeEntity.GetComponent<Block>() = BlockEditing.Resize(
            _resizeStart,
            _resizeHandle,
            view.ScreenToPlayfield(screenPosition),
            playfield);
    }

    /// <summary>Drops a default-sized block on the grid and selects it, so its handles show.</summary>
    private void PlaceBlock(Entity editor, Vector2 screenPosition)
    {
        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        Vector2 pointer = view.ScreenToPlayfield(screenPosition);
        if (!IsInside(pointer, playfield))
            return;

        Vector2 size = BlockGrid.DefaultSize(playfield);
        Vector2 position = BlockGrid.PlaceAt(pointer, size, playfield);

        EditorSnapshot before = _history.Capture();
        Entity created = EditorItems.Create(_entities, new Block(position, size));
        _history.Record(before, _history.Capture());
        editor.GetComponent<NoteSelection>().Note = created;
    }
}
