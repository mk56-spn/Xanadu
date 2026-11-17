namespace Xanadu.Model.Playfields;

/// <summary>
/// A route note as an attractor: the route's point at the song time (where
/// the chart has it, <see cref="RoutePath.At"/>) moves along the track, and the
/// body heads straight for that point. The body is never put on the line or
/// pulled toward the line's nearest point: it keeps its own motion and chases
/// the point, cutting across when it is behind or off to the side. The chase
/// is a critically damped spring on the gap and on the speed difference, so
/// the body catches the point without bouncing. Past the curve's end it lets
/// go and the body glides on.
/// </summary>
public static class CurveAttractor
{
    /// <summary>The spring's rate per second toward the point, critically damped. Lower is looser.</summary>
    public const float Pull = 12f;

    /// <summary>
    /// The acceleration the curve's point puts on a body at a song time, and
    /// where that point is; zero (and not held) once the song is past the curve.
    /// </summary>
    public static Vector2 Accelerate(RoutePath path, double timeMs, Vector2 position, Vector2 velocity, out Vector2 point, out bool held)
    {
        (Vector2 at, Vector2 pointVelocity) = path.At(timeMs);
        point = at;
        held = path.Covers(timeMs);
        if (!held)
            return Vector2.Zero;

        return Pull * Pull * (at - position) + 2f * Pull * (pointVelocity - velocity);
    }
}
