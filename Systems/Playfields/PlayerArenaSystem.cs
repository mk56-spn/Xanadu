using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>Wraps the player laterally and caps the arena top and bottom.</summary>
public sealed class PlayerArenaSystem : QuerySystem<Player, PhysicsBody, Position, PlayfieldView, Playfield>
{
    /// <summary>
    /// How much the floor grips the player. The floor is a limit rather than a
    /// Box2D body, so it has to apply its own friction; this is what the player's
    /// 0.15 and a block's 0.5 mix to, so both grounds feel alike.
    /// </summary>
    public const float GroundFriction = 0.27f;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref player, ref body, ref position, ref view, ref playfield, entity) =>
        {
            Vector2 fieldTopLeft = view.ScreenOrigin;
            Vector2 fieldSize = new(
                playfield.Width * view.Scale,
                playfield.Height * view.Scale);

            Vector2 half = player.Size / 2;
            NVector2 physicsPosition = body.Body.GetPosition();
            Vector2 screen = Box2DPhysicsWorld.ToScreen(physicsPosition);

            if (screen.X < fieldTopLeft.X)
                screen.X += fieldSize.X;
            else if (screen.X > fieldTopLeft.X + fieldSize.X)
                screen.X -= fieldSize.X;

            screen.Y = Math.Clamp(
                screen.Y,
                fieldTopLeft.Y + half.Y,
                fieldTopLeft.Y + fieldSize.Y - half.Y);

            body.Body.SetTransform(
                Box2DPhysicsWorld.ToPhysics(screen),
                body.Body.GetAngle());

            // Only motion into a limit stops there, so a launch can leave it.
            NVector2 velocity = body.Body.LinearVelocity;
            bool onFloor = screen.Y >= fieldTopLeft.Y + fieldSize.Y - half.Y - 0.5f &&
                           velocity.Y >= 0f;
            bool onCeiling = screen.Y <= fieldTopLeft.Y + half.Y + 0.5f &&
                             velocity.Y <= 0f;
            if (onFloor || onCeiling)
            {
                body.Body.SetLinearVelocity(new NVector2(
                    onFloor ? AfterFriction(velocity.X, delta) : velocity.X,
                    0f));
            }

            position.Value = screen;
            buffer.AddTag<Moved>(entity.Id);
        });
    }

    /// <summary>Sideways speed, in metres per second, after sliding on the floor; it stops rather than reverses.</summary>
    public static float AfterFriction(float speed, float delta)
    {
        float loss = GroundFriction * Box2DPhysicsWorld.DefaultGravity * delta;
        return Math.Abs(speed) <= loss ? 0f : speed - Math.Sign(speed) * loss;
    }
}
