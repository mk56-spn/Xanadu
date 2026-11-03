using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Playfields;

namespace Xanadu.Systems.Characters;

/// <summary>
/// Plays one of three attack poses on a note hit, layered additively over
/// whatever a rigged player's base animation is doing. Which one plays
/// depends on the note's height relative to the player: one above the
/// player's own height kicks high, one below kicks low, and one close to it
/// kicks at the middle. Three rather than one so repeated hits don't look
/// like the same kick over and over.
/// </summary>
public sealed class CharacterAttackSystem : QuerySystem<CharacterRig, CharacterAttack>
{
    /// <summary>
    /// How far, in screen pixels, a note has to sit above or below the
    /// player before it counts as high or low rather than level with them.
    /// A third of the reach radius, so all three bands get real use within
    /// notes a hit can even reach.
    /// </summary>
    public const float HeightBand = NoteReach.Radius / 3f;

    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        foreach (Entity entity in Query.ToEntityList())
        {
            ref CharacterAttack attack = ref entity.GetComponent<CharacterAttack>();
            if (attack.Clip is not { } clip)
                continue;

            attack.StateTime += delta;
            AnimatorRuntime runtime = entity.GetComponent<CharacterRig>().Runtime;
            if (attack.StateTime >= runtime.Package.Length(clip))
            {
                runtime.RemoveLayer(clip.Id());
                attack.Clip = null;
            }
        }
    }

    /// <summary>
    /// Layers the attack pose for a note hit at <paramref name="noteScreenY"/>
    /// onto <paramref name="entity"/>'s rig, relative to the player at
    /// <paramref name="playerScreenY"/>. Does nothing if the entity has no rig.
    /// </summary>
    public static void Trigger(Entity entity, float noteScreenY, float playerScreenY)
    {
        if (!entity.TryGetComponent(out CharacterRig rig))
            return;

        ContactClip clip = ClipFor(noteScreenY - playerScreenY, Airborne(entity));

        if (!entity.HasComponent<CharacterAttack>())
            entity.AddComponent(new CharacterAttack());

        ref CharacterAttack attack = ref entity.GetComponent<CharacterAttack>();
        if (attack.Clip is { } previous)
            rig.Runtime.RemoveLayer(previous.Id());

        rig.Runtime.AddLayer(clip.Id(), 1f);
        attack.Clip = clip;
        attack.StateTime = 0f;
    }

    /// <summary>Up is negative: a note above the player (smaller Y) kicks high, below kicks low.</summary>
    private static ContactClip ClipFor(float dy, bool airborne) =>
        dy <= -HeightBand ? airborne ? ContactClip.AttackAirHigh : ContactClip.AttackHigh :
        dy >= HeightBand ? airborne ? ContactClip.AttackAirLow : ContactClip.AttackLow :
        airborne ? ContactClip.AttackAirMid : ContactClip.AttackMid;

    /// <summary>A rig that has not settled into a motion state yet counts as grounded.</summary>
    private static bool Airborne(Entity entity) =>
        entity.TryGetComponent(out CharacterMotion motion) &&
        motion.State is CharacterMotionState.Jumping or CharacterMotionState.Falling;
}
