using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// A pad, freely placed in the level like a Geometry Dash pad: touching it sets
/// the body's speed upward to <see cref="Speed"/> (a set, not a push, so a run
/// always leaves it the same way). A dash that reaches a pad ends there and
/// leaves on the pad's launch. The player doesn't trigger it and it isn't judged
/// (<see cref="Playfields.Pads"/> has the shape).
/// <para>
/// A timed pad (<see cref="TimeMs"/> set) fires on its time instead of on touch:
/// when a fall should launch on a note, it puts the body standing on it at that
/// moment exactly and launches, so where the body happened to be doesn't matter.
/// </para>
/// </summary>
public struct Pad(Vector2 position, float speed, long timeMs = -1) : IComponent
{
    /// <summary>The pad's centre, playfield units.</summary>
    public Vector2 Position = position;

    /// <summary>The upward speed it launches at, playfield units a second.</summary>
    public float Speed = speed;

    /// <summary>When a timed pad fires, ms; -1 for a pad that fires on touch.</summary>
    public long TimeMs = timeMs;

    public readonly bool IsTimed => TimeMs >= 0;

    /// <summary>When it comes into the level, ms (<see cref="Playfields.SongSpans"/>); the default is always there.</summary>
    public long FromMs = long.MinValue;

    /// <summary>When it leaves the level, ms.</summary>
    public long ToMs = long.MaxValue;

    /// <summary>Whether it is in the level at song time <paramref name="ms"/>.</summary>
    public readonly bool LiveAt(double ms) => Playfields.SongSpans.Holds(FromMs, ToMs, ms);

    /// <summary>When it last launched the body, on the body's song clock, ms; play only, for its spring.</summary>
    public double FiredMs = double.NegativeInfinity;
}
