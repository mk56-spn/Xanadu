using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Non-FK rest-pose editing. Normally moving a bone drags everything below it
/// along, because each child's rest is relative to its parent. With PIN on, a
/// rest edit leaves the bones directly under the edited ones where they were
/// on screen: their own rest transforms are re-worked to cancel the change, and
/// anything further down follows them, so the whole rest of the chain stays put.
/// </summary>
public static class AnimatorRestPin
{
    /// <summary>The children to hold still and where they were in rest-pose space.</summary>
    public sealed record Pinned(List<(int Child, Transform2D World)> Children);

    /// <summary>Whether edits should pin: only the rest pose, and only with PIN on.</summary>
    public static bool IsOn(Entity session)
    {
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        return modes.Rest && modes.Pin;
    }

    /// <summary>
    /// Records where the children of <paramref name="edited"/> are now. Null
    /// when pinning is off. Bones that are edited themselves are not pinned,
    /// since moving them was the point.
    /// </summary>
    public static Pinned? Capture(Entity session, IEnumerable<int> editedBones)
    {
        if (!IsOn(session))
            return null;

        var edited = new HashSet<int>(editedBones);
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        Transform2D[] world = RestWorld(package);
        var children = new List<(int, Transform2D)>();
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            int parent = package.Skeleton.Bones[i].Parent;
            if (parent >= 0 && edited.Contains(parent) && !edited.Contains(i))
                children.Add((i, world[i]));
        }

        return children.Count == 0 ? null : new Pinned(children);
    }

    /// <summary>Puts the pinned children back where they were, whatever happened to their parents.</summary>
    public static void Restore(Entity session, Pinned? pinned)
    {
        if (pinned is null)
            return;

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        Transform2D[] world = RestWorld(package);
        foreach ((int child, Transform2D wanted) in pinned.Children)
        {
            XafBone bone = package.Skeleton.Bones[child];
            Transform2D parentWorld = bone.Parent >= 0 ? world[bone.Parent] : Transform2D.Identity;
            Transform2D local = parentWorld.AffineInverse() * wanted;
            bone.Rest = new Transform2D(local.Rotation, local.Scale, 0f, local.Origin);

            // Later siblings' parents are unaffected, but a pinned child can be the parent of another.
            world[child] = parentWorld * bone.Rest;
        }
    }

    /// <summary>Every bone's rest transform in skeleton space.</summary>
    public static Transform2D[] RestWorld(XafPackage package)
    {
        var pose = new XafPose(package.Skeleton.Bones.Count, 0);
        for (int i = 0; i < pose.Bones.Length; i++)
            pose.Bones[i] = XafTransform.From(package.Skeleton.Bones[i].Rest);
        return XafRig.WorldBones(package, pose);
    }
}
