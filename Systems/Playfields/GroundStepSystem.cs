using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>A step under way after a ground hit, from <see cref="GroundStepSystem"/>.</summary>
public struct GroundStep(long noteTimeMs, long startMs, float target) : IComponent
{
    /// <summary>The time of the notes this step belongs to; notes hit at the same time share it.</summary>
    public long NoteTimeMs = noteTimeMs;

    /// <summary>The playhead time the step starts at: the note's moment, not the press.</summary>
    public long StartMs = startMs;

    /// <summary>How far sideways the step goes, in screen pixels; negative is left, zero goes nowhere.</summary>
    public float Target = target;

    /// <summary>How far it has moved the player so far.</summary>
    public float Moved;
}

/// <summary>
/// Hitting a note while standing on the floor or a block carries the player a
/// short step towards the side the note is on, easing out to a stop. Notes hit
/// at one timestamp share a single step: more on the same side add nothing,
/// and notes on both sides cancel it. Any movement note ends the step.
/// </summary>
public sealed class GroundStepSystem : QuerySystem<GroundStep, PhysicsBody, Position, Playhead>
{
    /// <summary>How far one step goes, in playfield units.</summary>
    public const float Distance = 80f;

    /// <summary>How long a step takes.</summary>
    public const float DurationMs = 70f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref step, ref body, ref position, ref playhead, entity) =>
        {
            if (entity.Tags.Has<PlayPaused>())
                return;

            float t = Math.Clamp((playhead.TimeMs - step.StartMs) / DurationMs, 0f, 1f);
            // Quartic ease-out: almost all of the step lands in the first few frames.
            float rest = 1f - t;
            float eased = 1f - rest * rest * rest * rest;
            float wanted = step.Target * eased;
            float delta = wanted - step.Moved;
            step.Moved = wanted;

            NVector2 at = body.Body.GetPosition();
            body.Body.SetTransform(
                new NVector2(at.X + delta / Box2DPhysicsWorld.PixelsPerMeter, at.Y),
                body.Body.GetAngle());
            body.Body.SetLinearVelocity(new NVector2(0f, body.Body.LinearVelocity.Y));
            position.Value = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());

            if (t >= 1f && step.Moved == step.Target)
                buffer.RemoveComponent<GroundStep>(entity.Id);
        });
    }

    /// <summary>
    /// Starts the step for a note just hit, or folds the hit into the step its
    /// timestamp already started. Only a player standing on something steps.
    /// </summary>
    public static void OnHit(Entity player, long noteTimeMs, long startMs, float noteScreenX, float playerScreenX)
    {
        float dx = noteScreenX - playerScreenX;
        if (Math.Abs(dx) < 1f ||
            !player.TryGetComponent(out PlayfieldView view) ||
            !player.TryGetComponent(out Playfield playfield) ||
            !player.TryGetComponent(out PhysicsBody body) ||
            body.Body.LinearVelocity.Y < 0f)
        {
            return;
        }

        float side = Math.Sign(dx) * Distance * view.Scale;
        if (player.TryGetComponent(out GroundStep current) && current.NoteTimeMs == noteTimeMs)
        {
            if (current.Target != 0f && Math.Sign(current.Target) != Math.Sign(side))
                player.GetComponent<GroundStep>().Target = 0f;
            return;
        }

        Godot.Vector2 centre = Box2DPhysicsWorld.ToScreen(body.Body.GetPosition());
        if (!PlayerGround.TryStanding(player, view, playfield, centre, out _))
            return;

        if (player.HasComponent<GroundStep>())
            player.GetComponent<GroundStep>() = new GroundStep(noteTimeMs, startMs, side);
        else
            player.AddComponent(new GroundStep(noteTimeMs, startMs, side));
    }

    /// <summary>Stops a step where it is, as a dash or momentum note does.</summary>
    public static void Cancel(Entity player, CommandBuffer buffer)
    {
        if (player.HasComponent<GroundStep>())
            buffer.RemoveComponent<GroundStep>(player.Id);
    }
}
