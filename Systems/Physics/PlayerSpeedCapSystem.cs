using System;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using NVector2 = System.Numerics.Vector2;

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
            NVector2 velocity = body.Body.LinearVelocity;
            NVector2 capped = Capped(velocity);
            if (capped != velocity)
                body.Body.SetLinearVelocity(capped);
        });
    }

    public static NVector2 Capped(NVector2 velocity)
    {
        float max = Box2DPhysicsWorld.MaxSpeed;
        return new NVector2(
            Math.Clamp(velocity.X, -max, max),
            Math.Min(velocity.Y, max));
    }
}
