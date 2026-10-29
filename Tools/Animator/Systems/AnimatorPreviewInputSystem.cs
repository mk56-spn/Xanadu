using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Selects and drags bones and attachments in the preview panel. The wheel
/// zooms around the pointer, the middle button pans, and hover highlights what
/// a click would pick. With IK drag on, a joint pulls its parent and
/// grandparent along as a two-bone chain.
/// </summary>
public sealed class AnimatorPreviewInputSystem(
    IPointerInputSource pointer,
    IAnimatorWheel? wheel = null) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _panelHeld;
    private bool _middleHeld;
    private Vector2 _lastMiddle;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        if (session.GetComponent<AnimatorNewClipDialog>().Open)
        {
            session.GetComponent<AnimatorHover>() = AnimatorHover.None;
            return;
        }

        Entity panel = session.GetComponent<AnimatorUi>().Preview;
        PointerInput input = pointer.Read();
        Rect2 area = AnimatorGeometry.PanelArea(panel);
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();

        if (wheel is not null && area.HasPoint(input.Position))
            Wheel(session, ref view, area, input);

        // Preview mode still pans and zooms, but picks and drags nothing.
        if (Pan(ref view, area, input) || AnimatorPreviewModeCommands.IsOn(session))
        {
            session.GetComponent<AnimatorHover>() = AnimatorHover.None;
            return;
        }

        bool pressed = !panel.IsNull && panel.Tags.Has<Pressed>();
        bool begin = pressed && !_panelHeld;
        _panelHeld = pressed;

        Hover(session, area, input, !input.Pressed && !begin);

        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        if (begin)
        {
            Begin(session, input.Position, area, input.Shift);
        }
        else if (drag.Kind is AnimatorDragKind.Bone or AnimatorDragKind.Attachment
                     or AnimatorDragKind.Swing or AnimatorDragKind.Ik ||
                 AnimatorChainHandles.IsChainDrag(drag.Kind))
        {
            if (input.Pressed)
                Drag(session, input.Position, area, input.Shift);
            else
                End(session);
        }
    }

    private void Wheel(
        Entity session,
        ref AnimatorPreviewView view,
        Rect2 area,
        PointerInput input)
    {
        int notches = wheel!.TakeWheel();
        if (notches == 0)
            return;

        view.ZoomAt(
            input.Position,
            area,
            Mathf.Pow(AnimatorPreviewView.WheelStep, notches));
        AnimatorSession.SetStatus(session, $"ZOOM x{view.Zoom:0.##}");
    }

    /// <summary>Middle-drag panning, independent of selection; true while it owns the pointer.</summary>
    private bool Pan(ref AnimatorPreviewView view, Rect2 area, PointerInput input)
    {
        if (!input.Middle)
        {
            _middleHeld = false;
            return false;
        }

        if (!_middleHeld)
        {
            _middleHeld = true;
            _lastMiddle = input.Position;
            return true;
        }

        if (area.HasPoint(input.Position))
            view.Pan += input.Position - _lastMiddle;
        _lastMiddle = input.Position;
        return true;
    }

    /// <summary>Highlights the joint, label or attachment a click would pick.</summary>
    private static void Hover(Entity session, Rect2 area, PointerInput input, bool okay)
    {
        ref AnimatorHover hover = ref session.GetComponent<AnimatorHover>();
        if (!okay || !area.HasPoint(input.Position))
        {
            hover = AnimatorHover.None;
            return;
        }

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);
        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);

        if (modes.Svg)
        {
            Transform2D[] attachments = XafRig.WorldAttachments(document.Package, pose, world);
            hover = new AnimatorHover
            {
                Bone = -1,
                Attachment = AnimatorGeometry.PickNearest(
                    attachments, placement, input.Position, AnimatorGeometry.AttachmentHitRadius),
            };
            return;
        }

        hover = new AnimatorHover
        {
            Bone = AnimatorGeometry.PickBoneOrLabel(world, placement, input.Position),
            Attachment = -1,
        };
    }

    private static void Begin(Entity session, Vector2 pointer, Rect2 area, bool shift)
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

        // A cloth inspector button waits for clicks: they pin points or pick bones until stopped.
        if (session.GetComponent<AnimatorCloths>().Picking != AnimatorClothPick.None)
        {
            AnimatorClothCommands.Click(session, pointer, placement, world);
            return;
        }

        // The selected chain's root and collider are handles that win over the bones under them.
        if (AnimatorChainHandles.TryBegin(session, pointer, placement, world))
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

            if (bone >= 0 && modes.Ik && !modes.Rest && Chain(package, bone, out _, out _))
                BeginIk(session, pointer, bone);
            else if (bone >= 0)
                BeginBone(session, pointer, bone, modes.Rest, pose, world);
            return;
        }

        Transform2D[] attachments = XafRig.WorldAttachments(package, pose, world);
        int attachment = AnimatorGeometry.PickNearest(
            attachments, placement, pointer, AnimatorGeometry.AttachmentHitRadius);
        if (attachment < 0)
            return;

        selection.SelectOnlyAttachment(attachment);
        document.Runtime.SetPaused(true);
        if (modes.Rest)
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
            if (rest)
                AnimatorHistoryCommands.BeginGesture(session);
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

    /// <summary>The joint's parent and grandparent, when it has both.</summary>
    private static bool Chain(XafPackage package, int joint, out int parent, out int grandparent)
    {
        parent = package.Skeleton.Bones[joint].Parent;
        grandparent = parent >= 0 ? package.Skeleton.Bones[parent].Parent : -1;
        return grandparent >= 0;
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

    /// <summary>
    /// Solves the two-bone chain for the pointer, keeping the side the chain
    /// already bends to, then turns the grandparent and parent to match.
    /// </summary>
    private static void Ik(Entity session, Vector2 pointer, Transform2D placement, bool free)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        int joint = session.GetComponent<AnimatorDrag>().Joint;
        if (!Chain(document.Package, joint, out int parent, out int grandparent))
            return;

        XafPose pose = document.Runtime.Pose;
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);
        Vector2 root = world[grandparent].Origin;
        Vector2 elbow = world[parent].Origin;
        Vector2 end = world[joint].Origin;
        Vector2 reach = end - root;
        bool bendPositive = new Vector2(-reach.Y, reach.X).Dot(elbow - root) >= 0f;
        Vector2 target = placement.AffineInverse() * pointer;
        ref AnimatorSnapGuides guides = ref session.GetComponent<AnimatorSnapGuides>();
        guides = default;
        if (!free)
        {
            // The chain moves, so it only snaps to joints outside it; its root stays a target.
            float radius = AnimatorSnap.Pixels / Mathf.Max(0.0001f, Mathf.Abs(placement.Scale.X));
            target = AnimatorSnap.AlignPoint(
                target,
                AnimatorSnap.Joints(document.Package, world, grandparent, keepRoot: true),
                radius,
                out guides);
        }

        Vector2[] solved = XafFabrik.Solve(new[] { root, elbow, end }, target, bendPositive);

        Aim(document.Package, pose, grandparent, parent, solved[1]);
        Aim(document.Package, pose, parent, joint, solved[2]);
    }

    /// <summary>Turns a bone about its origin so its child joint points at a skeleton-space target.</summary>
    private static void Aim(XafPackage package, XafPose pose, int bone, int child, Vector2 target)
    {
        Transform2D[] world = XafRig.WorldBones(package, pose);
        Vector2 toTarget = target - world[bone].Origin;
        Vector2 toChild = world[child].Origin - world[bone].Origin;
        if (toTarget.LengthSquared() < 0.0001f || toChild.LengthSquared() < 0.0001f)
            return;

        float delta = Mathf.Wrap(toTarget.Angle() - toChild.Angle(), -Mathf.Pi, Mathf.Pi);
        // A mirrored bone turns local rotation the opposite way on screen.
        if (world[bone].Determinant() < 0f)
            delta = -delta;
        pose.Bones[bone].Rotation += delta;
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
        return drag.ParentWorld.AffineInverse() * snapped;
    }

    private static void Drag(Entity session, Vector2 pointer, Rect2 area, bool shift)
    {
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
        Transform2D placement = AnimatorGeometry.PreviewPlacement(area, view);

        if (AnimatorChainHandles.IsChainDrag(drag.Kind))
        {
            AnimatorChainHandles.Drag(session, pointer, placement);
            return;
        }

        if (drag.Kind == AnimatorDragKind.Swing)
        {
            Swing(session, pointer, placement, shift);
            return;
        }

        if (drag.Kind == AnimatorDragKind.Ik)
        {
            Ik(session, pointer, placement, shift);
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

    private static void End(Entity session)
    {
        session.GetComponent<AnimatorSnapGuides>() = default;
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
        AnimatorHistoryCommands.EndGesture(session);
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        if (AnimatorChainHandles.IsChainDrag(drag.Kind) || drag.Kind == AnimatorDragKind.None || modes.Rest)
            return;

        // An IK drag turned the parent and grandparent, so those are what get
        // keyed; selected keys on them take the change instead.
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (drag.Kind == AnimatorDragKind.Ik && Chain(package, drag.Joint, out int parent, out int grandparent))
        {
            int edited = 0;
            AnimatorHistoryCommands.Edit(session, () => edited = AnimatorKeyEditCommands.Apply(session));
            if (edited == 0 && modes.AutoKey)
                AnimatorEditing.KeyBones(session, grandparent, parent);
        }
        else
        {
            AnimatorEditing.KeyEdit(session);
        }
    }
}
