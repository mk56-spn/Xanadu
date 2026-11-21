using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Particles;

/// <summary>Plays each requested <see cref="ParticleBurst"/> once, then removes the request.</summary>
public sealed class ParticleBurstSystem(IParticleEmitter emitter) : QuerySystem<ParticleBurst>
{
    private bool _prepared;

    protected override void OnUpdate()
    {
        // On the playfield's first frame, well before any note.
        if (!_prepared)
        {
            emitter.Prepare();
            _prepared = true;
        }

        Query.EachEntity(new FireBursts(emitter, CommandBuffer));
    }

    private readonly record struct FireBursts(IParticleEmitter Emitter, CommandBuffer Buffer) : IEachEntity<ParticleBurst>
    {
        public void Execute(ref ParticleBurst burst, int id)
        {
            Emitter.Burst(burst.Particle, burst.Position, burst.Tint, burst.Area, burst.Angle, burst.Heading);
            Buffer.DeleteEntity(id);
        }
    }
}
