using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Works out whether each rigged player is on the ground, jumping or falling
/// and plays the matching clip. The only ground is the arena floor, and dashes
/// teleport, so the state comes from where the player is and how it moved:
/// moving up is a jump, airborne otherwise is a fall, and touching the floor
/// plays the landing clip once before going back to idle. On the ground, moving
/// sideways is a slide, since momentum is the only thing that carries the player
/// along the floor.
/// </summary>
public sealed class CharacterMotionSystem : QuerySystem<CharacterRig, Player, Position>
{
    /// <summary>Sideways speed, in pixels per second, at which a player on the ground starts sliding.</summary>
    public const float SlideStartSpeed = 60f;

    /// <summary>Sideways speed, in pixels per second, below which a slide ends; lower than the start so it cannot flicker.</summary>
    public const float SlideStopSpeed = 30f;

    /// <summary>A dash is instant, so a jump is held this long before it can turn into a fall.</summary>
    public const float JumpHoldSeconds = 0.3f;

    /// <summary>Upward movement in one frame, in pixels, that counts as a jump.</summary>
    public const float RiseThreshold = 1f;

    /// <summary>
    /// Upward speed, in pixels per second, that counts as a jump. Physics runs
    /// faster than frames, so a slow rise moves too little per frame to judge by distance.
    /// </summary>
    public const float RiseSpeed = 60f;

    /// <summary>
    /// Every change of state <see cref="Next"/> can make. Rising always jumps,
    /// leaving the floor falls, touching it lands (or goes straight to the
    /// ground without a landing clip), a landing ends on the ground, and the
    /// ground and a slide turn into each other with sideways speed.
    /// </summary>
    private static readonly (CharacterMotionState From, CharacterMotionState To)[] StateChanges =
    {
        (CharacterMotionState.Grounded, CharacterMotionState.Jumping),
        (CharacterMotionState.Landing, CharacterMotionState.Jumping),
        (CharacterMotionState.Falling, CharacterMotionState.Jumping),
        (CharacterMotionState.Jumping, CharacterMotionState.Falling),
        (CharacterMotionState.Grounded, CharacterMotionState.Falling),
        (CharacterMotionState.Landing, CharacterMotionState.Falling),
        (CharacterMotionState.Jumping, CharacterMotionState.Landing),
        (CharacterMotionState.Falling, CharacterMotionState.Landing),
        (CharacterMotionState.Jumping, CharacterMotionState.Grounded),
        (CharacterMotionState.Falling, CharacterMotionState.Grounded),
        (CharacterMotionState.Landing, CharacterMotionState.Grounded),
        (CharacterMotionState.Sliding, CharacterMotionState.Jumping),
        (CharacterMotionState.Sliding, CharacterMotionState.Falling),
        (CharacterMotionState.Jumping, CharacterMotionState.Sliding),
        (CharacterMotionState.Falling, CharacterMotionState.Sliding),
        (CharacterMotionState.Landing, CharacterMotionState.Sliding),
        (CharacterMotionState.Grounded, CharacterMotionState.Sliding),
        (CharacterMotionState.Sliding, CharacterMotionState.Grounded),
    };

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity entity in Query.ToEntityList())
        {
            float y = entity.GetComponent<Position>().Value.Y;
            if (!entity.HasComponent<CharacterMotion>())
            {
                entity.AddComponent(new CharacterMotion
                {
                    State = OnFloor(entity, y) ? CharacterMotionState.Grounded : CharacterMotionState.Falling,
                    LastY = y,
                });
                Play(entity.GetComponent<CharacterRig>().Runtime, entity.GetComponent<CharacterMotion>().State, restart: false);
                continue;
            }

            ref CharacterMotion motion = ref entity.GetComponent<CharacterMotion>();
            AnimatorRuntime runtime = entity.GetComponent<CharacterRig>().Runtime;
            motion.StateTime += delta;
            bool rising = Rising(entity, y, motion.LastY);
            bool freshRise = rising && FreshRise(entity, y, motion, delta);
            motion.WasRising = rising;
            motion.LastRiseSpeed = UpwardSpeed(entity);
            motion.LastY = y;

            CharacterMotionState next = Next(motion, rising, OnFloor(entity, y), SidewaysSpeed(entity), runtime);
            // A second jump while still jumping replays the jump from the start.
            bool rejump = freshRise && motion.State == CharacterMotionState.Jumping;
            if (next == motion.State && !rejump)
                continue;

            if (Landed(motion.State, next))
            {
                // Dust at the feet: the player's position is its centre.
                Vector2 feet = entity.GetComponent<Position>().Value +
                               new Vector2(0, entity.GetComponent<Player>().Size.Y / 2);
                entity.Store.CreateEntity(new ParticleBurst(feet, ParticleKind.Landing));
            }

            motion.State = next;
            motion.StateTime = 0f;
            Play(runtime, next, restart: rejump);
        }
    }

    /// <summary>Whether a change of state is touching down: from the air onto the floor.</summary>
    public static bool Landed(CharacterMotionState from, CharacterMotionState to) =>
        from is CharacterMotionState.Jumping or CharacterMotionState.Falling &&
        to is CharacterMotionState.Landing or CharacterMotionState.Grounded;

    /// <summary>The state after this frame, given whether the player rose, whether it is on the floor and how fast it moves sideways.</summary>
    public static CharacterMotionState Next(
        CharacterMotion motion,
        bool rising,
        bool onFloor,
        float sidewaysSpeed,
        AnimatorRuntime runtime)
    {
        if (rising)
            return CharacterMotionState.Jumping;

        if (onFloor)
        {
            return motion.State switch
            {
                CharacterMotionState.Jumping or CharacterMotionState.Falling =>
                    runtime.Package.Has(ContactClip.Landing) ? CharacterMotionState.Landing : OnGround(sidewaysSpeed, SlideStartSpeed),
                CharacterMotionState.Landing when motion.StateTime >= runtime.Package.Length(ContactClip.Landing) =>
                    OnGround(sidewaysSpeed, SlideStartSpeed),
                CharacterMotionState.Landing => CharacterMotionState.Landing,
                CharacterMotionState.Sliding => OnGround(sidewaysSpeed, SlideStopSpeed),
                _ => OnGround(sidewaysSpeed, SlideStartSpeed),
            };
        }

        if (motion.State == CharacterMotionState.Jumping && motion.StateTime < JumpHoldSeconds)
            return CharacterMotionState.Jumping;
        return CharacterMotionState.Falling;
    }

    private static CharacterMotionState OnGround(float sidewaysSpeed, float slideSpeed) =>
        sidewaysSpeed >= slideSpeed ? CharacterMotionState.Sliding : CharacterMotionState.Grounded;

    /// <summary>
    /// Clip for a state. A rig without a falling clip keeps its jump pose
    /// while falling, and one without a jump or slide clip stays idle.
    /// </summary>
    public static ContactClip ClipFor(CharacterMotionState state, AnimatorRuntime runtime) =>
        ClipFor(state, runtime.Package);

    public static ContactClip ClipFor(CharacterMotionState state, XafPackage package) => state switch
    {
        CharacterMotionState.Jumping => First(package, ContactClip.Jumping, ContactClip.Idle),
        CharacterMotionState.Falling => First(package, ContactClip.Falling, ContactClip.Jumping, ContactClip.Idle),
        CharacterMotionState.Landing => First(package, ContactClip.Landing, ContactClip.Idle),
        CharacterMotionState.Sliding => First(package, ContactClip.Sliding, ContactClip.Idle),
        _ => ContactClip.Idle,
    };

    /// <summary>
    /// The clip hand-overs this system can ask a rig for, given the clips it
    /// has. The package's transition table decides whether each one happens
    /// and how it blends; one it does not allow leaves the old clip playing.
    /// </summary>
    public static List<(ContactClip From, ContactClip To)> Transitions(XafPackage package)
    {
        bool landing = package.Has(ContactClip.Landing);
        var pairs = new List<(ContactClip, ContactClip)>();
        foreach ((CharacterMotionState from, CharacterMotionState to) in StateChanges)
        {
            // With a landing clip, touching down always lands first.
            if (landing &&
                from is CharacterMotionState.Jumping or CharacterMotionState.Falling &&
                to is CharacterMotionState.Grounded or CharacterMotionState.Sliding)
            {
                continue;
            }

            ContactClip a = ClipFor(from, package);
            ContactClip b = ClipFor(to, package);
            if (a != b && !pairs.Contains((a, b)))
                pairs.Add((a, b));
        }
        return pairs;
    }

    private static void Play(AnimatorRuntime runtime, CharacterMotionState state, bool restart)
    {
        string clip = ClipFor(state, runtime).Id();
        if (restart && runtime.CurrentClip == clip)
            runtime.Restart();
        else
            runtime.Transition(clip);
    }

    /// <summary>Whether the player went up this frame: moved up by a dash or a teleport, or is travelling up.</summary>
    private static bool Rising(Entity entity, float y, float lastY) =>
        Simulated(entity) && (y < lastY - RiseThreshold || UpwardSpeed(entity) > RiseSpeed);

    /// <summary>
    /// Whether this rise is a new jump rather than the one already under way:
    /// the player was not rising, gained speed that gravity could not have given
    /// it, or moved further up than its speed explains, as a dash does.
    /// </summary>
    private static bool FreshRise(Entity entity, float y, in CharacterMotion motion, float delta)
    {
        float speed = UpwardSpeed(entity);
        float travelled = Math.Max(speed, 0f) * delta * 2f;
        return !motion.WasRising ||
               speed > motion.LastRiseSpeed + 1f ||
               y < motion.LastY - RiseThreshold - travelled;
    }

    private static float UpwardSpeed(Entity entity) =>
        entity.TryGetComponent(out PhysicsBody body)
            ? -body.Body.LinearVelocity.Y * Box2DPhysicsWorld.PixelsPerMeter
            : 0f;

    private static float SidewaysSpeed(Entity entity) =>
        entity.TryGetComponent(out PhysicsBody body)
            ? Math.Abs(body.Body.LinearVelocity.X) * Box2DPhysicsWorld.PixelsPerMeter
            : 0f;

    /// <summary>Before play starts the player has no body and just stands where it was placed.</summary>
    private static bool Simulated(Entity entity) => entity.HasComponent<PhysicsBody>();

    private static bool OnFloor(Entity entity, float y)
    {
        if (!Simulated(entity))
            return true;

        if (!entity.TryGetComponent(out PlayfieldView view) ||
            !entity.TryGetComponent(out Playfield playfield))
        {
            return true;
        }

        float x = entity.GetComponent<Position>().Value.X;
        return PlayerGround.TryStanding(entity, view, playfield, new Vector2(x, y), out _);
    }

    private static ContactClip First(XafPackage package, params ContactClip[] clips)
    {
        foreach (ContactClip clip in clips)
        {
            if (package.Has(clip))
                return clip;
        }
        return clips[^1];
    }
}
