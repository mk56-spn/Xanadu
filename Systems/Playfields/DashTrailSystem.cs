using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;

namespace Writer.Systems.Playfields;

/// <summary>Advances and removes short-lived dash indicators.</summary>
public sealed class DashTrailSystem : QuerySystem<DashTrail>
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
