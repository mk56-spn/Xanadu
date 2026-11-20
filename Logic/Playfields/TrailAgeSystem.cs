using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Playfields;

/// <summary>Ages one kind of short-lived trail and deletes it once its life is spent.</summary>
public sealed class TrailAgeSystem<T> : QuerySystem<T> where T : struct, IComponent, IAgingTrail
{
    protected override void OnUpdate()
    {
        Query.EachEntity(new AgeTrails(CommandBuffer, Tick.deltaTime));
    }

    private readonly record struct AgeTrails(CommandBuffer Buffer, float Delta) : IEachEntity<T>
    {
        public void Execute(ref T trail, int id)
        {
            trail.Age += Delta;
            if (trail.Age >= trail.Lifetime)
                Buffer.DeleteEntity(id);
        }
    }
}
