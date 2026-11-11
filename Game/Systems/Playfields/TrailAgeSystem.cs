using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Ages one kind of short-lived trail and deletes it once its life is spent.</summary>
public sealed class TrailAgeSystem<T> : QuerySystem<T> where T : struct, IComponent, IAgingTrail
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref T trail, Entity entity) =>
        {
            trail.Age += Tick.deltaTime;
            if (trail.Age >= trail.Lifetime)
                buffer.DeleteEntity(entity.Id);
        });
    }
}
