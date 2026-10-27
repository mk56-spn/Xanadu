using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Physics;

/// <summary>Creates a Box2D body for player entities that do not have one yet.</summary>
public sealed class PlayerBodySystem : QuerySystem<Player, Position>
{
    private const float CharacterMass = 1f;
    private const float CharacterFriction = 0.15f;

    private readonly Box2DPhysicsWorld _physics;

    public PlayerBodySystem(Box2DPhysicsWorld physics)
    {
        _physics = physics;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref player, ref position, entity) =>
        {
            if (entity.HasComponent<PhysicsBody>())
                return;

            if (entity.TryGetComponent<PlaySession>(out PlaySession session) &&
                !session.Started)
            {
                return;
            }

            Body body = _physics.CreateDynamicBox(
                position.Value,
                player.Size,
                CharacterMass,
                CharacterFriction);

            buffer.AddComponent(entity.Id, new PhysicsBody(body));
        });
    }
}
