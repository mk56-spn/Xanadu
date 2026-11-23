using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The curve rework's player body, on the play entity: the hovering square's
/// place and velocity on the playfield (units, units a second), and where the
/// curve the song is on wants it. Written by the curve player system every
/// frame, so a watcher (a test, a debug view) can see how well it follows.
/// </summary>
public struct CurveBody() : IComponent
{
    public Vector2 Position;
    public Vector2 Velocity;

    /// <summary>The curve's point at the song time, the one the body chases; only meaningful while <see cref="OnCurve"/>.</summary>
    public Vector2 Target;

    public bool OnCurve;

    /// <summary>How visible the body is, 0 to 1 (<c>PlayRules.BodyAlpha</c>).</summary>
    public float Alpha;

    /// <summary>Whether a curve has taken the body yet; before that it cruises at its spawn speed, losing none.</summary>
    public bool Steered;

    /// <summary>Whether the body has been put at its start yet.</summary>
    public bool Placed;

    /// <summary>
    /// Settling after a curve: the velocity the body left it with, the fixed-speed
    /// velocity it blends to (straight, by song time), when the blend started and how long it runs, ms.
    /// Past the blend the body cruises at <see cref="ExitTo"/>.
    /// </summary>
    public Vector2 ExitFrom;

    public Vector2 ExitTo;
    public double ExitStartMs;
    public double ExitMs;
    public bool Exiting;

    /// <summary>
    /// The way the body is dashing, unit length, or zero when it isn't. A dash is
    /// instant: set on the hit, the curve player runs it out to a wall, a dash
    /// ender or a pad on its next step.
    /// </summary>
    public Vector2 Dash;

    public readonly bool Dashing => Dash != Vector2.Zero;

    /// <summary>
    /// When the body leaves the entrance: the run's entrance note's time, read once
    /// as the run starts (hit notes are taken away). NaN for a level without one,
    /// where the body is out from the start.
    /// </summary>
    public double ReleaseMs = double.NaN;

    /// <summary>Where the entrance is, playfield units: the body waits in it until <see cref="ReleaseMs"/>.</summary>
    public Vector2 EntranceAt;

    /// <summary>The entrance note, copied as the run starts, for its exit (how it launches the body).</summary>
    public Note Entrance;

    /// <summary>Hanging from an orb: placed on it by song time until its tail, or until let go too early.</summary>
    public bool OnOrb;

    /// <summary>An orb's tail is still to be judged: on the ride key's release, or when held past its window.</summary>
    public bool HoldOpen;

    /// <summary>The orb hold being ridden (or last ridden), copied off its note, which is gone once its head is hit.</summary>
    public Note Ride;

    /// <summary>The key that grabbed the orb; letting it go ends the hold.</summary>
    public Settings.KeybindAction RideKey;

    /// <summary>Where the dash went off, playfield units: the smear runs from here.</summary>
    public Vector2 DashFrom;

    /// <summary>Where the last dash ended, playfield units: it is instant, so a smear is left from <see cref="DashFrom"/> to here.</summary>
    public Vector2 DashTo;

    /// <summary>When the last dash went off, on the body's song clock, ms.</summary>
    public double DashStartMs = double.NegativeInfinity;

    /// <summary>When the last dash hit its wall, on the body's song clock, ms; the body squashes against it.</summary>
    public double ImpactMs = double.NegativeInfinity;

    /// <summary>The way the last dash was going when it hit its wall, unit length.</summary>
    public Vector2 ImpactWay;

    /// <summary>The smooth song clock this was written at, ms (frame-stepped, eased to the audio clock).</summary>
    public double SongMs;
}

/// <summary>
/// When the body last hit a note, on the play entity, so it can punch: squash
/// wide and flash white for a moment after. Song time, ms; the drawer works the
/// punch out from the body's clock, so scrubbing back before the hit drops it.
/// </summary>
public struct BodyPunch() : IComponent
{
    /// <summary>How long a punch lasts, ms.</summary>
    public const double LengthMs = 180;

    public double HitMs = double.NegativeInfinity;

    /// <summary>How hard the punch is at <paramref name="nowMs"/>: 1 on the hit, easing to 0.</summary>
    public readonly float At(double nowMs)
    {
        double age = nowMs - HitMs;
        if (age < 0 || age >= LengthMs)
            return 0f;
        float k = 1f - (float)(age / LengthMs);
        return k * k;
    }
}

/// <summary>
/// The line the body has drawn through its centre so far, on the play entity
/// only in an editor preview, so the mapper sees the path a run takes; back in
/// the editor the last preview's line is kept on the editor entity until the next one.
/// Playfield units; the curve player adds to it and clears it on a restart.
/// </summary>
public struct BodyTrace() : IComponent
{
    /// <summary>A new point is laid once the body is this far from the last one, playfield units.</summary>
    public const float Spacing = 6f;

    public List<Vector2> Points = [];
}

/// <summary>One reading of the body's velocity, at a song time (ms), playfield units a second.</summary>
public readonly record struct SpeedSample(double TimeMs, Vector2 Velocity);

/// <summary>
/// The body's x and y speed over the last few seconds of song time, on the play
/// entity only while the F6 speed graph is on. Cleared on a restart or a jump back.
/// </summary>
public struct SpeedGraph() : IComponent
{
    /// <summary>How much song time the graph keeps, ms.</summary>
    public const double WindowMs = 6000d;

    public List<SpeedSample> Samples = [];
}
