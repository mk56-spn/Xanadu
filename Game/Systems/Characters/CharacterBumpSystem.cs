using System;
using Box2DSharp.Collision.Collider;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Systems.Physics;
using Xanadu.Model.Physics;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Gives a rigged player a little flinch when she bumps into a block on her
/// way through the level: a new touch on a block's side or underside, at
/// speed, jolts her upper body away from it with a springy wobble, squashes her
/// briefly along the hit and puffs dust off the spot. Only the drawing reacts (CharacterBumpPose);
/// she never catches on anything. Standing on a block is not a bump: landing
/// has its own clip.
/// </summary>
public sealed class CharacterBumpSystem : QuerySystem<CharacterRig, Player, PhysicsBody>
{
    /// <summary>Speed into the block, in pixels per second, below which a touch is a graze and shows nothing.</summary>
    public const float MinSpeed = 80f;

    /// <summary>Speed into the block, in pixels per second, that gives the fullest flinch.</summary>
    public const float FullSpeed = 700f;

    private const int Left = 1, Right = 2, Above = 4;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.Tags.Has<PlayPaused>())
                continue;

            if (!entity.HasComponent<CharacterBump>())
                entity.AddComponent(new CharacterBump { Age = CharacterBump.Seconds });

            ref CharacterBump bump = ref entity.GetComponent<CharacterBump>();
            Body body = entity.GetComponent<PhysicsBody>().Body;
            bump.Age += delta;

            int touching = 0;
            foreach (Contact contact in body.World.ContactManager.ContactList)
            {
                if (!contact.IsTouching || !contact.IsEnabled ||
                    contact.FixtureA.IsSensor || contact.FixtureB.IsSensor)
                {
                    continue;
                }

                bool first = contact.FixtureA.Body == body;
                if (!first && contact.FixtureB.Body != body)
                    continue;

                contact.GetWorldManifold(out WorldManifold manifold);
                // The manifold's normal points from A to B; turn it to point from the block to her.
                Vector2 normal = first ? -manifold.Normal : manifold.Normal;
                int side = SideOf(normal);
                if (side == 0)
                    continue;

                touching |= side;
                if ((bump.Touching & side) != 0)
                    continue;

                float speed = -Vector2.Dot(bump.LastVelocity, normal);
                if (speed < MinSpeed)
                    continue;

                bump.Normal = normal;
                bump.Strength = Math.Clamp(speed / FullSpeed, 0.3f, 1f);
                bump.Age = 0f;
                Vector2 at = Box2DPhysicsWorld.ToScreen(manifold.Points[0]);
                entity.Store.CreateEntity(new ParticleBurst(
                    at, GameParticles.Landing, area: new Vector2(6f, 6f), heading: normal));
            }

            bump.Touching = touching;
            bump.LastVelocity = body.LinearVelocity * PhysicsScale.PixelsPerMeter;
        }
    }

    /// <summary>The bit for a block on her left, on her right or above her; 0 for the floor or a ramp under her.</summary>
    private static int SideOf(Vector2 normal) =>
        normal.X > 0.5f ? Left :
        normal.X < -0.5f ? Right :
        normal.Y > 0.5f ? Above : 0;
}
