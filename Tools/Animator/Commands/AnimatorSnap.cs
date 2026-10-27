using System.Collections.Generic;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>
/// Lines a dragged joint up with the other joints: its X and Y each jump to a
/// nearby joint's, and the guide lines say which. Bones that move with the
/// dragged one are left out, since aligning to them would chase itself.
/// </summary>
public static class AnimatorSnap
{
    /// <summary>How close, in screen pixels, a joint has to be before it snaps.</summary>
    public const float Pixels = 12f;

    /// <summary>
    /// Snaps <paramref name="wanted"/> (a skeleton-space position for
    /// <paramref name="bone"/>) to the other joints in <paramref name="world"/>.
    /// <paramref name="radius"/> is the snap distance in skeleton units.
    /// </summary>
    public static Vector2 Align(
        XafPackage package,
        Transform2D[] world,
        int bone,
        Vector2 wanted,
        float radius,
        out AnimatorSnapGuides guides) =>
        AlignPoint(wanted, Joints(package, world, bone, keepRoot: false), radius, out guides);

    /// <summary>The origins of every bone outside the subtree that is moving; the subtree's root can stay as a target.</summary>
    public static List<Vector2> Joints(XafPackage package, Transform2D[] world, int movingRoot, bool keepRoot)
    {
        HashSet<int> moving = Subtree(package, movingRoot);
        if (keepRoot)
            moving.Remove(movingRoot);

        var joints = new List<Vector2>();
        for (int i = 0; i < world.Length; i++)
        {
            if (!moving.Contains(i))
                joints.Add(world[i].Origin);
        }

        return joints;
    }

    /// <summary>Snaps a free point's X and Y, each to the nearest joint within the radius.</summary>
    public static Vector2 AlignPoint(
        Vector2 wanted,
        IReadOnlyList<Vector2> joints,
        float radius,
        out AnimatorSnapGuides guides)
    {
        guides = default;
        float bestX = radius;
        float bestY = radius;
        Vector2 snapped = wanted;
        foreach (Vector2 joint in joints)
        {
            float dx = Mathf.Abs(joint.X - wanted.X);
            if (dx < bestX)
            {
                bestX = dx;
                snapped.X = joint.X;
                guides.HasX = true;
                guides.X = joint.X;
            }

            float dy = Mathf.Abs(joint.Y - wanted.Y);
            if (dy < bestY)
            {
                bestY = dy;
                snapped.Y = joint.Y;
                guides.HasY = true;
                guides.Y = joint.Y;
            }
        }

        return snapped;
    }

    /// <summary>
    /// Snaps the direction of a bone that can only turn: its end lies on a
    /// circle of <paramref name="length"/> about <paramref name="pivot"/>, so it
    /// can line up with another joint on one axis, not both. Returns the unit
    /// direction that does, on the side the bone already points to, or the
    /// direction unchanged when nothing is in range.
    /// </summary>
    public static Vector2 Direction(
        Vector2 pivot,
        float length,
        Vector2 direction,
        IReadOnlyList<Vector2> joints,
        float radius,
        out AnimatorSnapGuides guides)
    {
        guides = default;
        if (length < 0.0001f)
            return direction;

        Vector2 end = pivot + direction * length;
        float best = radius;
        Vector2 result = direction;
        foreach (Vector2 joint in joints)
        {
            // Lining up in X: the end's X becomes the joint's, if the circle reaches it.
            float dx = Mathf.Abs(joint.X - end.X);
            float across = joint.X - pivot.X;
            if (dx < best && Mathf.Abs(across) <= length)
            {
                best = dx;
                float along = Mathf.Sqrt(length * length - across * across);
                result = new Vector2(across, end.Y - pivot.Y < 0f ? -along : along) / length;
                guides = new AnimatorSnapGuides { HasX = true, X = joint.X };
            }

            float dy = Mathf.Abs(joint.Y - end.Y);
            float down = joint.Y - pivot.Y;
            if (dy < best && Mathf.Abs(down) <= length)
            {
                best = dy;
                float along = Mathf.Sqrt(length * length - down * down);
                result = new Vector2(end.X - pivot.X < 0f ? -along : along, down) / length;
                guides = new AnimatorSnapGuides { HasY = true, Y = joint.Y };
            }
        }

        return result;
    }

    /// <summary>The bone and everything below it.</summary>
    public static HashSet<int> Subtree(XafPackage package, int bone)
    {
        var set = new HashSet<int> { bone };
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < package.Skeleton.Bones.Count; i++)
            {
                int parent = package.Skeleton.Bones[i].Parent;
                if (parent >= 0 && set.Contains(parent) && set.Add(i))
                    grew = true;
            }
        }

        return set;
    }
}
