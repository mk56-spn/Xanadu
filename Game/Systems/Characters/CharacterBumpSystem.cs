using System;
using Box2DSharp.Collision.Collider;
using Box2DSharp.Dynamics;
using Box2DSharp.Dynamics.Contacts;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ecs;
using Xanadu.Ecs.Components;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Gives a rigged player a little flinch when she bumps into a block on her
/// way through the level: a new touch on a block's side or underside, at
/// speed, jolts her upper body away from it with a springy wobble, squashes her
/// briefly along the hit and puffs dust off the spot. Only the drawing reacts;
/// she never catches on anything. Standing on a block is not a bump: landing
/// has its own clip.
/// </summary>
public sealed class CharacterBumpSystem : QuerySystem<CharacterRig, Player, PhysicsBody>
{
    /// <summary>How long a flinch lasts, in seconds.</summary>
    public const float Seconds = 0.45f;

    /// <summary>Speed into the block, in pixels per second, below which a touch is a graze and shows nothing.</summary>
    public const float MinSpeed = 80f;

    /// <summary>Speed into the block, in pixels per second, that gives the fullest flinch.</summary>
    public const float FullSpeed = 700f;

    /// <summary>How far the whole body squashes along the hit at the fullest, as a share of its size.</summary>
    public const float Squash = 0.1f;

    private const int Left = 1, Right = 2, Above = 4;

    /// <summary>Upper-body bones and how far each turns at the fullest flinch, in radians; the chain whips more towards the ends.</summary>
    private static readonly (string Bone, float Turn)[] Flinch =
    {
        ("spine", 0.12f),
        ("chest", 0.16f),
        ("head", 0.28f),
        ("upper_arm_l", 0.45f),
        ("forearm_l", 0.35f),
        ("upper_arm_r", 0.45f),
        ("forearm_r", 0.35f),
    };

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity entity in Query.ToEntityList())
        {
            if (entity.Tags.Has<PlayPaused>())
                continue;

            if (!entity.HasComponent<CharacterBump>())
                entity.AddComponent(new CharacterBump { Age = Seconds });

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
            bump.LastVelocity = body.LinearVelocity * Box2DPhysicsWorld.PixelsPerMeter;
        }
    }

    /// <summary>The bit for a block on her left, on her right or above her; 0 for the floor or a ramp under her.</summary>
    private static int SideOf(Vector2 normal) =>
        normal.X > 0.5f ? Left :
        normal.X < -0.5f ? Right :
        normal.Y > 0.5f ? Above : 0;

    /// <summary>
    /// How much of the flinch shows <paramref name="age"/> seconds after the bump:
    /// a near-instant jolt, then a spring back that overshoots once and settles.
    /// </summary>
    public static float Envelope(float age)
    {
        if (age < 0f || age >= Seconds)
            return 0f;

        float attack = Math.Min(1f, age / 0.03f);
        float tail = 1f - age / Seconds;
        return attack * MathF.Exp(-7f * age) * MathF.Cos(18f * age) * tail;
    }

    /// <summary>
    /// Turns <paramref name="pose"/>'s upper body away from the bump. Rig
    /// space faces right, where a positive turn tips the top forwards, so the
    /// hit's direction is mirrored for a player facing left.
    /// </summary>
    public static void ApplyFlinch(XafPackage package, XafPose pose, in CharacterBump bump, bool faceLeft)
    {
        float amount = Envelope(bump.Age) * bump.Strength;
        if (amount == 0f)
            return;

        float across = faceLeft ? -bump.Normal.X : bump.Normal.X;
        // Away from a wall, and ducked forwards from a ceiling.
        float lean = across + 0.7f * Math.Max(bump.Normal.Y, 0f);
        foreach ((string bone, float turn) in Flinch)
        {
            int index = package.Skeleton.Bones.FindIndex(entry => entry.Id == bone);
            if (index >= 0 && index < pose.Bones.Length)
                pose.Bones[index].Rotation += turn * lean * amount;
        }
    }

    /// <summary>
    /// The squash the bump puts on the drawn body, about <paramref name="centre"/>
    /// on screen: shorter along the hit, a little wider across it.
    /// </summary>
    public static Transform2D SquashAbout(in CharacterBump bump, Vector2 centre)
    {
        float amount = Envelope(bump.Age) * bump.Strength * Squash;
        if (amount == 0f)
            return Transform2D.Identity;

        float angle = MathF.Atan2(bump.Normal.Y, bump.Normal.X);
        var toCentre = new Transform2D(0f, -centre);
        var turn = new Transform2D(-angle, Vector2.Zero);
        var squash = new Transform2D(0f, new Vector2(1f - amount, 1f + amount * 0.5f), 0f, Vector2.Zero);
        var back = new Transform2D(angle, centre);
        return back * squash * turn * toCentre;
    }
}
