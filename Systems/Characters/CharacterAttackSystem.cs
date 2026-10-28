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
    public const string LowClip = "attack_low";
    public const string MidClip = "attack_mid";
    public const string HighClip = "attack_high";

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
            if (string.IsNullOrEmpty(attack.ClipId))
                continue;

            attack.StateTime += delta;
            AnimatorRuntime runtime = entity.GetComponent<CharacterRig>().Runtime;
            if (attack.StateTime >= ClipLength(runtime, attack.ClipId))
            {
                runtime.RemoveLayer(attack.ClipId);
                attack.ClipId = "";
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

        string clipId = ClipFor(noteScreenY - playerScreenY);

        if (!entity.HasComponent<CharacterAttack>())
            entity.AddComponent(new CharacterAttack());

        ref CharacterAttack attack = ref entity.GetComponent<CharacterAttack>();
        if (!string.IsNullOrEmpty(attack.ClipId))
            rig.Runtime.RemoveLayer(attack.ClipId);

        rig.Runtime.AddLayer(clipId, 1f);
        attack.ClipId = clipId;
        attack.StateTime = 0f;
    }

    /// <summary>Up is negative: a note above the player (smaller Y) kicks high, below kicks low.</summary>
    private static string ClipFor(float dy) =>
        dy <= -HeightBand ? HighClip :
        dy >= HeightBand ? LowClip :
        MidClip;

    private static float ClipLength(AnimatorRuntime runtime, string clipId) =>
        runtime.Package.Clips.Find(clip => clip.Id == clipId)?.Duration ?? 0f;
}
