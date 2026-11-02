using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Scenes;

/// <summary>
/// Pressing a carousel row: moving the pointer far enough drags the list under
/// it and the selection follows the nearest level; letting go without moving is
/// a click, which selects the row. Horizontal movement means nothing, since the
/// rows only travel vertically.
/// </summary>
public sealed class SongSelectDragSystem(IPointerInputSource pointer, SceneSystem scenes)
    : QuerySystem<SongSelectState>
{
    /// <summary>How far the pointer travels before a click becomes a drag.</summary>
    private const float DragThreshold = 8;

    private bool _holding;
    private bool _dragging;
    private int _pressedLevel;
    private float _anchorY;
    private float _anchorScroll;

    protected override void OnUpdate()
    {
        if (Query.Count == 0)
        {
            _holding = false;
            _dragging = false;
            return;
        }

        foreach (Entity root in Query.ToEntityList())
            UpdateList(root, pointer.Read().Position.Y);
    }

    private void UpdateList(Entity root, float y)
    {
        SongSelectState state = root.GetComponent<SongSelectState>();
        Entity tile = PressedTile(state);

        if (!_holding)
        {
            if (!tile.IsNull && !scenes.IsLevelDialogOpen)
                Grab(tile, y);
        }
        else if (tile.IsNull)
        {
            Release(root, state);
        }
        else if (_dragging || TryStartDrag(root, state, y))
        {
            Drag(root, y);
        }
    }

    private void Grab(Entity tile, float y)
    {
        _holding = true;
        _dragging = false;
        _pressedLevel = tile.GetComponent<SongSelectTile>().Index;
        _anchorY = y;
    }

    private void Release(Entity root, in SongSelectState state)
    {
        _holding = false;
        if (_dragging)
        {
            _dragging = false;
            root.RemoveTag<SongSelectDragging>();
        }
        else if (_pressedLevel >= 0)
        {
            scenes.TryStepCarousel(_pressedLevel - state.Selected);
        }
    }

    /// <summary>Starts from where the threshold was crossed, so the list doesn't jump.</summary>
    private bool TryStartDrag(Entity root, in SongSelectState state, float y)
    {
        if (state.Catalog.Count < 2 || Mathf.Abs(y - _anchorY) < DragThreshold)
            return false;

        _dragging = true;
        _anchorY = y;
        _anchorScroll = state.Scroll;
        root.AddTag<SongSelectDragging>();
        return true;
    }

    private void Drag(Entity root, float y)
    {
        ref SongSelectState state = ref root.GetComponent<SongSelectState>();
        SongSelectScene.SetScroll(ref state, _anchorScroll + SongSelectScene.LevelsDragged(_anchorY, y));

        int wanted = Mathf.RoundToInt(state.Scroll);
        if (wanted != state.Selected)
            scenes.TryStepCarousel(wanted - state.Selected);
    }

    private static Entity PressedTile(in SongSelectState state)
    {
        foreach (Entity tile in state.Tiles)
        {
            if (tile.Tags.Has<Pressed>())
                return tile;
        }

        return default;
    }
}
