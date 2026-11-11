using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Advances and removes short-lived hit pops.</summary>
public sealed class NotePopSystem : QuerySystem<NotePop>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref pop, entity) =>
        {
            pop.Age += Tick.deltaTime;
            if (pop.Age >= pop.Lifetime)
                buffer.DeleteEntity(entity.Id);
        });
    }
}
