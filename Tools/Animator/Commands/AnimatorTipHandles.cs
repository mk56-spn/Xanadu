using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// The rotation handle on each bone with no children. Any other bone turns by
/// dragging a child's joint, but a childless one has nothing to grab, so it gets
/// a short stem with a dot on the end. The stem turns with the bone and points
/// along its tip, if it has one, or continues the limb while its rotation is zero.
/// </summary>
public static class AnimatorTipHandles
{
    /// <summary>Length of the stem in screen pixels, whatever the zoom; clears the joint's own hit radius.</summary>
    public const float StemLength = 32f;

    /// <summary>How close the pointer has to be to grab the dot, in screen pixels.</summary>
    public const float HitRadius = 8f;

    /// <summary>The step a drag snaps the bone's rotation to unless Shift is held.</summary>
    public const float SnapStep = Mathf.Pi / 12f;

    /// <summary>Closer to the joint than this, in screen pixels, the pointer's angle is only noise.</summary>
    private const float DeadZone = 4f;

    /// <summary>The handles are bone tools: they follow the joint dots and give way to SVG placement.</summary>
    public static bool Shown(in AnimatorModes modes) => modes.Handles && !modes.Svg;

    /// <summary>Which bones have no children, and so carry a handle.</summary>
    public static bool[] Childless(XafPackage package)
    {
        var childless = new bool[package.Skeleton.Bones.Count];
        System.Array.Fill(childless, true);
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            if (bone.Parent >= 0 && bone.Parent < childless.Length)
                childless[bone.Parent] = false;
        }

        return childless;
    }

    /// <summary>Screen position of the dot on a bone's stem.</summary>
    public static Vector2 Tip(XafPackage package, int bone, Transform2D[] world, Transform2D placement)
    {
        // Along the bone's own tip when it has one, else on down the limb.
        XafBone owner = package.Skeleton.Bones[bone];
        Vector2 limb = owner.Tip.LengthSquared() > 0.0001f ? owner.Tip : owner.Rest.Origin;
        Vector2 local = limb.LengthSquared() > 0.0001f ? limb : Vector2.Right;
        Vector2 direction = placement.BasisXform(world[bone].BasisXform(local));
        if (direction.LengthSquared() < 0.0001f)
            direction = Vector2.Right;

        return placement * world[bone].Origin + direction.Normalized() * StemLength;
    }

    /// <summary>
    /// The bone whose dot is under the pointer, or -1. A joint nearer the pointer
    /// than the dot wins, so a crowded, zoomed-out rig keeps its joints reachable.
    /// </summary>
    public static int Pick(XafPackage package, Transform2D[] world, Transform2D placement, Vector2 pointer)
    {
        bool[] childless = Childless(package);
        int hit = -1;
        float best = HitRadius;
        for (int i = 0; i < childless.Length && i < world.Length; i++)
        {
            if (!childless[i])
                continue;

            float distance = Tip(package, i, world, placement).DistanceTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            hit = i;
        }

        return hit >= 0 && AnimatorGeometry.PickNearest(world, placement, pointer, best) < 0 ? hit : -1;
    }

    /// <summary>Starts turning the bone whose dot is under the pointer. False when none is hit.</summary>
    public static bool TryBegin(
        Entity session,
        Vector2 pointer,
        Transform2D placement,
        XafPose pose,
        Transform2D[] world)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        if (!Shown(session.GetComponent<AnimatorModes>()))
            return false;

        int bone = Pick(document.Package, world, placement, pointer);
        if (bone < 0)
            return false;

        document.Runtime.SetPaused(true);
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.Attachment = -1;
        selection.Effect = -1;
        selection.SelectOnlyBone(bone);
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = AnimatorDragKind.Rotate,
            StartPointer = pointer,
            Joint = bone,
            StartRotation = pose.Bones[bone].Rotation,
            PointerAngle = PointerAngle(pointer, placement, world[bone].Origin),
        };
        AnimatorSession.SetStatus(session, $"ROTATING {document.Package.Skeleton.Bones[bone].Id}");
        return true;
    }

    /// <summary>
    /// Turns the bone by however far the pointer has gone around its joint since
    /// the grab. The turn accumulates, so going past half a circle keeps counting
    /// instead of flipping to the other side, which would spin between keys.
    /// </summary>
    public static void Drag(Entity session, Vector2 pointer, Transform2D placement, bool free)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        bool rest = session.GetComponent<AnimatorModes>().Rest;
        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        XafPackage package = document.Package;
        int index = drag.Joint;
        XafBone bone = package.Skeleton.Bones[index];
        Transform2D[] world = XafRig.WorldBones(
            package, AnimatorGeometry.PreviewPose(document, session.GetComponent<AnimatorModes>()));
        Vector2 joint = world[index].Origin;
        if ((placement * joint).DistanceTo(pointer) < DeadZone)
            return;

        float angle = PointerAngle(pointer, placement, joint);
        drag.Turned += Mathf.Wrap(angle - drag.PointerAngle, -Mathf.Pi, Mathf.Pi);
        drag.PointerAngle = angle;

        // A mirrored parent turns local rotation the opposite way on screen.
        bool mirrored = bone.Parent >= 0 && world[bone.Parent].Determinant() < 0f;
        float rotation = drag.StartRotation + (mirrored ? -drag.Turned : drag.Turned);

        // Grabbing without moving leaves the bone alone; only a real turn snaps.
        if (!free && drag.Turned != 0f)
            rotation = Mathf.Snapped(rotation, SnapStep);

        if (rest)
        {
            // No children means nothing for PIN to hold still.
            bone.Rest = new Transform2D(rotation, bone.Rest.Scale, 0f, bone.Rest.Origin);
            return;
        }

        document.Runtime.Pose.Bones[index].Rotation = rotation;
        XafJointLocks.Apply(package, document.Runtime.Pose);
    }

    private static float PointerAngle(Vector2 pointer, Transform2D placement, Vector2 joint) =>
        (placement.AffineInverse() * pointer - joint).Angle();
}
