using System;
using Friflo.Engine.ECS;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

/// <summary>
/// The route a route note sends the body down once its head is hit: straight
/// stretches with right-angle turns, all at one of the three fixed speeds
/// (<see cref="DashStrength"/>, <see cref="SpeedOf"/>). The mapper only sets
/// when the turns come (<see cref="TurnsMs"/>, after the head); the player
/// picks each direction in play (<see cref="RouteRun"/>). At a fixed speed a
/// stretch's length is the time to the next turn, so the shape reads as rhythm.
/// The last time is the route's end, not a turn.
/// </summary>
/// <remarks>
/// Never changed in place: retiming makes a new one, so the copies undo, copy
/// and paste keep can share it safely.
/// </remarks>
public sealed class NoteRoute : IEquatable<NoteRoute>
{
    /// <summary>A route needs its end at least.</summary>
    public const int MinTimes = 1;

    public const int MaxTimes = 256;

    /// <summary>The 1× speed, and the body's speed at spawn.</summary>
    public const float DefaultSpeed = 900f;

    /// <summary>The speed each of the three fixed choices stands for, playfield units a second: 1×, 1.5× and 2× the default.</summary>
    public static float SpeedOf(DashStrength strength) => strength switch
    {
        DashStrength.Double => DefaultSpeed * 1.5f,
        DashStrength.Triple => DefaultSpeed * 2f,
        _ => DefaultSpeed,
    };

    private readonly long[] _timesMs;

    /// <summary>A route at <paramref name="strength"/>'s speed, turning at each of <paramref name="timesMs"/> (ms after the head, rising) and ending at the last.</summary>
    public NoteRoute(DashStrength strength, ReadOnlySpan<long> timesMs)
    {
        if (timesMs.Length < MinTimes)
            throw new ArgumentException("A route needs its end.");
        for (int i = 0; i < timesMs.Length; i++)
        {
            if (timesMs[i] <= (i == 0 ? 0 : timesMs[i - 1]))
                throw new ArgumentException("A route's times must rise after the head.");
        }

        Strength = strength;
        _timesMs = timesMs.ToArray();
    }

    public DashStrength Strength { get; }

    /// <summary>The body's speed along the whole route, playfield units a second.</summary>
    public float Speed => SpeedOf(Strength);

    /// <summary>When each turn comes, ms after the head; the last is the end.</summary>
    public ReadOnlySpan<long> TimesMs => _timesMs;

    /// <summary>How many turns the player can take: every time but the end.</summary>
    public int Turns => _timesMs.Length - 1;

    /// <summary>How long the route runs from its head, ms.</summary>
    public long DurationMs => _timesMs[^1];

    /// <summary>The same route at another of the three speeds.</summary>
    public NoteRoute WithStrength(DashStrength strength) => new(strength, _timesMs);

    public bool Equals(NoteRoute? other) =>
        other is not null &&
        (ReferenceEquals(this, other) ||
         (Strength == other.Strength && _timesMs.AsSpan().SequenceEqual(other._timesMs)));

    public override bool Equals(object? obj) => obj is NoteRoute other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Strength);
        foreach (long time in _timesMs)
            hash.Add(time);
        return hash.ToHashCode();
    }

    /// <summary>Orders routes by length, then speed, then time by time; null first.</summary>
    public static int Compare(NoteRoute? a, NoteRoute? b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a is null)
            return -1;
        if (b is null)
            return 1;
        int order = a._timesMs.Length.CompareTo(b._timesMs.Length);
        if (order == 0)
            order = a.Strength.CompareTo(b.Strength);
        for (int i = 0; order == 0 && i < a._timesMs.Length; i++)
            order = a._timesMs[i].CompareTo(b._timesMs[i]);
        return order;
    }
}

/// <summary>A way a route can head: the four directions, or a turn not taken yet.</summary>
public enum RouteHeading : byte
{
    /// <summary>No press: at a turn, carry straight on.</summary>
    None,
    Left,
    Right,
    Up,
    Down,
}

public static class RouteHeadings
{
    /// <summary>The unit step for a heading on the playfield (y points down); zero for none.</summary>
    public static Vector2 Step(RouteHeading heading) => heading switch
    {
        RouteHeading.Left => -Vector2.UnitX,
        RouteHeading.Right => Vector2.UnitX,
        RouteHeading.Up => -Vector2.UnitY,
        RouteHeading.Down => Vector2.UnitY,
        _ => Vector2.Zero,
    };

    /// <summary>The heading a direction key asks for; none for an action key.</summary>
    public static RouteHeading Of(KeybindAction action) => action switch
    {
        KeybindAction.Left => RouteHeading.Left,
        KeybindAction.Right => RouteHeading.Right,
        KeybindAction.Up => RouteHeading.Up,
        KeybindAction.Down => RouteHeading.Down,
        _ => RouteHeading.None,
    };

    /// <summary>Whether two headings are at right angles: only those make a turn.</summary>
    public static bool Perpendicular(RouteHeading a, RouteHeading b) =>
        a != RouteHeading.None && b != RouteHeading.None &&
        (a is RouteHeading.Left or RouteHeading.Right) != (b is RouteHeading.Left or RouteHeading.Right);
}

/// <summary>
/// A hit route note's run, on the note: the way its head was hit, and at each
/// turn the way the player pressed (none carries straight on). A turn is
/// settled once pressed, or once its window has passed.
/// </summary>
public struct RouteRun() : IComponent
{
    public RouteHeading Start;

    /// <summary>One per turn (<see cref="NoteRoute.Turns"/>): the heading picked there; none goes straight.</summary>
    public RouteHeading[] Turns = [];

    /// <summary>One per turn: whether a press has settled it, so a second press does nothing.</summary>
    public bool[] Pressed = [];
}
