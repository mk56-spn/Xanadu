using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Physics;
using NVector2 = System.Numerics.Vector2;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Playfields;

/// <summary>Wraps the player laterally and caps the arena top and bottom.</summary>
public sealed class PlayerArenaSystem : QuerySystem<Player, PhysicsBody, Position, PlayfieldView, Playfield>
{
    protected override void OnUpdate()
    {
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

            if (screen.Y >= fieldTopLeft.Y + fieldSize.Y - half.Y - 0.5f ||
                screen.Y <= fieldTopLeft.Y + half.Y + 0.5f)
            {
                body.Body.SetLinearVelocity(new NVector2(
                    body.Body.LinearVelocity.X,
                    0f));
            }

            position.Value = screen;
            buffer.AddTag<Moved>(entity.Id);
        });
    }
}
