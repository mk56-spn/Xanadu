using Godot;
using Writer.Ecs;

namespace Writer.Systems.Particles;

/// <summary>Plays one-off particle bursts; the Godot implementation is the only one that draws.</summary>
public interface IParticleEmitter
{
    /// <summary>Sets off one burst of <paramref name="kind"/> at a screen position.</summary>
    void Burst(Vector2 position, ParticleKind kind);
}
