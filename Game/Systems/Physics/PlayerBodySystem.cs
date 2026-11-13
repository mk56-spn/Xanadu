using Box2DSharp.Dynamics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Physics;

/// <summary>Creates a Box2D body for player entities that do not have one yet.</summary>
public sealed class PlayerBodySystem : QuerySystem<Player, Position>
{
    private const float CharacterMass = 1f;
    /// <summary>The player's own friction while <see cref="PlayerFriction"/> is on; it is 0 otherwise.</summary>
    public const float CharacterFriction = 0.15f;

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

            bool playing = entity.TryGetComponent<PlaySession>(out PlaySession session);
            if (playing && !PlayerDropIn.Begun(entity))
                return;

            // A run drops in already falling. A frame can land after the drop's
            // start, so the body starts as far into the fall as the song is.
            float late = playing ? PlayerDropIn.Elapsed(entity) : 0f;
            if (playing)
                position.Value.Y += PlayerDropIn.Fallen(session.DropSpeed, late);

            Body body = _physics.CreateDynamicBox(
                position.Value,
                player.Size,
                CharacterMass,
                PlayerFriction.IsOn(entity) ? CharacterFriction : 0f);

            if (playing)
                body.SetLinearVelocity(new Vector2(0f, session.DropSpeed + Box2DPhysicsWorld.DefaultGravity * late));

            buffer.AddComponent(entity.Id, new PhysicsBody(body));
            buffer.AddComponent(entity.Id, new PlayerSmoothing(position.Value));
        });
    }
}
