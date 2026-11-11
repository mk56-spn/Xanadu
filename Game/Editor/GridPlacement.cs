using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Editor;

/// <summary>Which side or corner of a boxed item a resize starts from.</summary>
public enum GridHandle
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
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

    /// <summary>Every grip a boxed item shows: the four sides and the four corners.</summary>
    public static readonly GridHandle[] Handles =
    {
        GridHandle.Left,
        GridHandle.Right,
        GridHandle.Top,
        GridHandle.Bottom,
        GridHandle.TopLeft,
        GridHandle.TopRight,
        GridHandle.BottomLeft,
        GridHandle.BottomRight,
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

    /// <summary>A new friction bar is two cells tall, enough to cross a standing player.</summary>
    public static float NewFrictionBarHeight(Playfield playfield) => Cell(playfield).Y * 2;

    /// <summary>
    /// Snaps a friction bar's box by its line and its foot: the line onto a
    /// column line and the bottom onto a row line, where block edges sit, so a
    /// bar stands on a block's top at its corner or a cell boundary.
    /// </summary>
    public static Vector2 SettleFrictionBar(Vector2 position, Vector2 size, Playfield playfield, bool snap = true)
    {
        Vector2 cell = Cell(playfield);
        if (snap && cell.X > 0 && cell.Y > 0)
        {
            position = new Vector2(
                Mathf.Round((position.X + size.X / 2) / cell.X) * cell.X - size.X / 2,
                Mathf.Round((position.Y + size.Y) / cell.Y) * cell.Y - size.Y);
        }

        return Clamp(position, size, playfield);
    }

    /// <summary>Top-left of a new friction bar centred on <paramref name="pointer"/>.</summary>
    public static Vector2 PlaceFrictionBar(Vector2 pointer, float height, Playfield playfield, bool snap = true)
    {
        var size = new Vector2(FrictionBar.Thickness, height);
        return SettleFrictionBar(pointer - size / 2, size, playfield, snap);
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
            size.Y > 0f && size.Y < cell.Y ? size.Y : cell.Y);
    }

    /// <summary>Keeps an item fully inside the field without breaking grid alignment.</summary>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Playfield playfield) => new(
        Mathf.Clamp(position.X, 0, Mathf.Max(0, playfield.Width - size.X)),
        Mathf.Clamp(position.Y, 0, Mathf.Max(0, playfield.Height - size.Y)));

    /// <summary>The playfield point a handle sits on: the middle of a side or a corner, turned with the item.</summary>
    public static Vector2 HandleCenter(Rect2 bounds, GridHandle handle, float degrees = 0f)
    {
        Vector2 center = bounds.GetCenter();
        Vector2 side = handle switch
        {
            GridHandle.Left => new Vector2(bounds.Position.X, center.Y),
            GridHandle.Right => new Vector2(bounds.End.X, center.Y),
            GridHandle.Top => new Vector2(center.X, bounds.Position.Y),
            GridHandle.Bottom => new Vector2(center.X, bounds.End.Y),
            GridHandle.TopLeft => bounds.Position,
            GridHandle.TopRight => new Vector2(bounds.End.X, bounds.Position.Y),
            GridHandle.BottomLeft => new Vector2(bounds.Position.X, bounds.End.Y),
            GridHandle.BottomRight => bounds.End,
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

    /// <summary>
    /// True when a screen point is close enough to grab a handle. On a small or
    /// zoomed-out item the grips overlap, so the nearest one wins.
    /// </summary>
    public static bool TryPickHandle(
        Rect2 bounds,
        float degrees,
        PlayfieldView view,
        Vector2 screenPoint,
        out GridHandle handle)
    {
        handle = GridHandle.None;
        float nearest = HandleHitRadius;
        foreach (GridHandle grip in Handles)
        {
            float distance = view.PlayfieldToScreen(HandleCenter(bounds, grip, degrees)).DistanceTo(screenPoint);
            if (distance <= nearest)
            {
                handle = grip;
                nearest = distance;
            }
        }

        return handle != GridHandle.None;
    }

    /// <summary>
    /// The box after dragging one side or corner to a playfield point. The
    /// opposite edge or corner stays put, the dragged edges snap when snapping, and the item never gets
    /// smaller than a cell or leaves the field. A turned spike resizes along its
    /// own axes, which do not lie on the grid, so it does not snap. With
    /// <paramref name="keepRatio"/> a corner scales the box evenly, following
    /// whichever side was pulled further.
    /// </summary>
    public static Rect2 Resize(
        Rect2 start,
        GridHandle handle,
        Vector2 pointer,
        Playfield playfield,
        bool snap = true,
        float degrees = 0f,
        bool keepRatio = false)
    {
        Vector2 cell = Cell(playfield);
        var field = new Vector2(playfield.Width, playfield.Height);
        if (degrees == 0f)
            return ResizeAlongAxes(start, handle, snap ? ViewportSnap.Snap(pointer, cell) : pointer, cell, Vector2.Zero, field, keepRatio);

        float angle = Mathf.DegToRad(degrees);
        Vector2 center = start.GetCenter();
        Vector2 local = center + (pointer - center).Rotated(-angle);
        Vector2 unbounded = Vector2.One * float.MaxValue / 2;
        Rect2 resized = ResizeAlongAxes(start, handle, local, cell, -unbounded, unbounded, keepRatio);

        // The unmoved edge has to stay where it is on the field, which means
        // shifting the box's centre along the turned axis.
        Vector2 kept = HandleCenter(start, Opposite(handle));
        Vector2 keptOnField = center + (kept - center).Rotated(angle);
        Vector2 newCenter = keptOnField - (kept - resized.GetCenter()).Rotated(angle);
        return new Rect2(Clamp(newCenter - resized.Size / 2, resized.Size, playfield), resized.Size);
    }

    /// <summary>A corner moves its two sides at once.</summary>
    private static Rect2 ResizeAlongAxes(
        Rect2 start,
        GridHandle handle,
        Vector2 point,
        Vector2 min,
        Vector2 fieldStart,
        Vector2 fieldEnd,
        bool keepRatio = false)
    {
        (GridHandle across, GridHandle up) = handle switch
        {
            GridHandle.TopLeft => (GridHandle.Left, GridHandle.Top),
            GridHandle.TopRight => (GridHandle.Right, GridHandle.Top),
            GridHandle.BottomLeft => (GridHandle.Left, GridHandle.Bottom),
            GridHandle.BottomRight => (GridHandle.Right, GridHandle.Bottom),
            _ => (handle, GridHandle.None),
        };
        if (keepRatio && up != GridHandle.None && start.Size.X > 0f && start.Size.Y > 0f)
            return ScaleFromCorner(start, across, up, point, min, fieldStart, fieldEnd);

        Rect2 resized = ResizeSide(start, across, point, min, fieldStart, fieldEnd);
        return ResizeSide(resized, up, point, min, fieldStart, fieldEnd);
    }

    /// <summary>
    /// Scales the box evenly about the corner opposite the one dragged. The
    /// scale follows the side pulled further, never shrinks the box below a
    /// cell on either side, and stops where either side meets the field's edge.
    /// </summary>
    private static Rect2 ScaleFromCorner(
        Rect2 start,
        GridHandle across,
        GridHandle up,
        Vector2 point,
        Vector2 min,
        Vector2 fieldStart,
        Vector2 fieldEnd)
    {
        bool left = across == GridHandle.Left;
        bool top = up == GridHandle.Top;
        var anchor = new Vector2(left ? start.End.X : start.Position.X, top ? start.End.Y : start.Position.Y);
        Vector2 pulled = new Vector2(left ? anchor.X - point.X : point.X - anchor.X, top ? anchor.Y - point.Y : point.Y - anchor.Y);
        Vector2 room = new Vector2(left ? anchor.X - fieldStart.X : fieldEnd.X - anchor.X, top ? anchor.Y - fieldStart.Y : fieldEnd.Y - anchor.Y);

        float scale = Mathf.Max(pulled.X / start.Size.X, pulled.Y / start.Size.Y);
        float smallest = Mathf.Max(min.X / start.Size.X, min.Y / start.Size.Y);
        float largest = Mathf.Min(room.X / start.Size.X, room.Y / start.Size.Y);
        scale = Mathf.Min(Mathf.Max(scale, smallest), Mathf.Max(largest, smallest));

        Vector2 size = start.Size * scale;
        var position = new Vector2(left ? anchor.X - size.X : anchor.X, top ? anchor.Y - size.Y : anchor.Y);
        return new Rect2(position, size);
    }

    private static Rect2 ResizeSide(
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
        GridHandle.TopLeft => GridHandle.BottomRight,
        GridHandle.TopRight => GridHandle.BottomLeft,
        GridHandle.BottomLeft => GridHandle.TopRight,
        GridHandle.BottomRight => GridHandle.TopLeft,
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
