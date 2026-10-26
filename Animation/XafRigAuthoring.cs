using System;
using System.Collections.Generic;
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
        foreach (XafClip clip in package.Clips)
        {
            clip.Channels.RemoveAll(channel =>
                channel.Target == XafChannelTarget.Bone &&
                channel.TargetId == bone.Id);
        }

        foreach (XafBone other in package.Skeleton.Bones)
        {
            if (other.Parent > index)
                other.Parent--;
        }
        return true;
    }

    /// <summary>
    /// Renames a bone and every attachment and clip channel that refers to it.
    /// False when the name is empty, unchanged or used by another bone.
    /// </summary>
    public static bool RenameBone(XafPackage package, int index, string id)
    {
        if (index < 0 || index >= package.Skeleton.Bones.Count)
            return false;
        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (package.Skeleton.Bones.Exists(bone => bone.Id == id))
            return false;

        XafBone renamed = package.Skeleton.Bones[index];
        string old = renamed.Id;
        renamed.Id = id;
        foreach (XafAttachment attachment in package.Attachments)
        {
            if (attachment.BoneId == old)
                attachment.BoneId = id;
        }

        foreach (XafClip clip in package.Clips)
        {
            foreach (XafChannel channel in clip.Channels)
            {
                if (channel.Target == XafChannelTarget.Bone && channel.TargetId == old)
                    channel.TargetId = id;
            }
        }
        return true;
    }

    /// <summary>
    /// Moves a bone under a new parent, keeping its rest pose where it is on
    /// screen. Bones are reordered so parents still come before children.
    /// False for the root, a no-op, or a parent inside the bone's own subtree.
    /// </summary>
    public static bool ReparentBone(XafPackage package, int index, int parent)
    {
        List<XafBone> bones = package.Skeleton.Bones;
        if (index < 0 || index >= bones.Count || parent < 0 || parent >= bones.Count)
            return false;
        if (bones[index].Parent < 0 || bones[index].Parent == parent)
            return false;
        if (IsInSubtree(bones, parent, index))
            return false;

        Transform2D[] world = RestWorld(bones);
        XafBone bone = bones[index];
        bone.Rest = world[parent].AffineInverse() * world[index];
        bone.Parent = parent;
        SortParentsFirst(bones);
        return true;
    }

    /// <summary>Whether <paramref name="bone"/> is <paramref name="ancestor"/> or below it.</summary>
    private static bool IsInSubtree(
        List<XafBone> bones,
        int bone,
        int ancestor)
    {
        for (int i = bone; i >= 0; i = bones[i].Parent)
        {
            if (i == ancestor)
                return true;
        }
        return false;
    }

    private static Transform2D[] RestWorld(List<XafBone> bones)
    {
        var world = new Transform2D[bones.Count];
        for (int i = 0; i < bones.Count; i++)
        {
            XafBone bone = bones[i];
            world[i] = bone.Parent < 0 ? bone.Rest : world[bone.Parent] * bone.Rest;
        }
        return world;
    }

    /// <summary>
    /// Stable reorder so every parent precedes its children, which world
    /// transforms rely on; parent indices are remapped to match.
    /// </summary>
    private static void SortParentsFirst(List<XafBone> bones)
    {
        var order = new List<int>(bones.Count);
        var placed = new int[bones.Count];
        Array.Fill(placed, -1);
        while (order.Count < bones.Count)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                if (placed[i] >= 0)
                    continue;
                int parent = bones[i].Parent;
                if (parent >= 0 && placed[parent] < 0)
                    continue;
                placed[i] = order.Count;
                order.Add(i);
            }
        }

        var sorted = new List<XafBone>(bones.Count);
        foreach (int old in order)
        {
            XafBone bone = bones[old];
            if (bone.Parent >= 0)
                bone.Parent = placed[bone.Parent];
            sorted.Add(bone);
        }
        bones.Clear();
        bones.AddRange(sorted);
    }
}
