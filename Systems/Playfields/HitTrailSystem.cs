using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Advances and removes short-lived note key trails.</summary>
public sealed class HitTrailSystem : QuerySystem<HitTrail>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref trail, entity) =>
        {
            trail.Age += Tick.deltaTime;
            if (trail.Age >= trail.Lifetime)
                buffer.DeleteEntity(entity.Id);
        });
    }
}
