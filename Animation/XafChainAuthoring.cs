using System;
using Godot;

namespace Xanadu.Animation;

/// <summary>Chain-level mutations for the animator tool.</summary>
public static class XafChainAuthoring
{
    /// <summary>
    /// Hangs a new chain with default settings off a bone and returns its index,
    /// or -1 when the bone does not exist. It is named after the bone, and
    /// numbered when that name is taken.
    /// </summary>
    public static int Add(XafPackage package, int bone)
    {
        if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            return -1;

        string boneId = package.Skeleton.Bones[bone].Id;
        package.Chains.Add(new XafChain
        {
            Id = UniqueId(package, $"{boneId}_chain"),
            BoneId = boneId,
        });
        return package.Chains.Count - 1;
    }

    public static bool Delete(XafPackage package, int index)
    {
        if (index < 0 || index >= package.Chains.Count)
            return false;

        package.Chains.RemoveAt(index);
        return true;
    }

    /// <summary>Sets the number of segments, clamped to what a chain allows.</summary>
    public static bool SetSegments(XafPackage package, int index, int segments) =>
        Edit(package, index, chain =>
            chain.Segments = Math.Clamp(segments, XafChain.MinSegments, XafChain.MaxSegments));

    public static bool SetSegmentLength(XafPackage package, int index, float length) =>
        Edit(package, index, chain =>
            chain.SegmentLength = Math.Clamp(
                length, XafChain.MinSegmentLength, XafChain.MaxSegmentLength));

    public static bool SetGravity(XafPackage package, int index, float gravity) =>
        Edit(package, index, chain => chain.Gravity = gravity);

    public static bool SetDamping(XafPackage package, int index, float damping) =>
        Edit(package, index, chain => chain.Damping = Math.Clamp(damping, 0f, 1f));

    public static bool SetWidth(XafPackage package, int index, float width) =>
        Edit(package, index, chain => chain.Width = Math.Max(1f, width));

    public static bool SetLean(XafPackage package, int index, float lean) =>
        Edit(package, index, chain => chain.Lean = lean);

    /// <summary>
    /// Sets the radius of the collider; 0 removes it. A chain that had none gets one
    /// on the bone it hangs from, which is where the first radius is a guess to move.
    /// </summary>
    public static bool SetCollideRadius(XafPackage package, int index, float radius) =>
        Edit(package, index, chain =>
        {
            chain.CollideRadius = Math.Max(0f, radius);
            if (chain.CollideRadius > 0f && chain.CollideBoneId.Length == 0)
                chain.CollideBoneId = chain.BoneId;
        });

    /// <summary>Sets the art; an empty path and group draw the chain as a plain line.</summary>
    public static bool SetArt(XafPackage package, int index, string svgPath, string group) =>
        Edit(package, index, chain =>
        {
            bool bare = string.IsNullOrWhiteSpace(svgPath) || string.IsNullOrWhiteSpace(group);
            chain.SvgPath = bare ? "" : svgPath;
            chain.Group = bare ? "" : group;
        });

    /// <summary>
    /// Hangs the chain from another bone without moving it: its offset is
    /// recomputed so the first point stays where it is in the rest pose.
    /// False for a missing chain or bone.
    /// </summary>
    public static bool SetBone(XafPackage package, int index, int bone)
    {
        if (index < 0 || index >= package.Chains.Count || bone < 0 || bone >= package.Skeleton.Bones.Count)
            return false;

        XafChain chain = package.Chains[index];
        Transform2D[] world = XafRig.RestWorld(package);
        int old = XafRig.FindBone(package, chain.BoneId);
        Vector2 point = old >= 0 ? world[old] * chain.Offset : chain.Offset;
        chain.BoneId = package.Skeleton.Bones[bone].Id;
        chain.Offset = world[bone].AffineInverse() * point;
        return true;
    }

    /// <summary>
    /// Puts the collider on another bone without moving it. A chain that had no
    /// collider gets one of a default size, centred on the bone, so there is
    /// something to see and to move.
    /// </summary>
    public static bool SetColliderBone(XafPackage package, int index, int bone)
    {
        if (index < 0 || index >= package.Chains.Count || bone < 0 || bone >= package.Skeleton.Bones.Count)
            return false;

        XafChain chain = package.Chains[index];
        Transform2D[] world = XafRig.RestWorld(package);
        int old = XafRig.FindBone(package, chain.CollideBoneId);
        Vector2 point = chain.HasCollider && old >= 0 ? world[old] * chain.CollideOffset : world[bone].Origin;
        chain.CollideBoneId = package.Skeleton.Bones[bone].Id;
        chain.CollideOffset = world[bone].AffineInverse() * point;
        if (chain.CollideRadius <= 0f)
            chain.CollideRadius = DefaultColliderRadius;
        return true;
    }

    public static bool SetCollideOffset(XafPackage package, int index, Vector2 offset) =>
        Edit(package, index, chain => chain.CollideOffset = offset);

    public const float DefaultColliderRadius = 8f;

    public static bool SetOffset(XafPackage package, int index, Vector2 offset) =>
        Edit(package, index, chain => chain.Offset = offset);

    private static bool Edit(XafPackage package, int index, Action<XafChain> edit)
    {
        if (index < 0 || index >= package.Chains.Count)
            return false;

        edit(package.Chains[index]);
        return true;
    }

    private static string UniqueId(XafPackage package, string stem)
    {
        string id = stem;
        for (int n = 2; package.Chains.Exists(chain => chain.Id == id); n++)
            id = $"{stem}{n}";
        return id;
    }
}
