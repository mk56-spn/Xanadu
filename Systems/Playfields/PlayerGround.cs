using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Whether the player stands on something: the arena floor, or the top of a
/// block it can land on. Screen space, the space the player's body works in.
/// </summary>
public static class PlayerGround
{
    /// <summary>How close to the floor the player's centre has to be to count as on it.</summary>
    public const float FloorTolerance = 0.5f;

    /// <summary>Slack for resting on a block, whose contact leaves a small gap.</summary>
    public const float BlockTolerance = 2f;

    /// <summary>
    /// True when a player centred at <paramref name="centre"/> rests on the floor
    /// or a block top; <paramref name="standingY"/> is then the exact centre
    /// height it stands at there.
    /// </summary>
    public static bool TryStanding(
        Entity player,
        PlayfieldView view,
        Playfield playfield,
        Vector2 centre,
        out float standingY)
    {
        Vector2 size = player.GetComponent<Player>().Size;
        float floor = view.ScreenOrigin.Y + playfield.Height * view.Scale - size.Y / 2;
        if (centre.Y >= floor - FloorTolerance)
        {
            standingY = floor;
            return true;
        }

        float feet = centre.Y + size.Y / 2;
        foreach (Entity blockEntity in player.Store.Query<Block>().ToEntityList())
        {
            Block block = blockEntity.GetComponent<Block>();
            if (block.OneWay is not (BlockOneWay.Off or BlockOneWay.Up))
                continue;

            Vector2 topLeft = view.PlayfieldToScreen(GroupOffset.Shown(blockEntity, block.Position));
            Vector2 bottomRight = view.PlayfieldToScreen(GroupOffset.Shown(blockEntity, block.End));
            bool overlapsX = centre.X + size.X / 2 > topLeft.X && centre.X - size.X / 2 < bottomRight.X;
            if (overlapsX && Mathf.Abs(feet - topLeft.Y) <= BlockTolerance)
            {
                standingY = topLeft.Y - size.Y / 2;
                return true;
            }
        }

        standingY = centre.Y;
        return false;
    }

    /// <summary>
    /// True when a falling player whose body's bottom is raised by
    /// <paramref name="raised"/> lands on a block top this frame: the full box's
    /// bottom was above the top at <paramref name="lastFeet"/> and has reached
    /// it, but not gone further than the raised bottom lets it.
    /// <paramref name="standingY"/> is then the centre height it stands at.
    /// </summary>
    public static bool TryLanding(
        Entity player,
        PlayfieldView view,
        Playfield playfield,
        Vector2 centre,
        float lastFeet,
        float raised,
        out float standingY)
    {
        Vector2 size = player.GetComponent<Player>().Size;
        float feet = centre.Y + size.Y / 2;
        foreach (Entity blockEntity in player.Store.Query<Block>().ToEntityList())
        {
            Block block = blockEntity.GetComponent<Block>();
            if (block.OneWay is not (BlockOneWay.Off or BlockOneWay.Up))
                continue;

            Vector2 topLeft = view.PlayfieldToScreen(GroupOffset.Shown(blockEntity, block.Position));
            Vector2 bottomRight = view.PlayfieldToScreen(GroupOffset.Shown(blockEntity, block.End));
            bool overlapsX = centre.X + size.X / 2 > topLeft.X && centre.X - size.X / 2 < bottomRight.X;
            float top = topLeft.Y;
            if (overlapsX &&
                lastFeet <= top + BlockTolerance &&
                feet >= top - BlockTolerance &&
                feet <= top + raised + BlockTolerance)
            {
                standingY = top - size.Y / 2;
                return true;
            }
        }

        standingY = centre.Y;
        return false;
    }
}
