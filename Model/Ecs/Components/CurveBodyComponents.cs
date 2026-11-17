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

    /// <summary>The point on the curve nearest the body, the one the line pulls toward; only meaningful while <see cref="OnCurve"/>.</summary>
    public Vector2 Target;

    public bool OnCurve;

    /// <summary>Whether the body has been put at its start yet.</summary>
    public bool Placed;

    /// <summary>The song time this was written at, ms.</summary>
    public long SongMs;
}
