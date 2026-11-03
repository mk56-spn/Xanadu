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
        // The sparks come off the player's body; the pop stays on the note.
        Vector2 origin = playfield.TryGetComponent(out Xanadu.Ecs.Position body) ? body.Value : position;
        Vector2 area = playfield.TryGetComponent(out Player player) ? player.Size : default;
        entities.CreateEntity(new ParticleBurst(origin, ParticleKind.Hit, color, area));
        PlayCameraSystem.Kick(playfield);
    }

    /// <summary>
    /// A hit dash note pops out with sparks where it stood, and leaves its
    /// connector and exit behind to fade, the exit last.
    /// </summary>
    public static void SpawnDash(EntityStore entities, Entity noteEntity, Note note, PlayfieldView view)
    {
        Vector2 at = view.PlayfieldToScreen(note.Position);
        float radius = PlayfieldRenderer.NoteSize * view.Scale / 2f;
        Color color = GroupColor.For(noteEntity, PlayfieldRenderer.ColorFor(note));
        entities.CreateEntity(new NotePop(note, at, color, radius, PlayfieldRenderer.NotePopLifetime));
        entities.CreateEntity(new ParticleBurst(at, ParticleKind.Hit, color));
        entities.CreateEntity(new DashLinkFade(at, PlayfieldRenderer.DashExitScreen(note, view), color, radius));
    }
}
