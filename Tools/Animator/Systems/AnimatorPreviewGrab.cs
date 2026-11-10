using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Decides what a press in the preview grabs: a pending pick, the weight
/// brush, a target or chain handle, a bone joint, or an SVG attachment.
/// </summary>
internal static class AnimatorPreviewGrab
{
    public static void Begin(Entity session, Vector2 pointer, Rect2 area, bool shift, bool ctrl = false)
    {
        if (!area.HasPoint(pointer))
            return;

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafPackage package = document.Package;
        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = XafRig.WorldBones(package, pose);
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);
        string suffix = modes.Rest ? " (REST)" : "";

        // A reparent from the bone menu takes the next click as its new parent.
        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        if (menu.Reparenting >= 0)
        {
            int child = menu.Reparenting;
            menu.Reparenting = -1;
            int target = AnimatorGeometry.PickBoneOrLabel(world, placement, pointer);
            if (target >= 0)
                AnimatorEditing.ReparentBone(session, child, target);
            else
                AnimatorSession.SetStatus(session, "REPARENT CANCELLED");
            return;
        }

        // A chain inspector button waits for a bone: the click gives it one, or cancels.
        if (session.GetComponent<AnimatorChains>().Picking != AnimatorChainPick.None)
        {
            int picked = AnimatorGeometry.PickBoneOrLabel(world, placement, pointer);
            if (picked >= 0)
                AnimatorChainPlacementCommands.PickBone(session, picked);
            else
                AnimatorChainPlacementCommands.CancelPick(session);
            return;
        }

        // The BONES button waits for clicks: each toggles a bone bending the mesh, nothing finishes.
        if (session.GetComponent<AnimatorMeshPick>().Active)
        {
            AnimatorArtCommands.PickBone(session, AnimatorGeometry.PickBoneOrLabel(world, placement, pointer));
            return;
        }

        // Painting weights: a joint chooses the bone to paint; anywhere else starts a stroke.
        if (session.GetComponent<AnimatorWeightPaint>().Active)
        {
            int joint = AnimatorGeometry.PickBoneOrLabel(world, placement, pointer);
            if (joint >= 0)
            {
                AnimatorWeightCommands.PickBone(session, joint);
                return;
            }
            AnimatorHistoryCommands.BeginGesture(session);
            session.GetComponent<AnimatorDrag>() = new AnimatorDrag
            {
                Kind = AnimatorDragKind.PaintWeights,
                StartPointer = pointer,
            };
            AnimatorWeightCommands.Stroke(session, pointer, placement, ctrl);
            return;
        }

        // A cloth inspector button waits for clicks: they pin points or pick bones until stopped.
        if (session.GetComponent<AnimatorCloths>().Picking != AnimatorClothPick.None)
        {
            AnimatorClothCommands.Click(session, pointer, placement, world);
            return;
        }

        // So are the selected target's handles.
        if (!modes.Rest && AnimatorTargetCommands.TryBegin(session, pointer, placement, world))
            return;

        // The selected chain's root and collider are handles that win over the bones under them.
        if (AnimatorChainHandles.TryBegin(session, pointer, placement, world))
            return;

        if (AnimatorTipHandles.TryBegin(session, pointer, placement, pose, world))
            return;

        // Bone movement and SVG placement are separate modes, so a click only
        // ever considers one kind of target.
        if (!modes.Svg)
        {
            int bone = AnimatorGeometry.PickNearest(
                world, placement, pointer, AnimatorGeometry.BoneHitRadius);
            if (bone >= 0 && shift)
            {
                // Shift-click groups bones; a fixed-length bone is picked as
                // the parent that actually turns, as a normal click does.
                int target = modes.Rest || package.Skeleton.Bones[bone].CanTranslate
                    ? bone
                    : package.Skeleton.Bones[bone].Parent;
                if (target >= 0)
                {
                    ToggleBone(session, target);
                    return;
                }
            }

            // Ctrl slides the joint itself off its rest spot: a shrug, a jab's
            // reach, hips swaying in a walk.
            if (bone >= 0 && ctrl && !modes.Rest && package.Skeleton.Bones[bone].Parent >= 0)
            {
                BeginOffset(session, pointer, bone, pose, world);
                return;
            }

            if (bone >= 0)
            {
                AnimatorHistoryCommands.BeginGesture(session);
                StartJoint(session, pointer, bone, modes, pose, world);
            }
            return;
        }

        Transform2D[] attachments = XafRig.WorldAttachments(package, pose, world);
        int attachment = AnimatorGeometry.PickNearest(
            attachments, placement, pointer, AnimatorGeometry.AttachmentHitRadius);
        if (attachment < 0)
            return;

        selection.SelectOnlyAttachment(attachment);
        document.Runtime.SetPaused(true);
        AnimatorHistoryCommands.BeginGesture(session);
        XafAttachment owner = package.Attachments[attachment];
        int parent = XafRig.FindBone(package, owner.BoneId);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = AnimatorDragKind.Attachment,
            StartPointer = pointer,
            StartValue = modes.Rest ? owner.Transform.Origin : pose.Attachments[attachment].Translation,
            ParentWorld = parent >= 0 ? world[parent] : Transform2D.Identity,
        };
        AnimatorSession.SetStatus(session, $"SELECTED {owner.Group}{suffix}");
    }

    /// <summary>Adds or removes one bone from the group and says how many are selected.</summary>
    private static void ToggleBone(Entity session, int bone)
    {
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.ToggleBone(bone);
        int count = selection.Bones().Count;
        string id = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones[bone].Id;
        AnimatorSession.SetStatus(
            session,
            count > 1 ? $"{id} ADDED • {count} BONES SELECTED" : $"SELECTED {id}");
    }

    /// <summary>
    /// Starts sliding a joint off its rest spot. A fixed-length bone is made
    /// movable first, inside the same undo step, so the offset can be keyed.
    /// </summary>
    private static void BeginOffset(Entity session, Vector2 pointer, int bone, XafPose pose, Transform2D[] world)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafBone rig = document.Package.Skeleton.Bones[bone];
        document.Runtime.SetPaused(true);
        selection.Attachment = -1;
        selection.Effect = -1;
        AnimatorHistoryCommands.BeginGesture(session);

        bool unlocked = !rig.AllowTranslation;
        rig.AllowTranslation = true;
        selection.SelectOnlyBone(bone);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = AnimatorDragKind.Bone,
            StartPointer = pointer,
            StartValue = pose.Bones[bone].Translation,
            ParentWorld = world[rig.Parent],
        };
        AnimatorSession.SetStatus(session, unlocked
            ? $"OFFSETTING {rig.Id}: ITS JOINT NOW SLIDES (MOVE ON)"
            : $"OFFSETTING {rig.Id}");
    }

    /// <summary>What a plain grab of this joint does under these modes.</summary>
    private static AnimatorDragKind JointKind(XafPackage package, int bone, AnimatorModes modes)
    {
        if (modes.Ik && !modes.Rest && AnimatorPreviewChain.TryGet(package, bone, out _, out _))
            return AnimatorDragKind.Ik;
        return modes.Rest || package.Skeleton.Bones[bone].CanTranslate
            ? AnimatorDragKind.Bone
            : AnimatorDragKind.Swing;
    }

    /// <summary>Starts, or restarts from where things are now, a plain joint grab under the current modes.</summary>
    private static void StartJoint(
        Entity session,
        Vector2 pointer,
        int bone,
        AnimatorModes modes,
        XafPose pose,
        Transform2D[] world)
    {
        bool usedIk = session.GetComponent<AnimatorDrag>().FollowsModes &&
                      session.GetComponent<AnimatorDrag>().UsedIk;
        if (JointKind(session.GetComponent<AnimatorDocument>().Package, bone, modes) == AnimatorDragKind.Ik)
            BeginIk(session, pointer, bone);
        else
            BeginBone(session, pointer, bone, modes.Rest, pose, world);

        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        drag.FollowsModes = true;
        drag.Grabbed = bone;
        drag.Rest = modes.Rest;
        drag.UsedIk = usedIk || drag.Kind == AnimatorDragKind.Ik;
    }

    /// <summary>
    /// Keeps a held joint grab in step with the modes: when IK or REST was
    /// turned on or off since it started, it carries on as the grab those modes
    /// make, from where the rig is now, inside the same undo step; a PIN change
    /// starts or stops holding the children still. No second press needed.
    /// </summary>
    public static void FollowModes(Entity session, Vector2 pointer, Rect2 area)
    {
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        if (!drag.FollowsModes)
            return;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        XafPackage package = document.Package;
        if (drag.Grabbed < 0 || drag.Grabbed >= package.Skeleton.Bones.Count)
            return;

        if (JointKind(package, drag.Grabbed, modes) != drag.Kind || drag.Rest != modes.Rest)
        {
            XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
            StartJoint(session, pointer, drag.Grabbed, modes, pose, XafRig.WorldBones(package, pose));
            return;
        }

        if (drag.Kind == AnimatorDragKind.Bone && modes.Rest && (drag.Pinned is not null) != AnimatorRestPin.IsOn(session))
            session.GetComponent<AnimatorDrag>().Pinned = AnimatorRestPin.Capture(session, new[] { drag.Grabbed });
    }

    /// <summary>
    /// Grabbing a joint moves it when the bone may translate (or in rest pose);
    /// otherwise the bone keeps its length and its parent swings instead.
    /// </summary>
    private static void BeginBone(
        Entity session,
        Vector2 pointer,
        int bone,
        bool rest,
        XafPose pose,
        Transform2D[] world)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafBone rig = document.Package.Skeleton.Bones[bone];
        document.Runtime.SetPaused(true);
        selection.Attachment = -1;
        selection.Effect = -1;

        if (rest || rig.CanTranslate)
        {
            selection.SelectOnlyBone(bone);
            session.GetComponent<AnimatorDrag>() = new AnimatorDrag
            {
                Kind = AnimatorDragKind.Bone,
                StartPointer = pointer,
                StartValue = rest ? rig.Rest.Origin : pose.Bones[bone].Translation,
                ParentWorld = rig.Parent >= 0 ? world[rig.Parent] : Transform2D.Identity,
                Pinned = rest ? AnimatorRestPin.Capture(session, new[] { bone }) : null,
            };
            AnimatorSession.SetStatus(session, rest ? $"SELECTED {rig.Id} (REST)" : $"SELECTED {rig.Id}");
            return;
        }

        selection.SelectOnlyBone(rig.Parent);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = AnimatorDragKind.Swing,
            StartPointer = pointer,
            Joint = bone,
        };
        AnimatorSession.SetStatus(
            session,
            $"ROTATING {document.Package.Skeleton.Bones[rig.Parent].Id}");
    }

    private static void BeginIk(Entity session, Vector2 pointer, int joint)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        document.Runtime.SetPaused(true);
        selection.SelectOnlyBone(joint);
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag
        {
            Kind = AnimatorDragKind.Ik,
            StartPointer = pointer,
            Joint = joint,
        };
        AnimatorSession.SetStatus(session, $"IK {document.Package.Skeleton.Bones[joint].Id}");
    }
}
