using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Advances a hit dash note's fading connector and exit, and removes them once the exit is gone.</summary>
public sealed class DashLinkFadeSystem : QuerySystem<DashLinkFade>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref fade, entity) =>
        {
            fade.Age += Tick.deltaTime;
            if (fade.Age >= DashLinkFade.ExitLifetime)
                buffer.DeleteEntity(entity.Id);
        });
    }
}
