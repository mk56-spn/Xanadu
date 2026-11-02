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
        float delta = Tick.deltaTime;
        bool stepping = _physics.WillStep(delta);
        ArchetypeQuery<PhysicsBody, PlayerSmoothing> smoothed =
            Query.Store.Query<PhysicsBody, PlayerSmoothing>();
        if (stepping)
        {
            smoothed.ForEachEntity((ref body, ref smoothing, _) =>
                smoothing.Previous = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition()));
        }

        _physics.Step(delta);
        float progress = _physics.StepProgress;
        smoothed.ForEachEntity((ref body, ref smoothing, _) =>
        {
            if (stepping)
                smoothing.Stepped = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            smoothing.Alpha = progress;
        });

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
