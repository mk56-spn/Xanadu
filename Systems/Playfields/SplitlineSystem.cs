using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Advances Splitline's two channel timers. When both sides are absorbed the
/// character is off the field and invulnerable; when one ends it returns to the
/// position it left.
/// </summary>
public sealed class SplitlineSystem : QuerySystem<SplitlineState, Playhead, Position>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref state, ref playhead, ref position, entity) =>
        {
            long judgedTime = playhead.TimeMs;

            if (state.LeftActive && judgedTime >= state.LeftEndMs)
                state.LeftActive = false;
            if (state.RightActive && judgedTime >= state.RightEndMs)
                state.RightActive = false;

            bool both = state.LeftActive && state.RightActive;
            bool hasBody = entity.TryGetComponent(out PhysicsBody body);

            if (both && !state.Absent)
            {
                state.Absent = true;
                state.ReturnPosition = position.Value;
                buffer.AddTag<SplitlineAbsent>(entity.Id);
            }
            else if (!both && state.Absent)
            {
                state.Absent = false;
                position.Value = state.ReturnPosition;
                buffer.RemoveTag<SplitlineAbsent>(entity.Id);
                if (hasBody)
                {
                    body.Body.SetTransform(
                        Box2DPhysicsWorld.ToPhysics(state.ReturnPosition),
                        body.Body.GetAngle());
                    body.Body.SetLinearVelocity(NVector2.Zero);
                }
            }

            if (!state.Absent)
                return;

            position.Value = state.ReturnPosition;
            if (hasBody)
            {
                body.Body.SetTransform(
                    Box2DPhysicsWorld.ToPhysics(state.ReturnPosition),
                    body.Body.GetAngle());
                body.Body.SetLinearVelocity(NVector2.Zero);
            }
        });
    }
}
