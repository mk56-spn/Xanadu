using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Particles;

/// <summary>Plays one-off particle bursts; the Godot implementation is the only one that draws.</summary>
public interface IParticleEmitter
{
    /// <summary>
    /// Sets off one burst of <paramref name="kind"/> at a screen position, its
    /// colours multiplied by <paramref name="tint"/>, its box and directions
    /// turned by <paramref name="angle"/> radians.
    /// </summary>
    void Burst(Vector2 position, ParticleKind kind, Color tint, Vector2 area = default, float angle = 0f);
}
