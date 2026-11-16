using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Ecs.Components;
using Xanadu.Systems.Physics;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Core.Position;

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

    /// <summary>Seconds between puffs of dust off the feet while sliding.</summary>
    public const float SlideDustSeconds = 0.06f;

    /// <summary>
    /// How long before she touches down she starts bracing for it, in seconds:
    /// the landing's first pose eases in over her fall so she reaches for the
    /// ground instead of meeting it stiff. Anything that would cut the fall
    /// short (a dash, a momentum note) is an instant change anyway, so a brace
    /// that never lands costs nothing.
    /// </summary>
    public const float BraceSeconds = 0.15f;

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

            bool onFloor = OnFloor(entity, y);
            CharacterMotionState next = Next(motion, rising, onFloor, SidewaysSpeed(entity), runtime);
            bool bracing = motion.Brace > 0f;
            Brace(entity, ref motion, runtime, rising || onFloor ? CharacterMotionState.Grounded : next);
            if (next == CharacterMotionState.Sliding)
                SlideDust(entity, ref motion, delta);
            else
                motion.SinceSlideDust = SlideDustSeconds;
            // A second jump while still jumping replays the jump from the start.
            bool rejump = freshRise && motion.State == CharacterMotionState.Jumping;
            if (next == motion.State && !rejump)
                continue;

            if (Landed(motion.State, next))
            {
                // Dust at the feet: the player's position is its centre.
                Vector2 feet = entity.GetComponent<Position>().Value +
                               new Vector2(0, entity.GetComponent<Player>().Size.Y / 2);
                entity.Store.CreateEntity(new ParticleBurst(feet, GameParticles.Landing));
            }

            motion.State = next;
            motion.StateTime = 0f;
            // Braced, the pose is already the landing's first; cut straight to it.
            if (bracing && next == CharacterMotionState.Landing)
                runtime.Play(ContactClip.Landing.Id(), 0f);
            else
                Play(runtime, next, restart: rejump);
        }
    }

    /// <summary>How hard the flinch is that carries her upper body on past where a dash puts her down, 0 to 1.</summary>
    public const float DashExitCarry = 0.6f;

    /// <summary>How quickly she drops into the landing's crouch out of a grounded dash, in seconds; a crossfade rig takes it as its fade.</summary>
    public const float DashExitSettle = 0.12f;

    /// <summary>
    /// A dash just put the player down at its end, <paramref name="way"/> being
    /// the way it went on screen. Her upper body is carried on past and springs
    /// back (the bump flinch, pushed the way she went), and if the dash left
    /// her on the ground she comes out of it through the landing clip from its
    /// crouch, rather than standing there as if she had never moved. Changes
    /// no components' presence, so it is safe from inside a query.
    /// </summary>
    public static void DashExit(Entity entity, Vector2 way)
    {
        if (way.LengthSquared() > 0f && entity.HasComponent<CharacterBump>())
        {
            ref CharacterBump bump = ref entity.GetComponent<CharacterBump>();
            bump.Normal = Vector2.Normalize(way);
            bump.Strength = DashExitCarry;
            bump.Age = 0f;
        }

        if (!entity.TryGetComponent(out CharacterRig rig) || !entity.HasComponent<CharacterMotion>() ||
            !rig.Runtime.Package.Has(ContactClip.Landing) ||
            !OnFloor(entity, entity.GetComponent<Position>().Value.Y))
        {
            return;
        }

        ref CharacterMotion motion = ref entity.GetComponent<CharacterMotion>();
        motion.State = CharacterMotionState.Landing;
        motion.StateTime = 0f;
        motion.Brace = 0f;
        string landing = ContactClip.Landing.Id();
        if (rig.Runtime.CurrentClip == landing && !rig.Runtime.IsCrossfading)
            rig.Runtime.Restart();
        else
            rig.Runtime.Play(landing, DashExitSettle);
        rig.Runtime.RemoveLayer(landing);
    }

    /// <summary>
    /// Lays the landing's first pose over a fall that will touch down within
    /// <see cref="BraceSeconds"/>, more of it the closer the ground; takes it
    /// off again once she is not falling towards it.
    /// </summary>
    private static void Brace(Entity entity, ref CharacterMotion motion, AnimatorRuntime runtime, CharacterMotionState airborne)
    {
        string landing = ContactClip.Landing.Id();
        float brace = 0f;
        if (airborne is CharacterMotionState.Jumping or CharacterMotionState.Falling &&
            runtime.Package.Has(ContactClip.Landing) &&
            entity.TryGetComponent(out PhysicsBody body) &&
            entity.TryGetComponent(out PlayfieldView view) &&
            entity.TryGetComponent(out Playfield playfield))
        {
            float scale = Box2DPhysicsWorld.PixelsPerMeter;
            Vector2 velocity = body.Body.LinearVelocity * scale;
            float gravity = body.Body.World.Gravity.Y * scale * Box2DPhysicsWorld.GravityScale(body.Body);
            if (velocity.Y > 0f &&
                PlayerGround.TimeToLand(entity, view, playfield, entity.GetComponent<Position>().Value, velocity, gravity, BraceSeconds) is { } left)
            {
                float share = 1f - left / BraceSeconds;
                brace = share * share * (3f - 2f * share);
            }
        }

        motion.Brace = brace;
        if (brace > 0f)
            runtime.HoldLayer(landing, brace, 0f, XafLayerBlend.Override);
        else
            runtime.RemoveLayer(landing);
    }

    /// <summary>
    /// Puffs dust off the feet while sliding, thrown back against the way she
    /// travels; the first puff goes off as the slide starts.
    /// </summary>
    private static void SlideDust(Entity entity, ref CharacterMotion motion, float delta)
    {
        motion.SinceSlideDust += delta;
        if (motion.SinceSlideDust < SlideDustSeconds)
            return;

        motion.SinceSlideDust = 0f;
        float way = entity.TryGetComponent(out PhysicsBody body) ? Math.Sign(body.Body.LinearVelocity.X) : 0f;
        Vector2 size = entity.GetComponent<Player>().Size;
        Vector2 feet = entity.GetComponent<Position>().Value + new Vector2(0, size.Y / 2);
        entity.Store.CreateEntity(new ParticleBurst(
            feet, GameParticles.Landing, area: new Vector2(size.X * 0.4f, 4f), heading: new Vector2(-way, -0.6f)));
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
