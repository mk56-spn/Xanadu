using Godot;

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
                : worldBones[bone] * local;
        }
        return world;
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
