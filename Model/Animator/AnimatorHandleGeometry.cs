using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>Where the preview's handles sit: chain root and collider, bone tip stems and target radii. Drawing and dragging both ask here.</summary>
public static class AnimatorHandleGeometry
{
    /// <summary>
    /// The selected chain's handles in rig space as drawn now: the first point,
    /// and the collider centre when it has one. False without a selected chain.
    /// </summary>
    public static bool ChainHandles(
        RigPackage package,
        int chain,
        Transform2D[] world,
        out Vector2 root,
        out Vector2? collider)
    {
        root = Vector2.Zero;
        collider = null;
        if (chain < 0 || chain >= package.Chains.Count)
            return false;

        RigChain selected = package.Chains[chain];
        Vector2? anchor = RigChainSimulation.Anchor(package, selected, world);
        if (anchor is null)
            return false;

        root = anchor.Value;
        collider = RigChainSimulation.Collider(package, selected, world);
        return true;
    }

    /// <summary>Length of the stem in screen pixels, whatever the zoom; clears the joint's own hit radius.</summary>
    public const float TipStemLength = 32f;

    /// <summary>The handles are bone tools: they follow the joint dots and give way to part placement.</summary>
    public static bool TipsShown(in AnimatorModes modes) => modes.Handles && !modes.Parts;

    /// <summary>Which bones have no children, and so carry a handle.</summary>
    public static bool[] Childless(RigPackage package)
    {
        var childless = new bool[package.Skeleton.Bones.Count];
        System.Array.Fill(childless, true);
        foreach (RigBone bone in package.Skeleton.Bones)
        {
            if (bone.Parent >= 0 && bone.Parent < childless.Length)
                childless[bone.Parent] = false;
        }

        return childless;
    }

    /// <summary>Screen position of the dot on a bone's stem.</summary>
    public static Vector2 Tip(RigPackage package, int bone, Transform2D[] world, Transform2D placement)
    {
        // Along the bone's own tip when it has one, else on down the limb.
        RigBone owner = package.Skeleton.Bones[bone];
        Vector2 limb = owner.Tip.LengthSquared() > 0.0001f ? owner.Tip : owner.Rest.Origin;
        Vector2 local = limb.LengthSquared() > 0.0001f ? limb : Vector2.Right;
        Vector2 direction = placement.BasisXform(world[bone].BasisXform(local));
        if (direction.LengthSquared() < 0.0001f)
            direction = Vector2.Right;

        return placement * world[bone].Origin + direction.Normalized() * TipStemLength;
    }

    /// <summary>The selected target's ellipse in rig space now, for drawing and grabbing.</summary>
    public static Transform2D TargetAnchorWorld(RigPackage package, RigLimbTarget target, Transform2D[] world)
    {
        int anchor = target.AnchorId.Length > 0 ? RigRig.FindBone(package, target.AnchorId) : -1;
        return anchor >= 0 ? world[anchor] : Transform2D.Identity;
    }

    public static Vector2 TargetRadiusXHandle(RigLimbTarget target) =>
        target.Center + new Vector2(target.RadiusX, 0f).Rotated(target.Tilt);

    public static Vector2 TargetRadiusYHandle(RigLimbTarget target) =>
        target.Center + new Vector2(0f, target.RadiusY).Rotated(target.Tilt);
}
