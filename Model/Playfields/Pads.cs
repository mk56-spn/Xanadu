using System;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Model.Playfields;

/// <summary>A pad's shape and its launch (<see cref="Pad"/>).</summary>
public static class Pads
{
    /// <summary>A pad's size: a low slab, wider than the body. Playfield units.</summary>
    public static readonly Vector2 Size = new(150f, 28f);

    /// <summary>The upward speed a new pad launches at, units a second: a little over the height of the field's middle under gravity. A placeholder.</summary>
    public const float DefaultSpeed = 1500f;

    /// <summary>How long the pad's spring lasts after it fires, ms.</summary>
    public const double SpringMs = 220;

    /// <summary>How long a timed pad shows a ring closing on it before its time, ms.</summary>
    public const long ApproachMs = 600;

    /// <summary>
    /// How far a timed pad reaches for the body at its time: this far either
    /// side, and from this far above down to standing on it. Farther, it doesn't fire.
    /// </summary>
    public static readonly Vector2 Reach = new(260f, 700f);

    /// <summary>Where the body's centre is when it stands on the pad.</summary>
    public static Vector2 StandingOn(in Pad pad) =>
        new(pad.Position.X, pad.Position.Y - Size.Y / 2f - PlayRules.BodyHeight / 2f);

    /// <summary>Whether a timed pad reaches the body, centred at <paramref name="body"/>, at its time.</summary>
    public static bool Reaches(in Pad pad, Vector2 body)
    {
        Vector2 gap = body - StandingOn(pad);
        return MathF.Abs(gap.X) <= Reach.X && gap.Y >= -Reach.Y && gap.Y <= Size.Y + PlayRules.BodyHeight / 2f;
    }

    /// <summary>Whether the body, centred at <paramref name="body"/>, touches the pad.</summary>
    public static bool Touches(in Pad pad, Vector2 body)
    {
        Vector2 reach = (Size + PlayRules.BodySize) / 2f;
        Vector2 gap = body - pad.Position;
        return MathF.Abs(gap.X) < reach.X && MathF.Abs(gap.Y) < reach.Y;
    }
}
