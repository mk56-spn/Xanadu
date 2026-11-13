using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Playfields;

namespace Xanadu.Editor;

/// <summary>Present on the editor while a selection is being rotated, so the handle can be drawn.</summary>
public struct EditorSelectionRotation(Vector2 pivot, Vector2 arm) : IComponent
{
    /// <summary>The playfield point everything turns around, fixed for the whole drag.</summary>
    public Vector2 Pivot = pivot;

    /// <summary>Where the handle sat from the pivot when the drag began, in screen pixels.</summary>
    public Vector2 Arm = arm;

    /// <summary>Radians turned so far, clockwise on screen.</summary>
    public float Angle;
}

/// <summary>An item as it was when a rotation began; every frame rotates from here, so errors never build up.</summary>
public readonly record struct RotationStart(
    Entity Entity,
    Vector2 Center,
    Vector2 Size,
    Vector2 Facing,
    Vector2 Momentum,
    float Degrees = 0f,
    BlockOneWay OneWay = BlockOneWay.Off,
    Xanadu.Items.Xif? Shape = null,
    BlockSlope Slope = BlockSlope.Off);

/// <summary>
/// Turns a group of playfield items around their shared centre. Anything with
/// a shape turns freely, to any angle: spikes, text, grass and polygons. The
/// exception is the block, a physics box that has to stay square to the field:
/// it only turns in 90 degree steps, swapping width and height. Notes and start
/// points only have a position, so they orbit; start points turn their facing.
/// </summary>
public static class SelectionRotation
{
    /// <summary>How far above the selection the handle sits, in screen pixels.</summary>
    public const float HandleGap = 28f;

    public const float HandleRadius = 7f;

    /// <summary>How far from the handle's centre a click still grabs it.</summary>
    public const float HandleHitRadius = 12f;

    /// <summary>Shift holds the angle to multiples of this.</summary>
    public static readonly float Step = Mathf.DegToRad(15f);

    /// <summary>
    /// The box around every item in the selection. False unless there is something
    /// to turn: two items, or one block or spike turning on the spot. One note
    /// only has a position, so it has nothing to turn around.
    /// </summary>
    public static bool TryGetBounds(IReadOnlyList<Entity> selection, out Rect2 bounds)
    {
        bounds = default;
        int count = 0;
        bool turnsOnTheSpot = false;
        foreach (Entity entity in selection)
        {
            if (!TryGetBox(entity, out Rect2 item))
                continue;

            bounds = count == 0 ? item : bounds.Merge(item);
            count++;
            turnsOnTheSpot = TurnsOnTheSpot(entity);
        }

        return count >= 2 || (count == 1 && turnsOnTheSpot);
    }

    /// <summary>The box around every item in the selection, even a single one. False when it holds none.</summary>
    public static bool TryGetItemBounds(IReadOnlyList<Entity> selection, out Rect2 bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Entity entity in selection)
        {
            if (!TryGetBox(entity, out Rect2 item))
                continue;

            bounds = any ? bounds.Merge(item) : item;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// The idle handle's screen point: centred just above the selection, or just
    /// below it when above would land under the header and out of reach.
    /// </summary>
    public static Vector2 HandlePoint(Rect2 bounds, PlayfieldView view)
    {
        float x = bounds.GetCenter().X;
        Vector2 above = view.PlayfieldToScreen(new Vector2(x, bounds.Position.Y)) + new Vector2(0, -HandleGap);
        if (above.Y - HandleHitRadius >= EditorViewport.Area(view.ScreenSize).Position.Y)
            return above;

        return view.PlayfieldToScreen(new Vector2(x, bounds.End.Y)) + new Vector2(0, HandleGap);
    }

    /// <summary>The angle between two pointer positions around the pivot, optionally stepped.</summary>
    public static float AngleBetween(Vector2 pivot, Vector2 from, Vector2 to, bool stepped)
    {
        float angle = Mathf.Wrap((to - pivot).Angle() - (from - pivot).Angle(), -Mathf.Pi, Mathf.Pi);
        return stepped ? Mathf.Snapped(angle, Step) : angle;
    }

    /// <summary>Items with a shape of their own, which a single one of turns on the spot.</summary>
    public static bool TurnsOnTheSpot(Entity entity) => ItemKinds.Of(entity)?.TurnsOnTheSpot == true;

    /// <summary>The upright box an item covers as it is turned; a point for notes and start points.</summary>
    public static bool TryGetBox(Entity entity, out Rect2 box)
    {
        ItemKind? kind = EditorItems.IsItem(entity) ? ItemKinds.Of(entity) : null;
        box = kind?.Hull(entity) ?? default;
        return kind is not null;
    }

    public static RotationStart Capture(Entity entity)
    {
        ItemKind? kind = ItemKinds.Of(entity);
        Vector2 center = kind?.TurnCenter(entity) ?? default;
        Vector2 facing = default;
        Vector2 momentum = default;
        if (entity.HasComponent<StartPoint>())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            facing = start.Direction;
            momentum = new Vector2(start.Momentum.X, start.Momentum.Y);
        }

        float degrees = kind?.Degrees(entity) ?? 0f;
        Xanadu.Items.Xif? shape = entity.TryGetComponent(out LevelPolygon shaped) && shaped.Item is not null
            ? Xanadu.Items.XifCopy.Clone(shaped.Item)
            : null;
        BlockOneWay oneWay = entity.HasComponent<Block>()
            ? entity.GetComponent<Block>().OneWay
            : BlockOneWay.Off;
        BlockSlope slope = entity.HasComponent<Block>()
            ? entity.GetComponent<Block>().Slope
            : BlockSlope.Off;
        return new RotationStart(entity, center, kind?.Size(entity) ?? Vector2.Zero, facing, momentum, degrees, oneWay, shape, slope);
    }

    /// <summary>Puts one item where the rotation takes it, kept inside the field.</summary>
    public static void Apply(
        in RotationStart start,
        Vector2 pivot,
        float angle,
        Playfield playfield,
        bool snap)
    {
        Entity entity = start.Entity;
        Vector2 center = pivot + (start.Center - pivot).Rotated(angle);

        if (entity.HasComponent<Block>())
        {
            Vector2 size = TurnedSize(start.Size, angle);
            ref Block block = ref entity.GetComponent<Block>();
            block.Size = size;
            block.Position = GridPlacement.Settle(center - size / 2, size, playfield, snap);
            block.OneWay = TurnedOneWay(start.OneWay, QuartersIn(angle));
            return;
        }

        if (entity.HasComponent<Spike>())
        {
            ref Spike spike = ref entity.GetComponent<Spike>();
            spike.RotationDegrees = SpikeRules.NormalizeDegrees(start.Degrees + Mathf.RadToDeg(angle));
            spike.Position = GridPlacement.Settle(center - start.Size / 2, start.Size, playfield, snap);
            return;
        }

        if (TryTurnFreely(start, InField(center, playfield), TurnedBox.Normalize(start.Degrees + Mathf.RadToDeg(angle))))
            return;

        EditorItemPosition.Set(entity, new Vector2(
            Mathf.Clamp(center.X, 0, playfield.Width),
            Mathf.Clamp(center.Y, 0, playfield.Height)));

        if (entity.HasComponent<StartPoint>())
        {
            ref StartPoint point = ref entity.GetComponent<StartPoint>();
            point.Direction = start.Facing.Rotated(angle);
            Vector2 momentum = start.Momentum.Rotated(angle);
            point.Momentum = new System.Numerics.Vector2(momentum.X, momentum.Y);
        }
    }

    /// <summary>
    /// Mirrors one item across the pivot, kept inside the field. Horizontal swaps
    /// left and right. Grid items stay on the grid; directions mirror with them.
    /// </summary>
    public static void ApplyFlip(
        in RotationStart start,
        Vector2 pivot,
        bool horizontal,
        Playfield playfield,
        bool snap)
    {
        Entity entity = start.Entity;
        Vector2 center = horizontal
            ? new Vector2(pivot.X * 2 - start.Center.X, start.Center.Y)
            : new Vector2(start.Center.X, pivot.Y * 2 - start.Center.Y);

        if (entity.HasComponent<Block>())
        {
            ref Block block = ref entity.GetComponent<Block>();
            block.Position = GridPlacement.Settle(center - start.Size / 2, start.Size, playfield, snap);
            block.OneWay = FlippedOneWay(start.OneWay, horizontal);
            // A ramp mirrored side to side rises the other way; upside down it stays a floor ramp.
            block.Slope = horizontal && start.Slope != BlockSlope.Off
                ? (start.Slope == BlockSlope.UpRight ? BlockSlope.UpLeft : BlockSlope.UpRight)
                : start.Slope;
            return;
        }

        if (entity.HasComponent<Spike>())
        {
            ref Spike spike = ref entity.GetComponent<Spike>();
            spike.RotationDegrees = FlippedSpikeDegrees(start.Degrees, horizontal);
            spike.Position = GridPlacement.Settle(center - start.Size / 2, start.Size, playfield, snap);
            return;
        }

        // A polygon mirrors its outline about its origin and turns the other way, which is an exact mirror.
        if (entity.HasComponent<LevelPolygon>() && start.Shape is { } shape)
        {
            ref LevelPolygon polygon = ref entity.GetComponent<LevelPolygon>();
            Xanadu.Items.Xif mirrored = Xanadu.Items.XifCopy.Clone(shape);
            Vector2 axis = horizontal ? new Vector2(-1, 1) : new Vector2(1, -1);
            foreach (Xanadu.Items.XifPrimitive primitive in mirrored.Primitives)
            {
                for (int i = 0; i < primitive.Points.Count; i++)
                    primitive.Points[i] *= axis;
            }

            polygon.Item = mirrored;
            polygon.RotationDegrees = TurnedBox.Normalize(-start.Degrees);
            polygon.Position = InField(center, playfield);
            return;
        }

        if (TryTurnFreely(start, InField(center, playfield), TurnedBox.Flipped(start.Degrees, horizontal)))
            return;

        EditorItemPosition.Set(entity, new Vector2(
            Mathf.Clamp(center.X, 0, playfield.Width),
            Mathf.Clamp(center.Y, 0, playfield.Height)));

        if (entity.HasComponent<StartPoint>())
        {
            ref StartPoint point = ref entity.GetComponent<StartPoint>();
            Vector2 axis = horizontal ? new Vector2(-1, 1) : new Vector2(1, -1);
            point.Direction = start.Facing * axis;
            Vector2 momentum = start.Momentum * axis;
            point.Momentum = new System.Numerics.Vector2(momentum.X, momentum.Y);
        }
    }

    /// <summary>
    /// Puts a freely turning item, text, grass or a polygon, with its centre at
    /// <paramref name="center"/> and turned to <paramref name="degrees"/>. False for any other item.
    /// </summary>
    private static bool TryTurnFreely(in RotationStart start, Vector2 center, float degrees) =>
        ItemKinds.Of(start.Entity)?.TryTurnFreely(start.Entity, center, degrees) == true;

    private static Vector2 InField(Vector2 point, Playfield playfield) => new(
        Mathf.Clamp(point.X, 0, playfield.Width),
        Mathf.Clamp(point.Y, 0, playfield.Height));

    /// <summary>A block's one-way side turns with it, a quarter at a time.</summary>
    public static BlockOneWay TurnedOneWay(BlockOneWay side, int quarterTurns) =>
        side == BlockOneWay.Off
            ? BlockOneWay.Off
            : (BlockOneWay)(Mathf.PosMod((int)side - 1 + quarterTurns, 4) + 1);

    /// <summary>The side a one-way block collides on after mirroring: along the mirror's axis it swaps.</summary>
    public static BlockOneWay FlippedOneWay(BlockOneWay side, bool horizontal) =>
        side == BlockOneWay.Off
            ? BlockOneWay.Off
            : (((int)side - 1) % 2 == 1) == horizontal
                ? (BlockOneWay)(Mathf.PosMod((int)side + 1, 4) + 1)
                : side;

    /// <summary>The way a spike points after mirroring.</summary>
    public static float FlippedSpikeDegrees(float degrees, bool horizontal) =>
        SpikeRules.NormalizeDegrees(horizontal ? -degrees : 180f - degrees);

    /// <summary>Width and height swap once the nearest quarter turn is an odd one.</summary>
    public static Vector2 TurnedSize(Vector2 size, float angle)
    {
        return QuartersIn(angle) % 2 == 0 ? size : new Vector2(size.Y, size.X);
    }

    private static int QuartersIn(float angle) => (int)MathF.Round(angle / (Mathf.Tau / 4));
}
