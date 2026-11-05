using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Particles;

/// <summary>Plays one-off particle bursts; the Godot implementation is the only one that draws.</summary>
public interface IParticleEmitter
{
    /// <summary>
    /// Sets off one burst of <paramref name="kind"/> at a screen position, its
    /// colours multiplied by <paramref name="tint"/>, its box and directions
    /// turned by <paramref name="angle"/> radians. A non-zero
    /// <paramref name="heading"/>, on screen, throws the particles in a cone
    /// that way instead of the kind's own directions.
    /// </summary>
    void Burst(
        Vector2 position,
        ParticleKind kind,
        Color tint,
        Vector2 area = default,
        float angle = 0f,
        Vector2 heading = default);
}
