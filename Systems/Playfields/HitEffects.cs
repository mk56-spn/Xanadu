using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// What a hit note leaves behind so the hit lands with some weight: the note
/// pops and sends out a shockwave, sparks in its colour fly off it, and the
/// camera gets a small kick.
/// </summary>
public static class HitEffects
{
    public static void Spawn(EntityStore entities, Entity playfield, Note note, Vector2 position, Color color,
        float radius)
    {
        entities.CreateEntity(new NotePop(note, position, color, radius, PlayfieldRenderer.NotePopLifetime));
        entities.CreateEntity(new ParticleBurst(position, ParticleKind.Hit, color));
        PlayCameraSystem.Kick(playfield);
    }
}
