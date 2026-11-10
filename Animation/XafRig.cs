using System;

namespace Xanadu.Animation;

/// <summary>Resolves local sampled poses into skeleton and attachment world transforms.</summary>
public static class XafRig
{
    public static Transform2D[] WorldBones(XafPackage package, XafPose pose)
    {
        var world = new Transform2D[package.Skeleton.Bones.Count];
        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = package.Skeleton.Bones[i];
            Transform2D local = pose.Bones[i].ToTransform();
            if (bone.Parent >= 0)
            {
                // The segment from the parent's joint to this one, seen from the
                // front: shorter the further apart the two joints are in depth.
                local.Origin *= Projection(local.Origin.Length(), pose.Bones[bone.Parent].Depth, pose.Bones[i].Depth);
            }
            world[i] = bone.Parent < 0
                ? local
                : world[bone.Parent] * local;
        }
        return world;
    }

    public static Transform2D[] WorldAttachments(
        XafPackage package,
        XafPose pose,
        Transform2D[] worldBones)
    {
        var world = new Transform2D[package.Attachments.Count];
        for (int i = 0; i < world.Length; i++)
        {
            XafAttachment attachment = package.Attachments[i];
            int bone = FindBone(package, attachment.BoneId);
            Transform2D local = pose.Attachments[i].ToTransform();
            world[i] = bone < 0
                ? local
                : worldBones[bone] * Foreshorten(package, pose, bone) * local;
        }
        return world;
    }

    /// <summary>Where each effect is in rig space; one on a missing bone sits at the rig origin.</summary>
    public static Transform2D[] WorldEffects(
        XafPackage package,
        XafPose pose,
        Transform2D[] worldBones)
    {
        var world = new Transform2D[package.Effects.Count];
        for (int i = 0; i < world.Length; i++)
        {
            XafEffect effect = package.Effects[i];
            int bone = FindBone(package, effect.BoneId);
            Transform2D local = i < pose.Effects.Length
                ? pose.Effects[i].Transform.ToTransform()
                : effect.Transform;
            world[i] = bone < 0 ? local : worldBones[bone] * Foreshorten(package, pose, bone) * local;
        }
        return world;
    }

    /// <summary>The least share of its length a segment keeps, however steeply it points into the screen.</summary>
    public const float MinProjection = 0.05f;

    /// <summary>
    /// How much of a segment <paramref name="length"/> long shows on screen
    /// when its ends sit at depths <paramref name="from"/> and <paramref name="to"/>:
    /// the third side of the right triangle its length and their depth gap make.
    /// </summary>
    public static float Projection(float length, float from, float to)
    {
        float gap = to - from;
        if (gap == 0f || length < 1e-4f)
            return 1f;
        float shown = MathF.Sqrt(MathF.Max(length * length - gap * gap, 0f)) / length;
        return MathF.Max(shown, MinProjection);
    }

    /// <summary>
    /// The squash a bone's own art gets from depth: along its segment to its
    /// first child, by as much as that segment is foreshortened, and not across
    /// it, since a limb's width does not change as it turns into the screen.
    /// A bone with no child has no segment and keeps its shape.
    /// </summary>
    public static Transform2D Foreshorten(XafPackage package, XafPose pose, int bone)
    {
        int child = package.Skeleton.Bones.FindIndex(candidate => candidate.Parent == bone);
        if (child < 0 || bone >= pose.Bones.Length || child >= pose.Bones.Length)
            return Transform2D.Identity;

        Vector2 along = pose.Bones[child].Translation;
        float share = Projection(along.Length(), pose.Bones[bone].Depth, pose.Bones[child].Depth);
        if (share >= 1f)
            return Transform2D.Identity;

        // A scale by the share along the segment's direction, and none across it.
        Vector2 axis = along.Normalized();
        float squash = share - 1f;
        return new Transform2D(
            new Vector2(1f + squash * axis.X * axis.X, squash * axis.X * axis.Y),
            new Vector2(squash * axis.X * axis.Y, 1f + squash * axis.Y * axis.Y),
            Vector2.Zero);
    }

    /// <summary>World transforms of every bone in the rest pose.</summary>
    public static Transform2D[] RestWorld(XafPackage package)
    {
        var world = new Transform2D[package.Skeleton.Bones.Count];
        for (int i = 0; i < world.Length; i++)
        {
            XafBone bone = package.Skeleton.Bones[i];
            world[i] = bone.Parent < 0 ? bone.Rest : world[bone.Parent] * bone.Rest;
        }
        return world;
    }

    public static int FindBone(XafPackage package, string boneId)
    {
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            if (package.Skeleton.Bones[i].Id == boneId)
                return i;
        }
        return -1;
    }
}
