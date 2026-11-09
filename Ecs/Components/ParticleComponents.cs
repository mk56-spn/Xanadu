using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// The names of the library effects gameplay sets off. Their looks live in the
/// particle library and are tuned in the particle editor.
/// </summary>
public static class GameParticles
{
    /// <summary>Dust kicked up where the player lands.</summary>
    public const string Landing = "landing";

    /// <summary>Note glyphs sprayed off the body where a dash or push goes off, in the note's colour.</summary>
    public const string Dash = "dash";

    /// <summary>Note glyphs thrown off when a note is hit, in the note's colour.</summary>
    public const string Hit = "hit";

    /// <summary>
    /// A streak of note glyphs left along a dash's path, from where the player
    /// vanished to where it appeared, that drift back the way it came and fade.
    /// </summary>
    public const string DashStreak = "dash-streak";

    /// <summary>
    /// The player bursting apart on a spike: a big spray of note glyphs thrown
    /// out of the body in every direction, falling, shrinking and fading.
    /// </summary>
    public const string Shatter = "shatter";

    /// <summary>Embers drifting up off a friction bar; white, so the burst's tint gives them the bar's colour.</summary>
    public const string Ember = "ember";

    /// <summary>Every name above, which the library must hold for gameplay to look right.</summary>
    public static readonly string[] All = { Landing, Dash, Hit, DashStreak, Shatter, Ember };
}

/// <summary>
/// A request for one burst of a library effect at a screen position. Gameplay
/// systems create it; <c>ParticleBurstSystem</c> hands it to the emitter and removes it.
/// </summary>
public struct ParticleBurst(
    Vector2 position,
    string particle,
    Color? tint = null,
    Vector2 area = default,
    float angle = 0f,
    Vector2 heading = default) : IComponent
{
    public Vector2 Position = position;

    /// <summary>The library effect's name, such as one of <see cref="GameParticles"/>.</summary>
    public string Particle = particle;

    /// <summary>Multiplies the effect's own colours; white leaves them as they are.</summary>
    public Color Tint = tint ?? Colors.White;

    /// <summary>The box around the position particles start in, such as the player's body; zero keeps the effect's own.</summary>
    public Vector2 Area = area;

    /// <summary>How far the burst, its box and its directions are turned, in radians.</summary>
    public float Angle = angle;

    /// <summary>
    /// The way the hit that set the burst off travels, on screen: the particles
    /// are thrown in a cone that way. Zero keeps the effect's own direction.
    /// </summary>
    public Vector2 Heading = heading;

    /// <summary>
    /// A <see cref="GameParticles.DashStreak"/> along the path from
    /// <paramref name="from"/> to <paramref name="to"/>, <paramref name="width"/> across.
    /// </summary>
    public static ParticleBurst Streak(Vector2 from, Vector2 to, float width, Color tint) =>
        new(
            (from + to) / 2,
            GameParticles.DashStreak,
            tint,
            new Vector2(from.DistanceTo(to), width),
            (to - from).Angle());
}
