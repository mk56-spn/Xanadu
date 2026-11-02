using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
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
    DashDirection Direction,
    Vector2 Facing,
    Vector2 Momentum,
    int SpikeTurns = 0);

/// <summary>
/// Turns a group of playfield items around their shared centre. Blocks and
/// spike tiles sit on the field's axes, so their centres orbit and they stay
/// square to it, swapping width and height at each odd quarter turn. A spike's
/// tip follows to the nearest quarter turn.
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

    private const int DirectionCount = 8;

    /// <summary>
    /// The box around every item in the selection. False unless at least two
    /// items are in it, since one item has nothing to turn around.
    /// </summary>
    public static bool TryGetBounds(IReadOnlyList<Entity> selection, out Rect2 bounds)
    {
        bounds = default;
        int count = 0;
        foreach (Entity entity in selection)
        {
            if (!EditorItems.IsItem(entity) ||
                !EditorItemPosition.TryGet(entity, out Vector2 center))
            {
                continue;
            }

            Rect2 item = new(center - SizeOf(entity) / 2, SizeOf(entity));
            bounds = count == 0 ? item : bounds.Merge(item);
            count++;
        }

        return count >= 2;
    }

    /// <summary>The box around every item in the selection, even a single one. False when it holds none.</summary>
    public static bool TryGetItemBounds(IReadOnlyList<Entity> selection, out Rect2 bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Entity entity in selection)
        {
            if (!EditorItems.IsItem(entity) ||
                !EditorItemPosition.TryGet(entity, out Vector2 center))
            {
                continue;
            }

            Rect2 item = new(center - SizeOf(entity) / 2, SizeOf(entity));
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

    public static RotationStart Capture(Entity entity)
    {
        EditorItemPosition.TryGet(entity, out Vector2 center);
        DashDirection direction = entity.HasComponent<Note>()
            ? entity.GetComponent<Note>().DashDirection
            : default;
        Vector2 facing = default;
        Vector2 momentum = default;
        if (entity.HasComponent<StartPoint>())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            facing = start.Direction;
            momentum = new Vector2(start.Momentum.X, start.Momentum.Y);
        }

        int spikeTurns = entity.HasComponent<Spike>()
            ? entity.GetComponent<Spike>().QuarterTurns
            : 0;
        return new RotationStart(entity, center, SizeOf(entity), direction, facing, momentum, spikeTurns);
    }

    /// <summary>Puts one item where the rotation takes it, kept inside the field.</summary>
    public static void Apply(
        in RotationStart start,
        Vector2 pivot,
        float angle,
        Playfield playfield,
        bool snapBlocks)
    {
        Entity entity = start.Entity;
        Vector2 center = pivot + (start.Center - pivot).Rotated(angle);

        if (entity.HasComponent<Block>())
        {
            Vector2 size = TurnedSize(start.Size, angle);
            Vector2 position = center - size / 2;
            if (snapBlocks)
                position = BlockGrid.Snap(position, BlockGrid.Cell(playfield));

            ref Block block = ref entity.GetComponent<Block>();
            block.Size = size;
            block.Position = BlockGrid.Clamp(position, size, playfield);
            return;
        }

        if (entity.HasComponent<Spike>())
        {
            Vector2 size = TurnedSize(start.Size, angle);
            ref Spike spike = ref entity.GetComponent<Spike>();
            spike.Size = size;
            spike.QuarterTurns = SpikeRules.NormalizeTurns(start.SpikeTurns + QuartersIn(angle));
            spike.Position = SpikeRules.Clamp(
                PlayfieldGrid.Snap(center - size / 2, playfield),
                size,
                playfield);
            return;
        }

        EditorItemPosition.Set(entity, new Vector2(
            Mathf.Clamp(center.X, 0, playfield.Width),
            Mathf.Clamp(center.Y, 0, playfield.Height)));

        if (entity.HasComponent<Note>())
        {
            ref Note note = ref entity.GetComponent<Note>();
            if (note.Kind is NoteKind.Dash or NoteKind.Momentum)
                note.DashDirection = TurnedDirection(start.Direction, angle);
        }
        else if (entity.HasComponent<StartPoint>())
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
        bool snapBlocks)
    {
        Entity entity = start.Entity;
        Vector2 center = horizontal
            ? new Vector2(pivot.X * 2 - start.Center.X, start.Center.Y)
            : new Vector2(start.Center.X, pivot.Y * 2 - start.Center.Y);

        if (entity.HasComponent<Block>())
        {
            Vector2 position = center - start.Size / 2;
            if (snapBlocks)
                position = BlockGrid.Snap(position, BlockGrid.Cell(playfield));
            entity.GetComponent<Block>().Position = BlockGrid.Clamp(position, start.Size, playfield);
            return;
        }

        if (entity.HasComponent<Spike>())
        {
            ref Spike spike = ref entity.GetComponent<Spike>();
            spike.QuarterTurns = FlippedSpikeTurns(start.SpikeTurns, horizontal);
            spike.Position = SpikeRules.Clamp(
                PlayfieldGrid.Snap(center - start.Size / 2, playfield),
                start.Size,
                playfield);
            return;
        }

        EditorItemPosition.Set(entity, new Vector2(
            Mathf.Clamp(center.X, 0, playfield.Width),
            Mathf.Clamp(center.Y, 0, playfield.Height)));

        if (entity.HasComponent<Note>())
        {
            ref Note note = ref entity.GetComponent<Note>();
            if (note.Kind is NoteKind.Dash or NoteKind.Momentum)
                note.DashDirection = FlippedDirection(start.Direction, horizontal);
        }
        else if (entity.HasComponent<StartPoint>())
        {
            ref StartPoint point = ref entity.GetComponent<StartPoint>();
            Vector2 axis = horizontal ? new Vector2(-1, 1) : new Vector2(1, -1);
            point.Direction = start.Facing * axis;
            Vector2 momentum = start.Momentum * axis;
            point.Momentum = new System.Numerics.Vector2(momentum.X, momentum.Y);
        }
    }

    /// <summary>The eight directions run clockwise from Up, so mirroring reflects the index.</summary>
    public static DashDirection FlippedDirection(DashDirection direction, bool horizontal) =>
        (DashDirection)Mathf.PosMod((horizontal ? 0 : DirectionCount / 2) - (int)direction, DirectionCount);

    /// <summary>A spike pointing along the mirror's axis turns round; one across it stays.</summary>
    public static int FlippedSpikeTurns(int quarterTurns, bool horizontal)
    {
        int turns = SpikeRules.NormalizeTurns(quarterTurns);
        bool pointsSideways = turns % 2 == 1;
        return pointsSideways == horizontal ? SpikeRules.NormalizeTurns(turns + 2) : turns;
    }

    /// <summary>The nearest of the eight dash directions after turning; they run clockwise from Up.</summary>
    public static DashDirection TurnedDirection(DashDirection direction, float angle)
    {
        int steps = (int)MathF.Round(angle / (Mathf.Tau / DirectionCount));
        return (DashDirection)Mathf.PosMod((int)direction + steps, DirectionCount);
    }

    /// <summary>Width and height swap once the nearest quarter turn is an odd one.</summary>
    public static Vector2 TurnedSize(Vector2 size, float angle)
    {
        return QuartersIn(angle) % 2 == 0 ? size : new Vector2(size.Y, size.X);
    }

    private static int QuartersIn(float angle) => (int)MathF.Round(angle / (Mathf.Tau / 4));

    private static Vector2 SizeOf(Entity entity)
    {
        if (entity.HasComponent<Block>())
            return entity.GetComponent<Block>().Size;

        return entity.HasComponent<Spike>()
            ? entity.GetComponent<Spike>().Size
            : Vector2.Zero;
    }
}
