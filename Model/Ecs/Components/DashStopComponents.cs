using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A dash ender (like Geometry Dash's S block), freely placed: a dash that
/// enters it ends there, the body stopped on its centre along the dash's line,
/// and runs on from it like after any dash. Nothing else notices it
/// (<see cref="Playfields.DashStops"/> has the shape).
/// </summary>
public struct DashStop(Vector2 position) : IComponent
{
    /// <summary>The marker's centre, playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>When it comes into the level, ms (<see cref="Playfields.SongSpans"/>); the default is always there.</summary>
    public long FromMs = long.MinValue;

    /// <summary>When it leaves the level, ms.</summary>
    public long ToMs = long.MaxValue;

    /// <summary>Whether it is in the level at song time <paramref name="ms"/>.</summary>
    public readonly bool LiveAt(double ms) => Playfields.SongSpans.Holds(FromMs, ToMs, ms);
}
