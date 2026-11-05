using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Painting, picking and side-handle resizing of blocks and spikes, which share
/// all of it. Split out of the note input system because they are grid geometry,
/// not timed items.
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

    /// <summary>Topmost level text under a playfield point, or null.</summary>
    private Entity FindTextAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelText>().ToEntityList())
        {
            LevelText text = entity.GetComponent<LevelText>();
            if (TurnedBox.Contains(text.Bounds, text.RotationDegrees, position))
                result = entity;
        }

        return result;
    }

    /// <summary>Topmost grass patch under a playfield point, or null.</summary>
    private Entity FindGrassAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<GrassPatch>().ToEntityList())
        {
            GrassPatch grass = entity.GetComponent<GrassPatch>();
            if (TurnedBox.Contains(grass.Bounds, grass.RotationDegrees, position))
                result = entity;
        }

        return result;
    }

    /// <summary>Topmost polygon item with a point inside one of its outlines, or null.</summary>
    private Entity FindPolygonAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<LevelPolygon>().ToEntityList())
        {
            if (entity.GetComponent<LevelPolygon>().Contains(position))
                result = entity;
        }

        return result;
    }

    /// <summary>Topmost spike under a playfield point, or null.</summary>
    private Entity FindSpikeAt(Vector2 position)
    {
        Entity result = default;
        foreach (Entity entity in _entities.Query<Spike>().ToEntityList())
        {
            if (entity.GetComponent<Spike>().Contains(position))
                result = entity;
        }

        return result;
    }

    /// <summary>
    /// Starts a resize when the press lands on a handle of the primary selected
    /// block or spike. Returns true when the drag now belongs to the handle.
    /// </summary>
    private bool TryBeginResize(
        Vector2 screenPosition,
        Entity editor,
        PlayfieldView view)
    {
        if (!editor.HasComponent<NoteSelection>())
            return false;

        Entity primary = editor.GetComponent<NoteSelection>().Note;
        if (!EditorItems.IsItem(primary) ||
            !GridPlacement.TryGet(primary, out Rect2 bounds, out float degrees) ||
            !GridPlacement.TryPickHandle(bounds, degrees, view, screenPosition, out GridHandle handle))
        {
            return false;
        }

        CancelDrag();
        _resizeEntity = primary;
        _resizeHandle = handle;
        _resizeStart = bounds;
        _resizeDegrees = degrees;
        _resizeBefore = _history.Capture();
        return true;
    }

    /// <summary>Drags one side of the item being resized.</summary>
    private void UpdateResize(
        Vector2 screenPosition,
        PlayfieldView view,
        Playfield playfield)
    {
        if (!EditorItems.IsItem(_resizeEntity))
            return;

        GridPlacement.SetBounds(_resizeEntity, GridPlacement.Resize(
            _resizeStart,
            _resizeHandle,
            view.ScreenToPlayfield(screenPosition),
            playfield,
            ViewportSnap.IsOn(_entities),
            _resizeDegrees));
    }

    /// <summary>
    /// Lays an item of the current tool under the pointer unless one is already
    /// there, so dragging paints a run of them. The newest is selected, so its
    /// handles show and the sidebar can style it.
    /// </summary>
    private void PaintAt(Vector2 pointer, Playfield playfield, Entity editor)
    {
        if (!IsInside(pointer, playfield))
            return;

        float degrees = editor.TryGetComponent(out EditorPlacement brush) ? brush.Degrees : 0f;
        BlockBrushSize blockSize = brush.BlockSize;
        bool snap = ViewportSnap.IsOn(_entities);
        Entity created;
        if (IsMode(editor, EditorPlacementMode.Spikes))
        {
            Vector2 size = GridPlacement.NewSpikeSize(playfield);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (!GridPlacement.IsSpikeSpaceFree(_entities, box))
                return;

            created = EditorItems.Create(_entities, new Spike(box.Position, box.Size, degrees));
        }
        else
        {
            Vector2 size = GridPlacement.NewBlockSize(playfield, degrees, blockSize);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (!GridPlacement.IsBlockSpaceFree(_entities, box))
                return;

            created = EditorItems.Create(_entities, new Block(box.Position, box.Size));
        }

        _paintAdded = true;
        editor.GetComponent<NoteSelection>().Note = created;
        _placed = created;
    }

    private static bool IsGridMode(Entity editor) =>
        IsMode(editor, EditorPlacementMode.Blocks) || IsMode(editor, EditorPlacementMode.Spikes);

    private static bool IsMode(Entity editor, EditorPlacementMode mode) =>
        editor.TryGetComponent(out EditorPlacement placement) && placement.Mode == mode;
}
