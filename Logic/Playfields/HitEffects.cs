using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// What a hit note leaves behind so the hit lands with some weight: the note
/// pops and sends out a shockwave, sparks in its colour fly off it, the body
/// punches (<see cref="BodyPunch"/>) and the camera gets a small kick.
/// </summary>
public static class HitEffects
{
    /// <summary>
    /// A hit note pops where it stood, on its own layer; <paramref name="source"/>
    /// is the note's entity, or null for a note that is not an item.
    /// </summary>
    public static void Spawn(EntityStore entities, Entity playfield, Entity source, Note note, Vector2 position,
        Color color, float radius, int chartIndex = -1)
    {
        entities.CreateEntity(
            new NotePop(note, position, color, radius, PlayfieldMetrics.NotePopLifetime, chartIndex));
        // The sparks come off the player's body, thrown on the way the hit
        // travels, from the note through the player; the pop stays on the note.
        Vector2 origin = playfield.TryGetComponent(out Position body) ? body.Value : position;
        Vector2 area = playfield.TryGetComponent(out Player player) ? player.Size : default;
        entities.CreateEntity(new ParticleBurst(origin, GameParticles.Hit, color, area, heading: origin - position));
        PlayCameraSystem.Kick(playfield);
        if (playfield.HasComponent<BodyPunch>() && playfield.TryGetComponent(out Playhead playhead))
            playfield.GetComponent<BodyPunch>().HitMs = playhead.ShownMs;
    }
}
