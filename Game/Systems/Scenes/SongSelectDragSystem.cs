using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Scenes;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Pressing a carousel row: moving the pointer far enough drags the list under
/// it and the selection follows the nearest level; letting go without moving is
/// a click, which selects the row. Horizontal movement means nothing, since the
/// rows only travel vertically. The list eases after the pointer rather than
/// sticking to it, so a drag glides.
/// </summary>
public sealed class SongSelectDragSystem(IPointerInputSource pointer, SceneSystem scenes)
    : QuerySystem<SongSelectState>
{
    /// <summary>How far the pointer travels before a click becomes a drag.</summary>
    private const float DragThreshold = 8;

    /// <summary>
    /// Exponential catch-up rate of the list toward the pointer, per second: a
    /// little behind the hand so a drag glides, quicker than the settle on release.
    /// </summary>
    public const float DragRate = 20f;

    private bool _holding;
    private bool _dragging;
    private int _pressedLevel;
    private float _anchorY;
    private float _anchorScroll;

    /// <summary>Where the pointer has pulled the list to; the list eases after it.</summary>
    private float _target;

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
            // A quick flick lets go before the list catches up: settle where the hand went.
            _dragging = false;
            root.RemoveTag<SongSelectDragging>();
            int wanted = Mathf.Clamp(Mathf.RoundToInt(_target), 0, Mathf.Max(state.Catalog.Count - 1, 0));
            if (wanted != state.Selected)
                scenes.TryStepCarousel(wanted - state.Selected);
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
        _target = state.Scroll;
        root.AddTag<SongSelectDragging>();
        return true;
    }

    private void Drag(Entity root, float y)
    {
        ref SongSelectState state = ref root.GetComponent<SongSelectState>();
        _target = _anchorScroll + SongSelectScene.LevelsDragged(_anchorY, y);
        float eased = Mathf.Lerp(state.Scroll, _target, 1 - Mathf.Exp(-DragRate * Tick.deltaTime));
        SongSelectScene.SetScroll(ref state, Mathf.Abs(_target - eased) < 0.002f ? _target : eased);

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
