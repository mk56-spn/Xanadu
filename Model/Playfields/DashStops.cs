using System;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>The dash ender's shape: one fixed square, and whether the body's centre is in it.</summary>
public static class DashStops
{
    /// <summary>The marker's size, playfield units.</summary>
    public static readonly Vector2 Size = new(110f, 110f);

    /// <summary>Whether the body's centre, at <paramref name="body"/>, is in the marker.</summary>
    public static bool Holds(in DashStop stop, Vector2 body) =>
        MathF.Abs(body.X - stop.Position.X) <= Size.X / 2f && MathF.Abs(body.Y - stop.Position.Y) <= Size.Y / 2f;

    /// <summary>Where a dash <paramref name="way"/> that entered the marker stops: on its centre along the dash's line.</summary>
    public static Vector2 StopAt(in DashStop stop, Vector2 body, Vector2 way) =>
        MathF.Abs(way.X) > MathF.Abs(way.Y) ? body with { X = stop.Position.X } : body with { Y = stop.Position.Y };
}
