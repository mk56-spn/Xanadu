using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>The looks a one-off burst of particles can have.</summary>
public enum ParticleKind
{
    /// <summary>Dust kicked up where the player lands.</summary>
    Landing,

    /// <summary>A spray where a dash ends.</summary>
    Dash,

    /// <summary>Sparks flying off a note that was hit, in the note's colour.</summary>
    Hit,
}

/// <summary>
/// A request for one burst of particles at a screen position. Gameplay systems
/// create it; <c>ParticleBurstSystem</c> hands it to the emitter and removes it.
/// </summary>
public struct ParticleBurst(Vector2 position, ParticleKind kind, Color? tint = null) : IComponent
{
    public Vector2 Position = position;
    public ParticleKind Kind = kind;

    /// <summary>Multiplies the burst's own colours; white leaves them as they are.</summary>
    public Color Tint = tint ?? Colors.White;
}
