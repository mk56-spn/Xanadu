using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Characters;

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

    /// <summary>Slack in pixels for resting on a block, whose contact leaves a small gap.</summary>
    private const float BlockTolerance = 2f;

    /// <summary>
    /// Every change of state <see cref="Next"/> can make. Rising always jumps,
    /// leaving the floor falls, touching it lands (or goes straight to the
    /// ground without a landing clip), and a landing ends on the ground.
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
    public static string ClipFor(CharacterMotionState state, AnimatorRuntime runtime) =>
        ClipFor(state, runtime.Package);

    public static string ClipFor(CharacterMotionState state, XafPackage package) => state switch
    {
        CharacterMotionState.Jumping => First(package, JumpClip, IdleClip),
        CharacterMotionState.Falling => First(package, FallClip, JumpClip, IdleClip),
        CharacterMotionState.Landing => First(package, LandClip, IdleClip),
        _ => IdleClip,
    };

    /// <summary>
    /// The clip hand-overs this system can ask a rig for, given the clips it
    /// has. The package's transition table decides whether each one happens
    /// and how it blends; one it does not allow leaves the old clip playing.
    /// </summary>
    public static List<(string From, string To)> Transitions(XafPackage package)
    {
        bool landing = HasClip(package, LandClip);
        var pairs = new List<(string, string)>();
        foreach ((CharacterMotionState from, CharacterMotionState to) in StateChanges)
        {
            // With a landing clip, touching down always lands first.
            if (landing && to == CharacterMotionState.Grounded && from != CharacterMotionState.Landing)
                continue;

            string a = ClipFor(from, package);
            string b = ClipFor(to, package);
            if (a != b && !pairs.Contains((a, b)))
                pairs.Add((a, b));
        }
        return pairs;
    }

    private static void Play(AnimatorRuntime runtime, CharacterMotionState state, bool restart)
    {
        string clip = ClipFor(state, runtime);
        if (restart && runtime.CurrentClip == clip)
            runtime.Restart();
        else
            runtime.Transition(clip);
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
        return y >= floor - FloorTolerance || OnBlock(entity, view, y);
    }

    /// <summary>Whether the player's feet rest on the top of a block.</summary>
    private static bool OnBlock(Entity entity, PlayfieldView view, float y)
    {
        Vector2 size = entity.GetComponent<Player>().Size;
        float x = entity.GetComponent<Position>().Value.X;
        float feet = y + size.Y / 2;

        foreach (Entity blockEntity in entity.Store.Query<Block>().ToEntityList())
        {
            Block block = blockEntity.GetComponent<Block>();
            Vector2 topLeft = view.PlayfieldToScreen(block.Position);
            Vector2 bottomRight = view.PlayfieldToScreen(block.End);
            bool overlapsX = x + size.X / 2 > topLeft.X && x - size.X / 2 < bottomRight.X;
            if (overlapsX && Mathf.Abs(feet - topLeft.Y) <= BlockTolerance)
                return true;
        }

        return false;
    }

    private static string First(XafPackage package, params string[] clips)
    {
        foreach (string clip in clips)
        {
            if (HasClip(package, clip))
                return clip;
        }
        return clips[^1];
    }

    private static bool HasClip(AnimatorRuntime runtime, string id) => HasClip(runtime.Package, id);

    private static bool HasClip(XafPackage package, string id) =>
        package.Clips.Exists(clip => clip.Id == id);

    private static float ClipLength(AnimatorRuntime runtime, string id) =>
        runtime.Package.Clips.Find(clip => clip.Id == id)?.Duration ?? 0f;
}
