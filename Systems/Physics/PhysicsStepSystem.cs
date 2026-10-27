using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Physics;

/// <summary>Steps Box2D and syncs character transforms and momentum into ECS.</summary>
public sealed class PhysicsStepSystem : QuerySystem<Character, PhysicsBody, Momentum>
{
    private readonly Box2DPhysicsWorld _physics;

    public PhysicsStepSystem(Box2DPhysicsWorld physics)
    {
        _physics = physics;
    }

    protected override void OnUpdate()
    {
        _physics.Step(Tick.deltaTime);
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref _, ref body, ref momentum, entity) =>
        {
            ref Position position = ref entity.GetComponent<Position>();
            position.Value = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            momentum.Value = body.Body.LinearVelocity * body.Body.Mass;
            buffer.AddTag<Moved>(entity.Id);
        });
    }
}
