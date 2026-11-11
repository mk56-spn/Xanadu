using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>Bends skin points from rest into a pose by mixing their bones' turns.</summary>
public static class XafSkinDeform
{
    /// <summary>
    /// Bends the rest points into the pose: each moves with each bone it
    /// follows as that bone moved from rest, mixed by weight. The attachment's
    /// own bone carries it as the attachment itself is posed, so keys on the
    /// attachment still move it.
    ///
    /// The moves are mixed as turns, not as end points (dual quaternion
    /// blending, flat): a point shared by the thigh and the shin swings round
    /// the knee by part of the bend instead of cutting across it, so the back
    /// of a bent knee keeps its volume. A bone turned into the screen first
    /// squashes the points it carries along itself, in the rest pose (see
    /// <see cref="XafRig.Foreshorten"/>). A move that is not a plain turn and
    /// shift, such as a mirrored bone or scaled art, is mixed point by point.
    /// </summary>
    public static Vector2[] Deform(IReadOnlyList<int> influences, float[][] weights, Vector2[] restPoints,
        Transform2D[] restBones, Transform2D[] bones, Transform2D restAttachment, Transform2D attachment,
        Transform2D[]? squash = null)
    {
        var moves = new Transform2D[influences.Count];
        var squashes = new Transform2D[influences.Count];
        var turns = new Transform2D[influences.Count];
        bool rigid = true;
        for (int b = 0; b < influences.Count; b++)
        {
            int bone = influences[b];
            Transform2D squashed = squash is not null && bone < squash.Length ? squash[bone] : Transform2D.Identity;
            moves[b] = b == 0
                ? attachment * restAttachment.AffineInverse()
                : bones[bone] * squashed * restBones[bone].AffineInverse();

            // The move is a squash in the rest pose, then a turn and a shift.
            squashes[b] = restBones[bone] * squashed * restBones[bone].AffineInverse();
            turns[b] = moves[b] * squashes[b].AffineInverse();
            rigid &= IsTurn(turns[b]);
        }

        var points = new Vector2[restPoints.Length];
        for (int p = 0; p < restPoints.Length; p++)
        {
            if (!rigid)
            {
                Vector2 sum = Vector2.Zero;
                for (int b = 0; b < moves.Length; b++)
                {
                    if (weights[p][b] > 0f)
                        sum += moves[b] * restPoints[p] * weights[p][b];
                }
                points[p] = sum;
                continue;
            }

            Vector2 squashedPoint = Vector2.Zero;
            for (int b = 0; b < squashes.Length; b++)
            {
                if (weights[p][b] > 0f)
                    squashedPoint += squashes[b] * restPoints[p] * weights[p][b];
            }
            points[p] = BlendTurns(turns, weights[p]) * squashedPoint;
        }
        return points;
    }

    /// <summary>Whether a transform only turns and shifts: an unscaled, unmirrored rotation.</summary>
    private static bool IsTurn(Transform2D transform) =>
        MathF.Abs(transform.X.Length() - 1f) < 1e-3f && MathF.Abs(transform.Y.Length() - 1f) < 1e-3f &&
        MathF.Abs(transform.X.Dot(transform.Y)) < 1e-3f && transform.Determinant() > 0f;

    /// <summary>
    /// Mixes turns and shifts by weight as dual quaternions about the axis out
    /// of the screen: each is the half turn (cos, sin) and the shift carried
    /// through it, the weighted sum is scaled back to a unit turn, and the
    /// shift is read back out. Halves that point the opposite way to the first
    /// are flipped, so two near turns never mix the long way round.
    /// </summary>
    private static Transform2D BlendTurns(Transform2D[] turns, float[] weights)
    {
        float rc = 0f, rs = 0f, dx = 0f, dy = 0f;
        float firstC = 0f, firstS = 0f;
        bool first = true;
        for (int b = 0; b < turns.Length; b++)
        {
            float w = weights[b];
            if (w <= 0f)
                continue;

            float half = MathF.Atan2(turns[b].X.Y, turns[b].X.X) / 2f;
            float c = MathF.Cos(half), s = MathF.Sin(half);
            if (first)
            {
                firstC = c;
                firstS = s;
                first = false;
            }
            else if (c * firstC + s * firstS < 0f)
            {
                w = -w;
            }

            Vector2 t = turns[b].Origin;
            rc += w * c;
            rs += w * s;
            dx += w * 0.5f * (c * t.X + s * t.Y);
            dy += w * 0.5f * (c * t.Y - s * t.X);
        }

        float length = MathF.Sqrt(rc * rc + rs * rs);
        if (length < 1e-6f)
            return Transform2D.Identity;
        rc /= length;
        rs /= length;
        dx /= length;
        dy /= length;

        var shift = new Vector2(2f * (rc * dx - rs * dy), 2f * (rc * dy + rs * dx));
        float angle = 2f * MathF.Atan2(rs, rc);
        return new Transform2D(angle, shift);
    }
}
