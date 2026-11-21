using System;
using Xanadu.Core.Maths;

namespace Xanadu.Core.Tweens;

/// <summary>
/// What a target's movement tweens add up to at one moment: every live offset
/// and rotation summed, every live scale and opacity multiplied. Applied after the target is
/// placed (laid out, positioned by its owner), never written back into that place.
/// </summary>
public readonly record struct TweenPose(Vector2 Offset, float Rotation, Vector2 Scale, float Opacity = 1f)
{
    public static TweenPose Identity => new(Vector2.Zero, 0f, Vector2.One);

    /// <summary>Whether it changes nothing: no move and full opacity.</summary>
    public bool IsIdentity => !Moves && Opacity == 1f;

    /// <summary>Whether it moves, turns or scales the target.</summary>
    public bool Moves => Offset != Vector2.Zero || Rotation != 0f || Scale != Vector2.One;

    /// <summary>
    /// The pose as a transform in the target's own space: scaled and turned about
    /// <paramref name="pivot"/>, then slid by the offset.
    /// </summary>
    public Transform2D Around(Vector2 pivot)
    {
        if (!Moves)
            return Transform2D.Identity;

        float cos = MathF.Cos(Rotation), sin = MathF.Sin(Rotation);
        var x = new Vector2(cos, sin) * Scale.X;
        var y = new Vector2(-sin, cos) * Scale.Y;
        // p' = pivot + offset + B (p - pivot)
        Vector2 origin = pivot + Offset - (x * pivot.X + y * pivot.Y);
        return new Transform2D(x, y, origin);
    }
}
