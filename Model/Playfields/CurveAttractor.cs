namespace Xanadu.Model.Playfields;

/// <summary>
/// A curve note as an attractor: the line pulls the hovering body toward its
/// nearest point, and the body keeps its own motion. Across the line the pull
/// is a damped spring, so the body settles onto it without bouncing; along the
/// line the body's speed eases toward the curve's speed at that point, which is
/// how a curve varies momentum along its length. Past the line's last point it
/// lets go and the body glides on.
/// </summary>
public static class CurveAttractor
{
    /// <summary>The spring's rate per second across the line, critically damped. Lower is looser.</summary>
    public const float Pull = 6f;

    /// <summary>How quickly the speed along the line eases to the curve's speed (per second, exponential).</summary>
    public const float AlongRate = 2.5f;

    /// <summary>
    /// The acceleration the line puts on a body, and the nearest point it
    /// pulls toward; zero once the body is past the line's end.
    /// </summary>
    public static Vector2 Accelerate(CurvePath path, Vector2 position, Vector2 velocity, out Vector2 nearest, out bool held)
    {
        (Vector2 at, Vector2 heading, float speed, bool pastEnd) = path.Nearest(position);
        nearest = at;
        held = !pastEnd;
        if (pastEnd || heading == Vector2.Zero)
            return Vector2.Zero;

        float along = Vector2.Dot(velocity, heading);
        Vector2 across = velocity - heading * along;
        Vector2 toLine = at - position;
        toLine -= heading * Vector2.Dot(toLine, heading);

        Vector2 spring = Pull * Pull * toLine - 2f * Pull * across;
        Vector2 push = heading * ((speed - along) * AlongRate);
        return spring + push;
    }
}
