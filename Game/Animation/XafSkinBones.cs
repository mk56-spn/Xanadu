using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>How far a skin point lies from a bone, in the terms a limb bends by.</summary>
public static class XafSkinBones
{
    /// <summary>
    /// How far a point is from a bone, mostly along it: how far past either end
    /// of the bone it lies, plus a little of how far to the side. A point beside
    /// the thigh just above the knee then counts as on the thigh, not half on
    /// the shin, so the blend sits on the joint and the inside of a bend does
    /// not pinch.
    /// </summary>
    internal static float Distance(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences) =>
        Distance(package, world, bone, point) + BeforeJoint(package, world, bone, point, influences) +
        OutAlongChild(package, world, bone, point, influences);

    /// <summary>
    /// The other side of <see cref="BeforeJoint"/>: how far a point lies out
    /// along a child that branches off sideways, less how far it lies either
    /// side of the joint along this bone's own line, for the child bending the
    /// same mesh that it reaches furthest along. A shin then does not keep the
    /// toes just because they sit level with the ankle; along a straight chain
    /// the two measures agree and nothing is added.
    /// </summary>
    private static float OutAlongChild(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences)
    {
        float most = 0f;
        for (int i = 0; i < influences.Count; i++)
        {
            int child = influences[i];
            if (package.Skeleton.Bones[child].Parent != bone)
                continue;

            (Vector2 joint, Vector2 end) = Segment(package, world, child);
            Vector2 inward = Inward(package, world, bone, child);
            Vector2 along = end - joint;
            if (inward == Vector2.Zero || along.LengthSquared() < 1e-6f)
                continue;

            // Either side of the joint along this bone's line takes away from
            // it, so the shin's own length above the ankle stays the shin's.
            Vector2 offset = point - joint;
            float beside = MathF.Abs(offset.Dot(inward));
            most = MathF.Max(most, offset.Dot(along.Normalized()) - beside);
        }
        return most;
    }

    /// <summary>
    /// How far a point lies on the parent's side of a bone's joint, measured
    /// along the line from the parent's joint to this one; zero past it, and
    /// zero when the parent does not bend the same mesh. A bone only carries
    /// what lies beyond its joint: a foot, which runs sideways from the ankle,
    /// does not take the bottom of the shin just because it lies beside it.
    /// </summary>
    private static float BeforeJoint(XafPackage package, Transform2D[] world, int bone, Vector2 point,
        IReadOnlyList<int> influences)
    {
        int parent = package.Skeleton.Bones[bone].Parent;
        if (parent < 0 || !Contains(influences, parent))
            return 0f;

        Vector2 joint = world[bone].Origin;
        Vector2 inward = Inward(package, world, parent, bone);
        if (inward == Vector2.Zero)
            return 0f;
        return MathF.Max(0f, (joint - point).Dot(inward));
    }

    /// <summary>
    /// The way a limb runs through the joint where <paramref name="child"/>
    /// meets <paramref name="parent"/>, as a unit vector. Where the child
    /// carries on the parent's own line, as the shin does the thigh's, it is
    /// the parent's way in to the joint. Where it branches off, as a leg does
    /// from the hips while the spine carries on up, the parent's line says
    /// nothing about it, and it is the child's own way out.
    /// </summary>
    private static Vector2 Inward(XafPackage package, Transform2D[] world, int parent, int child)
    {
        int next = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == parent);
        Vector2 way = next == child
            ? world[child].Origin - world[parent].Origin
            : Segment(package, world, child).To - world[child].Origin;
        return way.LengthSquared() < 1e-6f ? Vector2.Zero : way.Normalized();
    }

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }
        return false;
    }

    internal static float Distance(XafPackage package, Transform2D[] world, int bone, Vector2 point)
    {
        (Vector2 from, Vector2 to) = Segment(package, world, bone);
        Vector2 along = to - from;
        float length = along.Length();
        if (length < 1e-4f)
            return from.DistanceTo(point);

        Vector2 axis = along / length;
        float t = (point - from).Dot(axis);
        float past = t < 0f ? -t : t > length ? t - length : 0f;
        float side = MathF.Abs((point - from).Cross(axis)) * SideShare;
        return MathF.Sqrt(past * past + side * side);
    }

    /// <summary>
    /// Where a bone runs, in the space of <paramref name="world"/>: from its
    /// joint to its first child's, or, with no child, to its
    /// <see cref="XafBone.Tip"/>; a bone with neither is a point.
    /// </summary>
    public static (Vector2 From, Vector2 To) Segment(XafPackage package, Transform2D[] world, int bone)
    {
        Vector2 from = world[bone].Origin;
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        if (child >= 0 && child < world.Length)
            return (from, world[child].Origin);
        return (from, world[bone] * package.Skeleton.Bones[bone].Tip);
    }

    /// <summary>How much a point's distance to the side of a bone counts against its distance along it.</summary>
    internal const float SideShare = 0.25f;
}
