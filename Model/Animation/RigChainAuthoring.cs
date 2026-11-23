using System;
using Xanadu.Core.Items;
using Xanadu.Core.Layering;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Animation;

/// <summary>Chain-level mutations for the animator tool.</summary>
public static class RigChainAuthoring
{
    /// <summary>
    /// Hangs a new chain with default settings off a bone and returns its index,
    /// or -1 when the bone does not exist. It is named after the bone, and
    /// numbered when that name is taken.
    /// </summary>
    public static int Add(RigPackage package, int bone)
    {
        if (bone < 0 || bone >= package.Skeleton.Bones.Count)
            return -1;

        string boneId = package.Skeleton.Bones[bone].Id;
        var chain = new RigChain { BoneId = boneId };
        chain.Id = package.UniqueId($"{boneId}_chain");
        chain.Node.Modules.Add(DefaultArt());
        package.Add(chain);
        return package.Chains.IndexOf(chain);
    }

    /// <summary>A new chain's art: a golden rhombus, long side down its Y axis, stretched along each segment.</summary>
    public static XifGeometry DefaultArt() => new()
    {
        Polygons =
        {
            new XifPolygon
            {
                Points = { new Vector2(0, 0), new Vector2(4, 10), new Vector2(0, 20), new Vector2(-4, 10) },
                Fill = new Color("#e8b64c"),
            },
        },
    };

    public static bool Delete(RigPackage package, int index)
    {
        if (index < 0 || index >= package.Chains.Count)
            return false;

        return package.Remove(package.Chains[index].Node);
    }

    /// <summary>Sets the number of segments, clamped to what a chain allows.</summary>
    public static bool SetSegments(RigPackage package, int index, int segments) =>
        Edit(package, index, chain =>
            chain.Segments = Math.Clamp(segments, RigChain.MinSegments, RigChain.MaxSegments));

    public static bool SetSegmentLength(RigPackage package, int index, float length) =>
        Edit(package, index, chain =>
            chain.SegmentLength = Math.Clamp(
                length, RigChain.MinSegmentLength, RigChain.MaxSegmentLength));

    public static bool SetGravity(RigPackage package, int index, float gravity) =>
        Edit(package, index, chain => chain.Gravity = gravity);

    public static bool SetDamping(RigPackage package, int index, float damping) =>
        Edit(package, index, chain => chain.Damping = Math.Clamp(damping, 0f, 1f));

    public static bool SetWidth(RigPackage package, int index, float width) =>
        Edit(package, index, chain => chain.Width = Math.Max(1f, width));

    /// <summary>Sets the chain's own draw layer; it no longer simply draws above the body.</summary>
    public static bool SetLayer(RigPackage package, int index, int layer) =>
        Edit(package, index, chain => chain.Layer = DrawOrder.ClampLayer(layer));

    public static bool SetLean(RigPackage package, int index, float lean) =>
        Edit(package, index, chain => chain.Lean = lean);

    /// <summary>
    /// Sets the radius of the collider; 0 removes it. A chain that had none gets one
    /// on the bone it hangs from, which is where the first radius is a guess to move.
    /// </summary>
    public static bool SetCollideRadius(RigPackage package, int index, float radius) =>
        Edit(package, index, chain =>
        {
            chain.CollideRadius = Math.Max(0f, radius);
            if (chain.CollideRadius > 0f && chain.CollideBoneId.Length == 0)
                chain.CollideBoneId = chain.BoneId;
        });

    /// <summary>
    /// Hangs the chain from another bone without moving it: its offset is
    /// recomputed so the first point stays where it is in the rest pose.
    /// False for a missing chain or bone.
    /// </summary>
    public static bool SetBone(RigPackage package, int index, int bone)
    {
        if (index < 0 || index >= package.Chains.Count || bone < 0 || bone >= package.Skeleton.Bones.Count)
            return false;

        RigChain chain = package.Chains[index];
        Transform2D[] world = RigRig.RestWorld(package);
        int old = RigRig.FindBone(package, chain.BoneId);
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
    public static bool SetColliderBone(RigPackage package, int index, int bone)
    {
        if (index < 0 || index >= package.Chains.Count || bone < 0 || bone >= package.Skeleton.Bones.Count)
            return false;

        RigChain chain = package.Chains[index];
        Transform2D[] world = RigRig.RestWorld(package);
        int old = RigRig.FindBone(package, chain.CollideBoneId);
        Vector2 point = chain.HasCollider && old >= 0 ? world[old] * chain.CollideOffset : world[bone].Origin;
        chain.CollideBoneId = package.Skeleton.Bones[bone].Id;
        chain.CollideOffset = world[bone].AffineInverse() * point;
        if (chain.CollideRadius <= 0f)
            chain.CollideRadius = DefaultColliderRadius;
        return true;
    }

    public static bool SetCollideOffset(RigPackage package, int index, Vector2 offset) =>
        Edit(package, index, chain => chain.CollideOffset = offset);

    public const float DefaultColliderRadius = 8f;

    public static bool SetOffset(RigPackage package, int index, Vector2 offset) =>
        Edit(package, index, chain => chain.Offset = offset);

    private static bool Edit(RigPackage package, int index, Action<RigChain> edit)
    {
        if (index < 0 || index >= package.Chains.Count)
            return false;

        edit(package.Chains[index]);
        return true;
    }
}
