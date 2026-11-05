using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// A paint stroke laying blocks or spikes: the whole stroke is one undo step,
/// and the newest item stays selected so its handles show.
/// </summary>
public sealed class EditorPaintTool(EditorSceneQuery scene, EditorHistory history)
{
    private bool _painting;
    private bool _added;
    private EditorSnapshot _before;

    public bool Active => _painting;

    /// <summary>Whether the current tool paints blocks or spikes.</summary>
    public static bool IsGridMode(Entity editor) =>
        IsMode(editor, EditorPlacementMode.Blocks) || IsMode(editor, EditorPlacementMode.Spikes);

    /// <summary>Starts a stroke and lays its first item; the created item, or null when the spot is taken.</summary>
    public Entity Begin(Entity editor, Vector2 pointer, Playfield playfield)
    {
        _painting = true;
        _added = false;
        _before = history.Capture();
        return Paint(editor, pointer, playfield);
    }

    /// <summary>
    /// Lays an item of the current tool under the pointer unless one is already
    /// there, so dragging paints a run of them. Returns the created item.
    /// </summary>
    public Entity Paint(Entity editor, Vector2 pointer, Playfield playfield)
    {
        if (!EditorItemPicker.IsInside(pointer, playfield))
            return default;

        EntityStore entities = scene.Store;
        float degrees = editor.TryGetComponent(out EditorPlacement brush) ? brush.Degrees : 0f;
        BlockBrushSize blockSize = brush.BlockSize;
        bool snap = ViewportSnap.IsOn(entities);
        Entity created;
        if (IsMode(editor, EditorPlacementMode.Spikes))
        {
            Vector2 size = GridPlacement.NewSpikeSize(playfield);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (!GridPlacement.IsSpikeSpaceFree(entities, box))
                return default;

            created = EditorItems.Place(entities, new Spike(box.Position, box.Size, degrees));
        }
        else
        {
            Vector2 size = GridPlacement.NewBlockSize(playfield, degrees, blockSize);
            var box = new Rect2(GridPlacement.Place(pointer, size, playfield, snap), size);
            if (!GridPlacement.IsBlockSpaceFree(entities, box))
                return default;

            created = EditorItems.Place(entities, new Block(box.Position, box.Size));
        }

        _added = true;
        editor.GetComponent<NoteSelection>().Note = created;
        return created;
    }

    /// <summary>Ends the stroke, recording it as one undo step when it laid anything.</summary>
    public void End(Entity editor)
    {
        _painting = false;
        if (_added && !editor.IsNull)
            history.Record(_before, history.Capture());

        _added = false;
        _before = default;
    }

    private static bool IsMode(Entity editor, EditorPlacementMode mode) =>
        editor.TryGetComponent(out EditorPlacement placement) && placement.Mode == mode;
}
