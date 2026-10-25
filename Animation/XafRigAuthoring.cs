using System;
using Godot;

namespace Writer.Animation;

/// <summary>Skeleton-level mutations for the animator tool.</summary>
public static class XafRigAuthoring
{
    public static XafPackage CreateEmptyPackage()
    {
        var package = new XafPackage();
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = "root",
            Parent = -1,
            Rest = Transform2D.Identity,
        });
        foreach (string id in new[] { "idle", "jumping", "landing", "death" })
        {
            package.Clips.Add(new XafClip
            {
                Id = id,
                Duration = 2f,
            });
        }
        return package;
    }

    public static int AddBone(
        XafPackage package,
        int parent,
        string id,
        Vector2 localOffset)
    {
        if (parent < 0 || parent >= package.Skeleton.Bones.Count)
            return -1;
        if (string.IsNullOrWhiteSpace(id))
            return -1;
        if (package.Skeleton.Bones.Exists(bone => bone.Id == id))
            return -1;

        var bone = new XafBone
        {
            Id = id,
            Parent = parent,
            Rest = new Transform2D(
                0f,
                Vector2.One,
                0f,
                localOffset),
        };
        package.Skeleton.Bones.Add(bone);
        return package.Skeleton.Bones.Count - 1;
    }

    public static bool DeleteBone(XafPackage package, int index)
    {
        if (index < 0 || index >= package.Skeleton.Bones.Count)
            return false;

        XafBone bone = package.Skeleton.Bones[index];
        if (bone.Parent < 0)
            return false;

        if (package.Skeleton.Bones.Exists(other => other.Parent == index))
            return false;

        if (package.Attachments.Exists(attachment =>
                attachment.BoneId == bone.Id))
        {
            return false;
        }

        package.Skeleton.Bones.RemoveAt(index);
        foreach (XafBone other in package.Skeleton.Bones)
        {
            if (other.Parent > index)
                other.Parent--;
        }
        return true;
    }
}
