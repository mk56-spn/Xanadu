using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>Dragging one side or corner handle of the primary selected boxed item.</summary>
public sealed class EditorResizeTool(EditorSceneQuery scene, EditorHistory history)
{
    private Entity _entity;
    private GridHandle _handle;
    private Rect2 _start;
    private float _degrees;
    private EditorSnapshot _before;

    public bool Active => !_entity.IsNull;

    /// <summary>
    /// Starts a resize when the press lands on a handle of the primary selected
    /// block or spike. Returns true when the drag now belongs to the handle.
    /// </summary>
    public bool TryBegin(Vector2 screenPosition, Entity editor, PlayfieldView view)
    {
        if (!editor.HasComponent<NoteSelection>())
            return false;

        Entity primary = editor.GetComponent<NoteSelection>().Note;
        // The handles sit on the item where its groups show it; the bounds stay placed.
        view = GroupOffset.View(primary, view);
        if (!EditorItems.IsItem(primary) ||
            !GridPlacement.TryGet(primary, out Rect2 bounds, out float degrees) ||
            !GridPlacement.TryPickHandle(bounds, degrees, view, screenPosition, out GridHandle handle))
        {
            return false;
        }

        _entity = primary;
        _handle = handle;
        _start = bounds;
        _degrees = degrees;
        _before = history.Capture();
        return true;
    }

    /// <summary>Drags one side or corner of the item being resized; <paramref name="keepRatio"/> (shift) holds a corner's proportions.</summary>
    public void Update(Vector2 screenPosition, bool keepRatio, PlayfieldView view, Playfield playfield)
    {
        if (!EditorItems.IsItem(_entity))
            return;

        GridPlacement.SetBounds(_entity, GridPlacement.Resize(
            _start,
            _handle,
            GroupOffset.View(_entity, view).ScreenToPlayfield(screenPosition),
            playfield,
            ViewportSnap.IsOn(scene.Store),
            _degrees,
            keepRatio));
    }

    public void End()
    {
        history.Record(_before, history.Capture());
        _entity = default;
    }

    /// <summary>Drops a resize the press was about to start on another control.</summary>
    public void Cancel() => _entity = default;
}
