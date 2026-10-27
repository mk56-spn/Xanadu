using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Particles;

/// <summary>Plays each requested <see cref="ParticleBurst"/> once, then removes the request.</summary>
public sealed class ParticleBurstSystem(IParticleEmitter emitter) : QuerySystem<ParticleBurst>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref burst, entity) =>
        {
            emitter.Burst(burst.Position, burst.Kind);
            buffer.DeleteEntity(entity.Id);
        });
    }
}
