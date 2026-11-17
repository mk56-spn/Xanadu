using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The curve rework's player body, on the play entity: the hovering square's
/// place and velocity on the playfield (units, units a second), and where the
/// curve the song is on wants it. Written by the curve player system every
/// frame, so a watcher (a test, a debug view) can see how well it follows.
/// </summary>
public struct CurveBody : IComponent
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

    /// <summary>The smooth song clock this was written at, ms (frame-stepped, eased to the audio clock).</summary>
    public double SongMs;
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

    public List<Vector2> Points = new();
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

    public List<SpeedSample> Samples = new();
}
