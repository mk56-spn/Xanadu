using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Logic.Playfields;

/// <summary>Advances and removes short-lived hit pops.</summary>
public sealed class NotePopSystem : QuerySystem<NotePop>
{
    protected override void OnUpdate() => Query.EachEntity(new AgePops(CommandBuffer, Tick.deltaTime));

    private readonly record struct AgePops(CommandBuffer Buffer, float Delta) : IEachEntity<NotePop>
    {
        public void Execute(ref NotePop pop, int id)
        {
            pop.Age += Delta;
            if (pop.Age >= pop.Lifetime)
                Buffer.DeleteEntity(id);
        }
    }
}
