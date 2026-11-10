using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// A paint stroke laying blocks or spikes: the whole stroke is one undo step,
/// and the newest item stays selected so its handles show. A single click
/// follows the grid snap toggle, but a drag always paints on the grid: once a
/// stroke lays a second item it snaps, and moves its first onto the grid too,
/// so a run can never smear into a pile of overlapping, offset copies.
/// </summary>
public sealed class EditorPaintTool(EditorSceneQuery scene, EditorHistory history)
{
    private bool _painting;
    private bool _added;
    private EditorSnapshot _before;

    /// <summary>What this stroke has laid, so holding still never stacks copies even with collision off.</summary>
    private readonly HashSet<Rect2> _laid = new();

    /// <summary>The stroke's first item and where the press was, to snap it once the stroke becomes a drag.</summary>
    private Entity _first;
    private Vector2 _firstPointer;

    /// <summary>Whether the pointer has moved far enough from the press for the stroke to be a drag.</summary>
    private bool _dragging;

    public bool Active => _painting;

    /// <summary>Whether the current tool paints blocks or spikes.</summary>
    public static bool IsGridMode(Entity editor) =>
        IsMode(editor, EditorPlacementMode.Blocks) || IsMode(editor, EditorPlacementMode.Spikes);

    /// <summary>Starts a stroke and lays its first item; the created item, or null when the spot is taken.</summary>
    public Entity Begin(Entity editor, Vector2 pointer, Playfield playfield)
    {
        _painting = true;
        _added = false;
        _laid.Clear();
        _first = default;
        _dragging = false;
        _before = history.Capture();
        Entity first = Paint(editor, pointer, playfield);
        _first = first;
        _firstPointer = pointer;
        return first;
    }

    /// <summary>
    /// Lays an item of the current tool under the pointer, so dragging paints a
    /// run of them. A spot this stroke already filled is skipped, and with
    /// collision on so is any spot one of its kind holds. Returns the created item.
    /// </summary>
    public Entity Paint(Entity editor, Vector2 pointer, Playfield playfield)
    {
        if (!EditorItemPicker.IsInside(pointer, playfield))
            return default;

        EntityStore entities = scene.Store;
        if (EditorItems.WouldOverfill(entities, editor))
            return default;

        float degrees = editor.TryGetComponent(out EditorPlacement brush) ? brush.Degrees : 0f;
        BlockBrushSize blockSize = brush.BlockSize;
        // Once the pointer leaves the press the stroke is a drag, and a drag
        // always paints on the grid. Holding still lays nothing more.
        if (_added && !_dragging)
        {
            if (pointer.DistanceTo(_firstPointer) < GridPlacement.Cell(playfield).X / 4)
                return default;

            _dragging = true;
            SnapFirst(entities, playfield);
        }

        bool snap = _dragging || ViewportSnap.IsOn(entities);

        Entity created;
        if (IsMode(editor, EditorPlacementMode.Spikes))
        {
            Vector2 size = GridPlacement.NewSpikeSize(playfield);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (_laid.Contains(box) || !PlacementCollision.SpikeFits(entities, box))
                return default;

            created = EditorItems.Place(entities, new Spike(box.Position, box.Size, degrees));
        }
        else
        {
            Vector2 size = GridPlacement.NewBlockSize(playfield, degrees, blockSize);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (_laid.Contains(box) || !PlacementCollision.BlockFits(entities, box))
                return default;

            created = EditorItems.Place(entities, new Block(box.Position, box.Size));
        }

        _added = true;
        _laid.Add(created.TryGetComponent(out Block laidBlock) ? laidBlock.Bounds : created.GetComponent<Spike>().Bounds);
        editor.GetComponent<NoteSelection>().Note = created;
        return created;
    }

    /// <summary>Moves the stroke's first item, laid where the press was, onto the grid when that spot is free.</summary>
    private void SnapFirst(EntityStore entities, Playfield playfield)
    {
        Entity first = _first;
        _first = default;
        if (!EditorItems.IsItem(first) || !GridPlacement.TryGet(first, out Rect2 box, out _))
            return;

        var snapped = new Rect2(GridPlacement.Place(_firstPointer, box.Size, playfield, snap: true), box.Size);
        if (snapped == box || !FitsBesides(entities, first, snapped))
            return;

        _laid.Remove(box);
        GridPlacement.SetBounds(first, snapped);
        _laid.Add(snapped);
    }

    /// <summary>Whether a box is free of its kind, the item being moved aside, when collision is on.</summary>
    private static bool FitsBesides(EntityStore entities, Entity moving, Rect2 box)
    {
        if (!PlacementCollision.IsOn(entities))
            return true;

        bool spike = moving.HasComponent<Spike>();
        foreach (Entity other in spike ? entities.Query<Spike>().ToEntityList() : entities.Query<Block>().ToEntityList())
        {
            if (other.Id == moving.Id)
                continue;

            Rect2 bounds = spike ? other.GetComponent<Spike>().Bounds : other.GetComponent<Block>().Bounds;
            if (bounds.Intersects(box))
                return false;
        }

        return true;
    }

    /// <summary>Ends the stroke, recording it as one undo step when it laid anything.</summary>
    public void End(Entity editor)
    {
        _painting = false;
        if (_added && !editor.IsNull)
            history.Record(_before, history.Capture());

        _added = false;
        _laid.Clear();
        _first = default;
        _dragging = false;
        _before = default;
    }

    private static bool IsMode(Entity editor, EditorPlacementMode mode) =>
        editor.TryGetComponent(out EditorPlacement placement) && placement.Mode == mode;
}
