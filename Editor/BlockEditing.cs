using Godot;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Which side of a block a resize starts from.</summary>
public enum BlockHandle
{
    None,
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>
/// Handle geometry and drag maths for blocks. Handles sit at the middle of
/// each side and keep a fixed screen size, so they stay grabbable at any
/// playfield scale.
/// </summary>
public static class BlockEditing
{
    /// <summary>Side of a handle, in screen pixels.</summary>
    public const float HandleSize = 10f;

    /// <summary>How far from a handle's centre a click still grabs it.</summary>
    public const float HandleHitRadius = 12f;

    public static readonly BlockHandle[] Sides =
    {
        BlockHandle.Left,
        BlockHandle.Right,
        BlockHandle.Top,
        BlockHandle.Bottom,
    };

    /// <summary>Smallest block is one grid cell on each axis.</summary>
    public static Vector2 MinSize(Playfield playfield) => BlockGrid.Cell(playfield);

    /// <summary>The playfield point a handle sits on: the middle of one side.</summary>
    public static Vector2 HandleCenter(Block block, BlockHandle handle) => handle switch
    {
        BlockHandle.Left => new Vector2(block.Position.X, block.Center.Y),
        BlockHandle.Right => new Vector2(block.End.X, block.Center.Y),
        BlockHandle.Top => new Vector2(block.Center.X, block.Position.Y),
        BlockHandle.Bottom => new Vector2(block.Center.X, block.End.Y),
        _ => block.Center,
    };

    /// <summary>The handle's screen rect, a fixed size whatever the view scale.</summary>
    public static Rect2 ScreenRect(Block block, BlockHandle handle, PlayfieldView view)
    {
        Vector2 screen = view.PlayfieldToScreen(HandleCenter(block, handle));
        return new Rect2(
            screen - new Vector2(HandleSize, HandleSize) / 2,
            new Vector2(HandleSize, HandleSize));
    }

    /// <summary>True when a screen point is close enough to grab a handle.</summary>
    public static bool TryPick(
        Block block,
        PlayfieldView view,
        Vector2 screenPoint,
        out BlockHandle handle)
    {
        foreach (BlockHandle side in Sides)
        {
            Vector2 screen = view.PlayfieldToScreen(HandleCenter(block, side));
            if (screen.DistanceTo(screenPoint) <= HandleHitRadius)
            {
                handle = side;
                return true;
            }
        }

        handle = BlockHandle.None;
        return false;
    }

    /// <summary>Moves a block by a drag delta, keeping both edges on the grid when snapping.</summary>
    public static Block Move(Block start, Vector2 delta, Playfield playfield, bool snap = true)
    {
        Vector2 moved = start.Position + delta;
        return new Block(
            BlockGrid.Clamp(
                snap ? BlockGrid.Snap(moved, BlockGrid.Cell(playfield)) : moved,
                start.Size,
                playfield),
            start.Size,
            start.ShapeId,
            start.ShapePiece);
    }

    /// <summary>
    /// Block after dragging one side to a playfield point. The opposite edge
    /// stays put, the dragged edge snaps to the grid when snapping, and the
    /// block never gets smaller than one cell or leaves the field.
    /// </summary>
    public static Block Resize(
        Block start,
        BlockHandle handle,
        Vector2 pointer,
        Playfield playfield,
        bool snap = true)
    {
        Vector2 cell = BlockGrid.Cell(playfield);
        Vector2 min = MinSize(playfield);
        Vector2 point = snap ? BlockGrid.Snap(pointer, cell) : pointer;
        Vector2 position = start.Position;
        Vector2 size = start.Size;

        switch (handle)
        {
            case BlockHandle.Left:
            {
                float right = start.End.X;
                position.X = Mathf.Clamp(point.X, 0, right - min.X);
                size.X = right - position.X;
                break;
            }

            case BlockHandle.Right:
            {
                float right = Mathf.Clamp(
                    point.X,
                    start.Position.X + min.X,
                    playfield.Width);
                size.X = right - start.Position.X;
                break;
            }

            case BlockHandle.Top:
            {
                float bottom = start.End.Y;
                position.Y = Mathf.Clamp(point.Y, 0, bottom - min.Y);
                size.Y = bottom - position.Y;
                break;
            }

            case BlockHandle.Bottom:
            {
                float bottom = Mathf.Clamp(
                    point.Y,
                    start.Position.Y + min.Y,
                    playfield.Height);
                size.Y = bottom - start.Position.Y;
                break;
            }
        }

        return new Block(position, size, start.ShapeId, start.ShapePiece);
    }
}
