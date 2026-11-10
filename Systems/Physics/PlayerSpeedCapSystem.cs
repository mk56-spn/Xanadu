using System;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Holds the player to <see cref="Box2DPhysicsWorld.MaxSpeed"/> sideways and
/// falling, so nothing carries it past the strongest momentum note and falls reach a
/// terminal speed. Rising speed is not capped.
/// </summary>
public sealed class PlayerSpeedCapSystem : QuerySystem<Player, PhysicsBody>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref _, ref body, _) =>
        {
            Vector2 velocity = body.Body.LinearVelocity;
            Vector2 capped = Capped(velocity);
            if (capped != velocity)
                body.Body.SetLinearVelocity(capped);
        });
    }

    public static Vector2 Capped(Vector2 velocity)
    {
        float max = Box2DPhysicsWorld.MaxSpeed;
        return new Vector2(
            Math.Clamp(velocity.X, -max, max),
            Math.Min(velocity.Y, max));
    }
}
