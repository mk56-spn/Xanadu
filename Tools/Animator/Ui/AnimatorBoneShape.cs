using System;
using Godot;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The kite ("rhombus") a bone is drawn as: widest near its parent joint and
/// tapering to the child joint, as in most rigging tools.
/// </summary>
public static class AnimatorBoneShape
{
    /// <summary>How far along the bone the kite is widest, as a fraction of its length.</summary>
    public const float WidestAt = 0.15f;

    public const float HalfWidthRatio = 0.14f;
    public const float MinHalfWidth = 3f;
    public const float MaxHalfWidth = 9f;
    public const float MinLength = 2f;

    /// <summary>
    /// Writes the four corners (parent end, left shoulder, child tip, right
    /// shoulder) into <paramref name="points"/>. False when the bone is too
    /// short to draw.
    /// </summary>
    public static bool Kite(Vector2 from, Vector2 to, Span<Vector2> points)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length < MinLength || points.Length < 4)
            return false;

        Vector2 direction = delta / length;
        Vector2 side = new(-direction.Y, direction.X);
        float half = Math.Clamp(length * HalfWidthRatio, MinHalfWidth, MaxHalfWidth);
        // Very short bones keep the kite from being wider than it is long.
        half = Math.Min(half, length * 0.5f);
        Vector2 shoulder = from + direction * (length * WidestAt);

        points[0] = from;
        points[1] = shoulder + side * half;
        points[2] = to;
        points[3] = shoulder - side * half;
        return true;
    }
}
