using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Tools.Animator.Commands;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// Selects and drags bones and attachments in the preview panel. With IK drag
/// on, a joint pulls its parent and grandparent along as a two-bone chain.
/// </summary>
public sealed class AnimatorPreviewInputSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _panelHeld;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        Entity panel = session.GetComponent<AnimatorUi>().Preview;
        PointerInput input = pointer.Read();
        bool pressed = !panel.IsNull && panel.Tags.Has<Pressed>();
        bool begin = pressed && !_panelHeld;
        _panelHeld = pressed;

        Rect2 area = AnimatorGeometry.PanelArea(panel);
        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        if (begin)
        {
            Begin(session, input.Position, area);
        }
        else if (drag.Kind is AnimatorDragKind.Bone or AnimatorDragKind.Attachment
                 or AnimatorDragKind.Swing or AnimatorDragKind.Ik)
        {
            if (input.Pressed)
                Drag(session, input.Position, AnimatorGeometry.PreviewOrigin(area));
            else
                End(session);
        }
    }

    private static void Begin(Entity session, Vector2 pointer, Rect2 area)
    {
        if (!area.HasPoint(pointer))
            return;

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafPackage package = document.Package;
        XafPose pose = AnimatorGeometry.PreviewPose(document, modes);
        Transform2D[] world = XafRig.WorldBones(package, pose);
        Vector2 origin = AnimatorGeometry.PreviewOrigin(area);
        string suffix = modes.Rest ? " (REST)" : "";

        // A reparent from the bone menu takes the next click as its new parent.
        ref AnimatorBoneMenu menu = ref session.GetComponent<AnimatorBoneMenu>();
        if (menu.Reparenting >= 0)
        {
            int child = menu.Reparenting;
            menu.Reparenting = -1;
            int target = AnimatorGeometry.PickBoneOrLabel(world, origin, pointer);
            if (target >= 0)
                AnimatorEditing.ReparentBone(session, child, target);
            else
                AnimatorSession.SetStatus(session, "REPARENT CANCELLED");
            return;
        }

        // Bone movement and SVG placement are separate modes, so a click only
        // ever considers one kind of target.
        if (!modes.Svg)
        {
            int bone = AnimatorGeometry.PickNearest(
                world, origin, pointer, AnimatorGeometry.BoneHitRadius);
            if (bone >= 0 && modes.Ik && !modes.Rest && Chain(package, bone, out _, out _))
                BeginIk(session, pointer, bone);
            else if (bone >= 0)
                BeginBone(session, pointer, bone, modes.Rest, pose, world);
            return;
        }

        Transform2D[] attachments = XafRig.WorldAttachments(package, pose, world);
        int attachment = AnimatorGeometry.PickNearest(
            attachments, origin, pointer, AnimatorGeometry.AttachmentHitRadius);
        if (attachment < 0)
            return;

        selection.Attachment = attachment;
        selection.Bone = -1;
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

        if (rest || rig.CanTranslate)
        {
            if (rest)
                AnimatorHistoryCommands.BeginGesture(session);
            selection.Bone = bone;
            session.GetComponent<AnimatorDrag>() = new AnimatorDrag
            {
                Kind = AnimatorDragKind.Bone,
                StartPointer = pointer,
                StartValue = rest ? rig.Rest.Origin : pose.Bones[bone].Translation,
                ParentWorld = rig.Parent >= 0 ? world[rig.Parent] : Transform2D.Identity,
            };
            AnimatorSession.SetStatus(session, rest ? $"SELECTED {rig.Id} (REST)" : $"SELECTED {rig.Id}");
            return;
        }

        selection.Bone = rig.Parent;
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
        selection.Bone = joint;
        selection.Attachment = -1;
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
    private static void Ik(Entity session, Vector2 pointer, Vector2 origin)
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
        Vector2[] solved = XafFabrik.Solve(new[] { root, elbow, end }, pointer - origin, bendPositive);

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

    private static void Swing(Entity session, Vector2 pointer, Vector2 origin)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        int joint = session.GetComponent<AnimatorDrag>().Joint;
        int parent = document.Package.Skeleton.Bones[joint].Parent;
        XafPose pose = document.Runtime.Pose;
        Transform2D[] world = XafRig.WorldBones(document.Package, pose);

        Vector2 toPointer = pointer - (origin + world[parent].Origin);
        Vector2 toJoint = world[joint].Origin - world[parent].Origin;
        if (toPointer.LengthSquared() < 1f || toJoint.LengthSquared() < 0.0001f)
            return;

        float delta = Mathf.Wrap(toPointer.Angle() - toJoint.Angle(), -Mathf.Pi, Mathf.Pi);
        // A mirrored parent turns local rotation the opposite way on screen.
        if (world[parent].Determinant() < 0f)
            delta = -delta;
        pose.Bones[parent].Rotation += delta;
    }

    private static void Drag(Entity session, Vector2 pointer, Vector2 origin)
    {
        if (session.GetComponent<AnimatorDrag>().Kind == AnimatorDragKind.Swing)
        {
            Swing(session, pointer, origin);
            return;
        }

        if (session.GetComponent<AnimatorDrag>().Kind == AnimatorDragKind.Ik)
        {
            Ik(session, pointer, origin);
            return;
        }

        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool rest = session.GetComponent<AnimatorModes>().Rest;

        // The pointer moves in screen space; the value lives in the parent's space.
        Vector2 value = drag.StartValue +
                        drag.ParentWorld.BasisXformInv(pointer - drag.StartPointer);

        if (drag.Kind == AnimatorDragKind.Bone && selection.Bone >= 0)
        {
            if (rest)
            {
                XafBone bone = document.Package.Skeleton.Bones[selection.Bone];
                bone.Rest = new Transform2D(bone.Rest.Rotation, bone.Rest.Scale, 0f, value);
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
        AnimatorDrag drag = session.GetComponent<AnimatorDrag>();
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag();
        AnimatorHistoryCommands.EndGesture(session);
        if (session.GetComponent<AnimatorModes>() is not { AutoKey: true, Rest: false })
            return;

        // An IK drag turned the parent and grandparent, so those are what get keyed.
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (drag.Kind == AnimatorDragKind.Ik && Chain(package, drag.Joint, out int parent, out int grandparent))
            AnimatorEditing.KeyBones(session, grandparent, parent);
        else
            AnimatorEditing.KeySelected(session);
    }
}
