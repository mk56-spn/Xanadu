using System;
using Godot;

namespace Xanadu.Animation;

/// <summary>
/// Bend locks, for knees and elbows: a locked bone's joint, the one at its
/// origin, only bends one way. The bend is the turn from the parent's
/// direction (its origin to this joint) to this bone's (this joint to its
/// first child's), in rig space, so it holds whichever way the character
/// faces. Posing respects it: IK always folds a locked joint to its side, and
/// turning a bone past straight stops at straight. Clips are played as keyed.
/// </summary>
public static class XafJointLocks
{
    /// <summary>Below this the joint counts as straight, with no side to lock to.</summary>
    public const float Straight = 0.05f;

    /// <summary>The signed bend at a bone's joint, or false when it has no parent or child to measure from.</summary>
    public static bool TryBend(XafPackage package, Transform2D[] world, int bone, out float bend)
    {
        bend = 0f;
        if (bone < 0 || bone >= package.Skeleton.Bones.Count || bone >= world.Length)
            return false;

        int parent = package.Skeleton.Bones[bone].Parent;
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        if (parent < 0 || child < 0 || child >= world.Length)
            return false;

        Vector2 into = world[bone].Origin - world[parent].Origin;
        Vector2 outOf = world[child].Origin - world[bone].Origin;
        if (into.LengthSquared() < 1e-6f || outOf.LengthSquared() < 1e-6f)
            return false;

        bend = MathF.Atan2(into.Cross(outOf), into.Dot(outOf));
        return true;
    }

    /// <summary>Straightens every locked joint that bends the wrong way; true if any did.</summary>
    public static bool Apply(XafPackage package, XafPose pose)
    {
        Transform2D[] world = XafRig.WorldBones(package, pose);
        bool changed = false;
        for (int i = 0; i < package.Skeleton.Bones.Count && i < pose.Bones.Length; i++)
        {
            int locked = package.Skeleton.Bones[i].Bend;
            if (locked == 0 || !TryBend(package, world, i, out float bend) || bend * locked >= 0f)
                continue;

            // Turning this bone by -bend on screen lines it up with its parent;
            // a mirrored bone turns local rotation the other way. Joints below
            // measure their bend against this bone, so turning it leaves theirs alone.
            pose.Bones[i].Rotation += world[i].Determinant() < 0f ? bend : -bend;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The side a two-bone IK solve must fold the middle joint to, for
    /// <see cref="XafFabrik.Solve"/>, or null when that joint is free. A fold to
    /// the solver's positive side turns the chain the negative way.
    /// </summary>
    public static bool? BendPositive(XafPackage package, int middle) =>
        middle >= 0 && middle < package.Skeleton.Bones.Count && package.Skeleton.Bones[middle].Bend != 0
            ? package.Skeleton.Bones[middle].Bend < 0
            : null;
}
