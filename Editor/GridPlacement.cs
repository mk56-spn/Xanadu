using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Editor;

/// <summary>Which side of a block or spike a resize starts from.</summary>
public enum GridHandle
{
    None,
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>
/// The one set of rules boxed items are placed, moved and resized by: blocks,
/// spikes and grass. They sit on the grid the editor draws when snapping is on,
/// and stay inside the field. They differ only in how they turn: see
/// <see cref="SelectionRotation"/>.
/// </summary>
public static class GridPlacement
{
    /// <summary>Side of a resize handle, in screen pixels.</summary>
    public const float HandleSize = 10f;

    /// <summary>How far from a handle's centre a click still grabs it.</summary>
    public const float HandleHitRadius = 12f;

    public static readonly GridHandle[] Sides =
    {
        GridHandle.Left,
        GridHandle.Right,
        GridHandle.Top,
        GridHandle.Bottom,
    };

    public static Vector2 Cell(Playfield playfield) => PlayfieldGrid.Cell(playfield);

    /// <summary>The block a brush lays down, in cells, before the brush's turn.</summary>
    public static Vector2 BlockCells(BlockBrushSize brush) => brush switch
    {
        BlockBrushSize.Quarter => new Vector2(0.5f, 0.5f),
        BlockBrushSize.Platform => new Vector2(2f, 1f),
        _ => Vector2.One,
    };

    /// <summary>A new block fills its brush's cells, turned with the brush.</summary>
    public static Vector2 NewBlockSize(
        Playfield playfield,
        float degrees = 0f,
        BlockBrushSize brush = BlockBrushSize.Cell)
    {
        Vector2 cell = Cell(playfield);
        Vector2 cells = BlockCells(brush);
        return SelectionRotation.TurnedSize(
            new Vector2(cell.X * cells.X, cell.Y * cells.Y),
            Mathf.DegToRad(degrees));
    }

    /// <summary>A new spike fills one cell; the brush turns it, not its box.</summary>
    public static Vector2 NewSpikeSize(Playfield playfield) => Cell(playfield);

    /// <summary>Top-left of an item centred on the pointer, snapped when asked and kept in the field.</summary>
    public static Vector2 Place(Vector2 pointer, Vector2 size, Playfield playfield, bool snap = true) =>
        Settle(pointer - size / 2, size, playfield, snap);

    /// <summary>Top-left of an item dragged by <paramref name="delta"/>.</summary>
    public static Vector2 Move(Vector2 position, Vector2 size, Vector2 delta, Playfield playfield, bool snap = true) =>
        Settle(position + delta, size, playfield, snap);

    /// <summary>Snaps a top-left corner when asked and keeps the item in the field.</summary>
    public static Vector2 Settle(Vector2 position, Vector2 size, Playfield playfield, bool snap = true) =>
        Clamp(snap ? ViewportSnap.Snap(position, SnapStep(size, playfield)) : position, size, playfield);

    /// <summary>
    /// Snaps a grass patch's box by the edges that meet blocks: its left side
    /// onto a column line and its base onto a row line, so a patch laid along a
    /// block's top sits on it and starts at its corner. Its top, the blade tips,
    /// is left wherever its length puts it.
    /// </summary>
    public static Vector2 SettleGrass(Vector2 position, Vector2 size, Playfield playfield, bool snap = true)
    {
        Vector2 cell = Cell(playfield);
        if (snap && cell.X > 0 && cell.Y > 0)
        {
            position = new Vector2(
                Mathf.Round(position.X / cell.X) * cell.X,
                Mathf.Round((position.Y + size.Y) / cell.Y) * cell.Y - size.Y);
        }

        return Clamp(position, size, playfield);
    }

    /// <summary>Where a patch's base goes when placed at <paramref name="pointer"/>, its base's middle.</summary>
    public static Vector2 PlaceGrass(Vector2 pointer, GrassPatch patch, Playfield playfield, bool snap = true)
    {
        var size = new Vector2(patch.Width, patch.Length);
        Vector2 corner = SettleGrass(pointer - new Vector2(size.X / 2, size.Y), size, playfield, snap);
        return corner + new Vector2(size.X / 2, size.Y);
    }

    /// <summary>
    /// The grid an item snaps to: the field's cells, or its own size where that is
    /// finer, so a block smaller than a cell lands on its own edges and quarters tile.
    /// </summary>
    public static Vector2 SnapStep(Vector2 size, Playfield playfield)
    {
        Vector2 cell = Cell(playfield);
        return new Vector2(
            size.X > 0f && size.X < cell.X ? size.X : cell.X,
    /// <summary>
    /// Snaps a flare's box by its centre, onto the half-cell grid, and keeps
    /// the centre in the field. Its light may spill past the edges; it is a
    /// light, not a body, so only its core has to be on the field.
    /// </summary>
    public static Vector2 SettleFlare(Vector2 position, Vector2 size, Playfield playfield, bool snap = true)
    {
        Vector2 center = position + size / 2;
        Vector2 half = Cell(playfield) / 2;
        if (snap && half.X > 0 && half.Y > 0)
            center = ViewportSnap.Snap(center, half);
        center = new Vector2(Mathf.Clamp(center.X, 0, playfield.Width), Mathf.Clamp(center.Y, 0, playfield.Height));
        return center - size / 2;
    }

    /// <summary>Where a flare's centre goes when placed at <paramref name="pointer"/>.</summary>
    public static Vector2 PlaceFlare(Vector2 pointer, LevelFlare flare, Playfield playfield, bool snap = true)
    {
        var size = new Vector2(flare.Width, flare.Height);
        return SettleFlare(pointer - size / 2, size, playfield, snap) + size / 2;
    }

            size.Y > 0f && size.Y < cell.Y ? size.Y : cell.Y);
    }

    /// <summary>Keeps an item fully inside the field without breaking grid alignment.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Playfield playfield) => new(
        Mathf.Clamp(position.X, 0, Mathf.Max(0, playfield.Width - size.X)),
        Mathf.Clamp(position.Y, 0, Mathf.Max(0, playfield.Height - size.Y)));

    /// <summary>The playfield point a handle sits on: the middle of one side, turned with the item.</summary>
    public static Vector2 HandleCenter(Rect2 bounds, GridHandle handle, float degrees = 0f)
    {
        Vector2 center = bounds.GetCenter();
        Vector2 side = handle switch
        {
            GridHandle.Left => new Vector2(bounds.Position.X, center.Y),
            GridHandle.Right => new Vector2(bounds.End.X, center.Y),
            GridHandle.Top => new Vector2(center.X, bounds.Position.Y),
            GridHandle.Bottom => new Vector2(center.X, bounds.End.Y),
            _ => center,
        };
        return center + (side - center).Rotated(Mathf.DegToRad(degrees));
    }

    /// <summary>The handle's screen rect, a fixed size whatever the view scale.</summary>
    public static Rect2 HandleScreenRect(Rect2 bounds, GridHandle handle, float degrees, PlayfieldView view)
    {
        var size = new Vector2(HandleSize, HandleSize);
        return new Rect2(view.PlayfieldToScreen(HandleCenter(bounds, handle, degrees)) - size / 2, size);
    }

    /// <summary>True when a screen point is close enough to grab a handle.</summary>
    public static bool TryPickHandle(
        Rect2 bounds,
        float degrees,
        PlayfieldView view,
        Vector2 screenPoint,
        out GridHandle handle)
    {
        foreach (GridHandle side in Sides)
        {
            Vector2 screen = view.PlayfieldToScreen(HandleCenter(bounds, side, degrees));
            if (screen.DistanceTo(screenPoint) <= HandleHitRadius)
            {
                handle = side;
                return true;
            }
        }

        return handle != GridHandle.None;
    }

    /// <summary>
    /// The box after dragging one side to a playfield point. The opposite edge
    /// stays put, the dragged edge snaps when snapping, and the item never gets
    /// smaller than a cell or leaves the field. A turned spike resizes along its
    /// own axes, which do not lie on the grid, so it does not snap.
    /// </summary>
    public static Rect2 Resize(
        Rect2 start,
        GridHandle handle,
        Vector2 pointer,
        Playfield playfield,
        bool snap = true,
        float degrees = 0f)
    {
        Vector2 cell = Cell(playfield);
        var field = new Vector2(playfield.Width, playfield.Height);
        if (degrees == 0f)
            return ResizeAlongAxes(start, handle, snap ? ViewportSnap.Snap(pointer, cell) : pointer, cell, Vector2.Zero, field);

        float angle = Mathf.DegToRad(degrees);
        Vector2 center = start.GetCenter();
        Vector2 local = center + (pointer - center).Rotated(-angle);
        Vector2 unbounded = Vector2.One * float.MaxValue / 2;
        Rect2 resized = ResizeAlongAxes(start, handle, local, cell, -unbounded, unbounded);

        // The unmoved edge has to stay where it is on the field, which means
        // shifting the box's centre along the turned axis.
        Vector2 kept = HandleCenter(start, Opposite(handle));
        Vector2 keptOnField = center + (kept - center).Rotated(angle);
        Vector2 newCenter = keptOnField - (kept - resized.GetCenter()).Rotated(angle);
        return new Rect2(Clamp(newCenter - resized.Size / 2, resized.Size, playfield), resized.Size);
    }

    private static Rect2 ResizeAlongAxes(
        Rect2 start,
        GridHandle handle,
        Vector2 point,
        Vector2 min,
        Vector2 fieldStart,
        Vector2 fieldEnd)
    {
        Vector2 position = start.Position;
        Vector2 size = start.Size;
        switch (handle)
        {
            case GridHandle.Left:
                position.X = Mathf.Clamp(point.X, fieldStart.X, start.End.X - min.X);
                size.X = start.End.X - position.X;
                break;
            case GridHandle.Right:
                size.X = Mathf.Clamp(point.X, start.Position.X + min.X, fieldEnd.X) - start.Position.X;
                break;
            case GridHandle.Top:
                position.Y = Mathf.Clamp(point.Y, fieldStart.Y, start.End.Y - min.Y);
                size.Y = start.End.Y - position.Y;
                break;
            case GridHandle.Bottom:
                size.Y = Mathf.Clamp(point.Y, start.Position.Y + min.Y, fieldEnd.Y) - start.Position.Y;
                break;
        }

        return new Rect2(position, size);
    }

    private static GridHandle Opposite(GridHandle handle) => handle switch
    {
        GridHandle.Left => GridHandle.Right,
        GridHandle.Right => GridHandle.Left,
        GridHandle.Top => GridHandle.Bottom,
        GridHandle.Bottom => GridHandle.Top,
        _ => GridHandle.None,
    };

    /// <summary>A boxed item's unturned box and turn (<see cref="ItemKind.TryGetBox"/>); false for the kinds that aren't boxes.</summary>
    public static bool TryGet(Entity entity, out Rect2 bounds, out float degrees)
    {
        bounds = default;
        degrees = 0f;
        return ItemKinds.Of(entity)?.TryGetBox(entity, out bounds, out degrees) == true;
    }

    /// <summary>Gives a boxed item a new box, leaving the rest of it alone.</summary>
    public static void SetBounds(Entity entity, Rect2 bounds) => ItemKinds.Of(entity)?.SetBox(entity, bounds);

    /// <summary>A box may not overlap another of its own kind; merely touching is fine.</summary>
    public static bool IsBlockSpaceFree(EntityStore entities, Rect2 box)
    {
        foreach (Entity entity in entities.Query<Block>().ToEntityList())
        {
            if (entity.GetComponent<Block>().Bounds.Intersects(box))
                return false;
        }

        return true;
    }

    /// <inheritdoc cref="IsBlockSpaceFree"/>
    public static bool IsSpikeSpaceFree(EntityStore entities, Rect2 box)
    {
        foreach (Entity entity in entities.Query<Spike>().ToEntityList())
        {
            if (entity.GetComponent<Spike>().Bounds.Intersects(box))
                return false;
        }

        return true;
    }
}
