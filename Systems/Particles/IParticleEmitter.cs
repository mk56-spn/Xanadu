using Godot;

namespace Xanadu.Systems.Particles;

/// <summary>Plays one-off particle bursts.</summary>
public interface IParticleEmitter
{
    /// <summary>
    /// Sets off one burst of the library effect called <paramref name="particle"/>
    /// at a screen position, its colours multiplied by <paramref name="tint"/>,
    /// its box and directions turned by <paramref name="angle"/> radians. A
    /// non-zero <paramref name="heading"/>, on screen, throws the particles in a
    /// cone that way instead of the effect's own direction. An unknown name does nothing.
    /// </summary>
    void Burst(
        string particle,
        Vector2 position,
        Color tint,
        Vector2 area = default,
        float angle = 0f,
        Vector2 heading = default);
}

/// <summary>Draws nothing: for tests and hosts without particles.</summary>
public sealed class SilentParticleEmitter : IParticleEmitter
{
    public void Burst(string particle, Vector2 position, Color tint, Vector2 area = default, float angle = 0f, Vector2 heading = default) { }
}
