using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Ages and removes the see-through copies a character leaves when it hits a note.</summary>
public sealed class HitGhostSystem : QuerySystem<HitGhost>
{
    /// <summary>
    /// Leaves a copy of <paramref name="player"/> where it was last drawn. A
    /// player that is not on screen leaves nothing.
    /// </summary>
    public static void Spawn(EntityStore entities, Entity player)
    {
        if (!player.TryGetComponent(out Player box) ||
            !player.HasComponent<Position>())
        {
            return;
        }

        entities.CreateEntity(new HitGhost(player, PlayerSmoothing.DrawnPosition(player), box.Size));
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref ghost, entity) =>
        {
            ghost.Age += Tick.deltaTime;
            if (ghost.Age >= ghost.Lifetime || ghost.Player.IsNull)
                buffer.DeleteEntity(entity.Id);
        });
    }
}
