using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// What a hit note leaves behind so the hit lands with some weight: the note
/// pops and sends out a shockwave, sparks in its colour fly off it, and the
/// camera gets a small kick.
/// </summary>
public static class HitEffects
{
    /// <summary>
    /// A hit note pops where it stood, on its own layer; <paramref name="source"/>
    /// is the note's entity, or null for a note that is not an item.
    /// </summary>
    public static void Spawn(EntityStore entities, Entity playfield, Entity source, Note note, Vector2 position,
        Color color, float radius)
    {
        entities.CreateEntity(
            new NotePop(note, position, color, radius, PlayfieldRenderer.NotePopLifetime),
            EffectLayer.From(source));
        // The sparks come off the player's body, thrown on the way the hit
        // travels, from the note through the player; the pop stays on the note.
        Vector2 origin = playfield.TryGetComponent(out Xanadu.Ecs.Position body) ? body.Value : position;
        Vector2 area = playfield.TryGetComponent(out Player player) ? player.Size : default;
        entities.CreateEntity(new ParticleBurst(origin, GameParticles.Hit, color, area, heading: origin - position));
        PlayCameraSystem.Kick(playfield);
    }

    /// <summary>
    /// A hit dash note pops out with sparks where it stood, thrown the way it
    /// sends the player.
    /// </summary>
    public static void SpawnDash(EntityStore entities, Entity noteEntity, Note note, PlayfieldView view, DashDirection heading)
    {
        Vector2 at = view.PlayfieldToScreen(GroupOffset.Shown(noteEntity, note.Position));
        float radius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
        Color color = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(note));
        EffectLayer layer = EffectLayer.From(noteEntity);
        entities.CreateEntity(new NotePop(note, at, color, radius, PlayfieldRenderer.NotePopLifetime), layer);
        Vector2 way = PlayerMovementSystem.DashDirectionVector(heading);
        entities.CreateEntity(new ParticleBurst(at, GameParticles.Hit, color, heading: way));
    }
}
