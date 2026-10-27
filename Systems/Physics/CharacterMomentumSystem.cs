using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Physics;

/// <summary>Applies queued ECS momentum to character physics bodies.</summary>
public sealed class CharacterMomentumSystem : QuerySystem<Character, PhysicsBody, Momentum>
{
    public CharacterMomentumSystem()
    {
        Filter.AllTags(Tags.Get<ApplyMomentum>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref _, ref body, ref momentum, entity) =>
        {
            body.Body.ApplyLinearImpulseToCenter(momentum.Value, true);
            buffer.RemoveTag<ApplyMomentum>(entity.Id);
        });
    }
}
