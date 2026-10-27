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
    public const float Pixels = 8f;

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
        out AnimatorSnapGuides guides)
    {
        guides = default;
        HashSet<int> moving = Subtree(package, bone);
        float bestX = radius;
        float bestY = radius;
        Vector2 snapped = wanted;
        for (int i = 0; i < world.Length; i++)
        {
            if (moving.Contains(i))
                continue;

            Vector2 joint = world[i].Origin;
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
