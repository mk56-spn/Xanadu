using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Physics;

/// <summary>
/// Fits a rigged player's body to her legs after each physics frame. In the air
/// the body's bottom rises as far as the art lifts her feet; on the ground it is
/// the full box. Falling onto a block lands as soon as the full box's bottom
/// reaches the block's top, so she touches down feet first rather than dropping
/// until the raised bottom meets it.
/// </summary>
public sealed class PlayerTuckSystem : QuerySystem<Player, PhysicsBody, PlayerTuck, Position>
{
    /// <summary>Pixels the lift must change by before the body is reshaped.</summary>
    public const float ReshapeSlack = 0.5f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref player, ref body, ref tuck, ref position, entity) =>
        {
            if (!entity.TryGetComponent(out PlayfieldView view) ||
                !entity.TryGetComponent(out Playfield playfield))
            {
                return;
            }

            Vector2 centre = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
            if (tuck.Raised > 0f &&
                body.Body.LinearVelocity.Y >= 0f &&
                PlayerGround.TryLanding(entity, view, playfield, centre, tuck.LastFeet, tuck.Raised, out float standingY))
            {
                centre.Y = standingY;
                body.Body.SetTransform(Box2DPhysicsWorld.ToPhysics(centre), body.Body.GetAngle());
                body.Body.SetLinearVelocity(new Vector2(body.Body.LinearVelocity.X, 0f));
                position.Value = centre;
                buffer.AddTag<Moved>(entity.Id);
            }

            float wanted = Airborne(entity) && !PlayerGround.TryStanding(entity, view, playfield, centre, out _)
                ? Math.Clamp(tuck.Lift, 0f, player.Size.Y / 2)
                : 0f;
            if (Math.Abs(wanted - tuck.Raised) > ReshapeSlack || (wanted == 0f && tuck.Raised != 0f))
            {
                Box2DPhysicsWorld.RaiseBottom(body.Body, player.Size, wanted);
                tuck.Raised = wanted;
            }

            tuck.LastFeet = centre.Y + player.Size.Y / 2;
        });
    }

    private static bool Airborne(Entity entity) =>
        entity.TryGetComponent(out CharacterMotion motion) &&
        motion.State is CharacterMotionState.Jumping or CharacterMotionState.Falling;
}
