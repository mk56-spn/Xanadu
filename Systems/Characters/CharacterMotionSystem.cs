using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Animation;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Characters;

/// <summary>
/// Works out whether each rigged player is on the ground, jumping or falling
/// and plays the matching clip. The only ground is the arena floor, and dashes
/// teleport, so the state comes from where the player is and how it moved:
/// moving up is a jump, airborne otherwise is a fall, and touching the floor
/// plays the landing clip once before going back to idle.
/// </summary>
public sealed class CharacterMotionSystem : QuerySystem<CharacterRig, Player, Position>
{
    public const string IdleClip = CharacterRigLoadSystem.IdleClip;
    public const string JumpClip = "jumping";
    public const string FallClip = "falling";
    public const string LandClip = "landing";

    /// <summary>A dash is instant, so a jump is held this long before it can turn into a fall.</summary>
    public const float JumpHoldSeconds = 0.3f;

    /// <summary>Upward movement in one frame, in pixels, that counts as a jump.</summary>
    public const float RiseThreshold = 1f;

    /// <summary>Slack in pixels when testing whether the player rests on the floor.</summary>
    private const float FloorTolerance = 0.5f;

    private const float FadeSeconds = 0.1f;

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
            bool rising = Simulated(entity) && y < motion.LastY - RiseThreshold;
            motion.LastY = y;

            CharacterMotionState next = Next(motion, rising, OnFloor(entity, y), runtime);
            // A second jump while still jumping replays the jump from the start.
            bool rejump = rising && motion.State == CharacterMotionState.Jumping;
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

    /// <summary>The state after this frame, given whether the player rose and whether it is on the floor.</summary>
    public static CharacterMotionState Next(
        CharacterMotion motion,
        bool rising,
        bool onFloor,
        AnimatorRuntime runtime)
    {
        if (rising)
            return CharacterMotionState.Jumping;

        if (onFloor)
        {
            return motion.State switch
            {
                CharacterMotionState.Jumping or CharacterMotionState.Falling =>
                    HasClip(runtime, LandClip) ? CharacterMotionState.Landing : CharacterMotionState.Grounded,
                CharacterMotionState.Landing when motion.StateTime >= ClipLength(runtime, LandClip) =>
                    CharacterMotionState.Grounded,
                _ => motion.State,
            };
        }

        if (motion.State == CharacterMotionState.Jumping && motion.StateTime < JumpHoldSeconds)
            return CharacterMotionState.Jumping;
        return CharacterMotionState.Falling;
    }

    /// <summary>
    /// Clip for a state. A rig without a falling clip keeps its jump pose
    /// while falling, and one without a jump clip stays idle.
    /// </summary>
    public static string ClipFor(CharacterMotionState state, AnimatorRuntime runtime) => state switch
    {
        CharacterMotionState.Jumping => First(runtime, JumpClip, IdleClip),
        CharacterMotionState.Falling => First(runtime, FallClip, JumpClip, IdleClip),
        CharacterMotionState.Landing => First(runtime, LandClip, IdleClip),
        _ => IdleClip,
    };

    private static void Play(AnimatorRuntime runtime, CharacterMotionState state, bool restart)
    {
        string clip = ClipFor(state, runtime);
        if (restart && runtime.CurrentClip == clip)
            runtime.Restart();
        else
            runtime.Play(clip, FadeSeconds);
    }

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

        float floor = view.ScreenOrigin.Y + playfield.Height * view.Scale -
                      entity.GetComponent<Player>().Size.Y / 2;
        return y >= floor - FloorTolerance;
    }

    private static string First(AnimatorRuntime runtime, params string[] clips)
    {
        foreach (string clip in clips)
        {
            if (HasClip(runtime, clip))
                return clip;
        }
        return clips[^1];
    }

    private static bool HasClip(AnimatorRuntime runtime, string id) =>
        runtime.Package.Clips.Exists(clip => clip.Id == id);

    private static float ClipLength(AnimatorRuntime runtime, string id) =>
        runtime.Package.Clips.Find(clip => clip.Id == id)?.Duration ?? 0f;
}
