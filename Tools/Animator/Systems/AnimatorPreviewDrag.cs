using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Moves what a preview drag grabbed: a bone joint or attachment, a swinging
/// parent, a two-bone IK chain, a rotate handle, a chain or target handle.
/// </summary>
internal static class AnimatorPreviewDrag
{
    public static void Drag(Entity session, Vector2 pointer, Rect2 area, bool shift)
    {
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);

        if (AnimatorChainHandles.IsChainDrag(drag.Kind))
        {
            AnimatorChainHandles.Drag(session, pointer, placement);
            return;
        }

        if (AnimatorTargetCommands.IsDrag(drag.Kind))
        {
            AnimatorTargetCommands.Drag(session, pointer, placement, shift);
            return;
        }

        if (drag.Kind == AnimatorDragKind.Swing)
        {
            Swing(session, pointer, placement, shift);
            return;
        }

        if (drag.Kind == AnimatorDragKind.Ik)
        {
            AnimatorPreviewIk.Pose(session, pointer, placement, shift);
            return;
        }

        if (drag.Kind == AnimatorDragKind.Rotate)
        {
            AnimatorTipHandles.Drag(session, pointer, placement, shift);
            return;
        }

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool rest = session.GetComponent<AnimatorModes>().Rest;

        // The pointer moves in screen space; the value lives in the parent's space.
        Vector2 localNow = placement.AffineInverse() * pointer;
        Vector2 localStart = placement.AffineInverse() * drag.StartPointer;
        Vector2 value = drag.StartValue +
                        drag.ParentWorld.BasisXformInv(localNow - localStart);

        if (drag.Kind == AnimatorDragKind.Bone && selection.Bone >= 0)
        {
            value = AlignToOtherBones(session, drag, selection.Bone, value, placement, shift);
            if (rest)
            {
                XafBone bone = document.Package.Skeleton.Bones[selection.Bone];
                bone.Rest = new Transform2D(bone.Rest.Rotation, bone.Rest.Scale, 0f, value);
                AnimatorRestPin.Restore(session, drag.Pinned);
            }
            else
            {
                document.Runtime.Pose.Bones[selection.Bone].Translation = value;
            }
        }
        else if (drag.Kind == AnimatorDragKind.Attachment && selection.Attachment >= 0)
        {
            if (!shift && AnimatorPreviewGrid.On(session))
                value = drag.ParentWorld.AffineInverse() * AnimatorSnap.ToGrid(drag.ParentWorld * value, default);

            if (rest)
            {
                XafAttachment attachment = document.Package.Attachments[selection.Attachment];
                attachment.Transform = new Transform2D(
                    attachment.Transform.Rotation,
                    attachment.Transform.Scale,
                    0f,
                    value);
            }
            else
            {
                document.Runtime.Pose.Attachments[selection.Attachment].Translation = value;
            }
        }
    }

    public static void End(Entity session)
    {
        session.GetComponent<AnimatorSnapGuides>() = default;
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        bool keys = !AnimatorChainHandles.IsChainDrag(drag.Kind) && !AnimatorTargetCommands.IsDrag(drag.Kind) &&
                    drag.Kind is not (AnimatorDragKind.None or AnimatorDragKind.PaintWeights) && !modes.Rest;

        // The drag and any key it leaves are one undo step.
        AnimatorHistoryCommands.EndGesture(session, keys ? () => KeyDrag(session, drag, modes) : null);
    }

    private static void Swing(Entity session, Vector2 pointer, Transform2D placement, bool free)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        int joint = session.GetComponent<AnimatorDrag>().Joint;
        int parent = document.Package.Skeleton.Bones[joint].Parent;
        XafPose pose = document.Runtime.Pose;
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);

        Vector2 local = placement.AffineInverse() * pointer;
        Vector2 toPointer = local - world[parent].Origin;
        Vector2 toJoint = world[joint].Origin - world[parent].Origin;
        ref AnimatorSnapGuides guides = ref session.GetComponent<AnimatorSnapGuides>();
        guides = default;
        if (toPointer.LengthSquared() < 1f || toJoint.LengthSquared() < 0.0001f)
            return;

        // The joint can only move on a circle about its parent, so it snaps by turning to line up
        // with another joint. The parent itself is a target (a straight bone); what it carries is not.
        if (!free)
        {
            float radius = AnimatorSnap.Pixels / Mathf.Max(0.0001f, Mathf.Abs(placement.Scale.X));
            Vector2 snapped = AnimatorSnap.Direction(
                world[parent].Origin,
                toJoint.Length(),
                toPointer.Normalized(),
                AnimatorSnap.Joints(document.Package, world, parent, keepRoot: true),
                radius,
                out guides);
            toPointer = snapped * toPointer.Length();
        }

        float delta = Mathf.Wrap(toPointer.Angle() - toJoint.Angle(), -Mathf.Pi, Mathf.Pi);
        // A mirrored parent turns local rotation the opposite way on screen.
        if (world[parent].Determinant() < 0f)
            delta = -delta;
        pose.Bones[parent].Rotation += delta;
        XafJointLocks.Apply(document.Package, pose);
    }

    /// <summary>
    /// Snaps the dragged joint's X and Y to nearby joints of other bones and
    /// publishes the guides to draw. Holding Shift drags freely.
    /// </summary>
    private static Vector2 AlignToOtherBones(
        Entity session,
        AnimatorDrag drag,
        int bone,
        Vector2 value,
        Transform2D placement,
        bool free)
    {
        ref AnimatorSnapGuides guides = ref session.GetComponent<AnimatorSnapGuides>();
        guides = default;
        if (free)
            return value;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        Transform2D[] world = XafRig.WorldBones(
            document.Package, AnimatorGeometry.PreviewPose(document, modes));
        float radius = AnimatorSnap.Pixels / Mathf.Max(0.0001f, Mathf.Abs(placement.Scale.X));
        Vector2 wanted = drag.ParentWorld * value;
        Vector2 snapped = AnimatorSnap.Align(
            document.Package, world, bone, wanted, radius, out AnimatorSnapGuides found);
        guides = found;
        if (AnimatorPreviewGrid.On(session))
            snapped = AnimatorSnap.ToGrid(snapped, found);
        return drag.ParentWorld.AffineInverse() * snapped;
    }


    private static void KeyDrag(Entity session, AnimatorDrag drag, AnimatorModes modes)
    {
        // An IK drag turned the parent and grandparent, so those are what get
        // keyed; selected keys on them take the change instead.
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (drag.Kind == AnimatorDragKind.Ik &&
            AnimatorPreviewChain.TryGet(package, drag.Joint, out int parent, out int grandparent))
        {
            int edited = 0;
            AnimatorHistoryCommands.Edit(session, () => edited = AnimatorKeyEditCommands.Apply(session));
            if (edited == 0 && modes.AutoKey)
                AnimatorEditing.KeyBones(session, grandparent, parent);
            return;
        }

        AnimatorEditing.KeyEdit(session);

        // IK was turned off partway through: the chain it bent before then still needs its keys.
        if (drag.UsedIk && modes.AutoKey &&
            AnimatorPreviewChain.TryGet(package, drag.Grabbed, out int bentParent, out int bentGrandparent))
            AnimatorEditing.KeyBones(session, bentGrandparent, bentParent);
    }
}
