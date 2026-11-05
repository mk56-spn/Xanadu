using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>The looks a one-off burst of particles can have.</summary>
public enum ParticleKind
{
    /// <summary>Dust kicked up where the player lands.</summary>
    Landing,

    /// <summary>Solid squares sprayed off the body where a dash or push goes off, in the note's colour.</summary>
    Dash,

    /// <summary>Solid squares thrown off when a note is hit, in the note's colour.</summary>
    Hit,

    /// <summary>
    /// A streak of squares left along a dash's path, from where the player
    /// vanished to where it appeared, that drift back the way it came and fade.
    /// </summary>
    DashStreak,
}

/// <summary>
/// A request for one burst of particles at a screen position. Gameplay systems
/// create it; <c>ParticleBurstSystem</c> hands it to the emitter and removes it.
/// </summary>
public struct ParticleBurst(
    Vector2 position,
    ParticleKind kind,
    Color? tint = null,
    Vector2 area = default,
    float angle = 0f) : IComponent
{
    public Vector2 Position = position;
    public ParticleKind Kind = kind;

    /// <summary>Multiplies the burst's own colours; white leaves them as they are.</summary>
    public Color Tint = tint ?? Colors.White;

    /// <summary>The box around the position particles start in, such as the player's body; zero is a point.</summary>
    public Vector2 Area = area;

    /// <summary>How far the burst, its box and its directions are turned, in radians.</summary>
    public float Angle = angle;

    /// <summary>
    /// A <see cref="ParticleKind.DashStreak"/> along the path from
    /// <paramref name="from"/> to <paramref name="to"/>, <paramref name="width"/> across.
    /// </summary>
    public static ParticleBurst Streak(Vector2 from, Vector2 to, float width, Color tint) =>
        new(
            (from + to) / 2,
            ParticleKind.DashStreak,
            tint,
            new Vector2(from.DistanceTo(to), width),
            (to - from).Angle());
}
