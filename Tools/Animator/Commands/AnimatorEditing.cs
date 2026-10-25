using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>Pose, key and bone edits on the selected rig element.</summary>
public static class AnimatorEditing
{
    public const float RotationStep = 0.25f;
    public const float ScaleStep = 0.1f;

    public static void KeySelected(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => Key(session));

    private static void Key(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (session.GetComponent<AnimatorModes>().Rest)
        {
            AnimatorSession.SetStatus(session, "LEAVE REST POSE TO KEY");
            return;
        }

        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return;

        float time = document.Runtime.CurrentTime;
        XafPose pose = document.Runtime.Pose;
        if (selection.Bone >= 0)
        {
            XafAuthoring.KeyBoneTransform(
                document.Package,
                clip,
                selection.Bone,
                time,
                pose.Bones[selection.Bone]);
            AnimatorSession.SetStatus(
                session,
                $"KEYED {document.Package.Skeleton.Bones[selection.Bone].Id}");
        }
        else if (selection.Attachment >= 0)
        {
            XafAuthoring.KeyAttachmentTransform(
                document.Package,
                clip,
                selection.Attachment,
                time,
                pose.Attachments[selection.Attachment]);
            AnimatorSession.SetStatus(
                session,
                $"KEYED {document.Package.Attachments[selection.Attachment].Group}");
        }
    }

    /// <summary>Rotates and scales the selection in the pose, or in the rest pose.</summary>
    public static void Nudge(Entity session, float rotation, float scale) =>
        AnimatorHistoryCommands.Edit(session, () => NudgeCore(session, rotation, scale));

    private static void NudgeCore(Entity session, float rotation, float scale)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        AnimatorModes modes = session.GetComponent<AnimatorModes>();
        document.Runtime.SetPaused(true);

        if (modes.Rest)
        {
            NudgeRest(document.Package, selection, rotation, scale);
            return;
        }

        if (selection.Bone >= 0)
            Apply(ref document.Runtime.Pose.Bones[selection.Bone], rotation, scale);
        else if (selection.Attachment >= 0)
            Apply(ref document.Runtime.Pose.Attachments[selection.Attachment], rotation, scale);
        else
            return;

        if (modes.AutoKey)
            KeySelected(session);
    }

    /// <summary>Lets the selected bone translate, or locks it back to its rest length.</summary>
    public static void ToggleMovable(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => ToggleMovableCore(session));

    private static void ToggleMovableCore(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        int index = session.GetComponent<AnimatorSelection>().Bone;
        if (index < 0 || index >= package.Skeleton.Bones.Count)
            return;

        XafBone bone = package.Skeleton.Bones[index];
        if (bone.Parent < 0)
        {
            AnimatorSession.SetStatus(session, "ROOT IS ALWAYS MOVABLE");
            return;
        }

        bone.AllowTranslation = !bone.AllowTranslation;
        AnimatorSession.SetStatus(
            session,
            $"{bone.Id} {(bone.AllowTranslation ? "MOVABLE" : "FIXED LENGTH")}");
    }

    public static void AddBone(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => AddBoneCore(session));

    private static void AddBoneCore(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        int parent = selection.Bone >= 0 ? selection.Bone : 0;
        int number = 1;
        string id;
        do
        {
            id = $"bone-{number++}";
        } while (document.Package.Skeleton.Bones.Exists(bone => bone.Id == id));

        int created = XafRigAuthoring.AddBone(
            document.Package,
            parent,
            id,
            new Vector2(0, -40));
        if (created < 0)
        {
            AnimatorSession.SetStatus(session, "ADD BONE FAILED");
            return;
        }

        selection.Bone = created;
        selection.Attachment = -1;
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, $"ADDED {id}");
    }

    public static void DeleteBone(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteBoneCore(session));

    private static void DeleteBoneCore(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        if (selection.Bone < 0 ||
            selection.Bone >= document.Package.Skeleton.Bones.Count)
        {
            return;
        }

        int parent = document.Package.Skeleton.Bones[selection.Bone].Parent;
        if (!XafRigAuthoring.DeleteBone(document.Package, selection.Bone))
        {
            AnimatorSession.SetStatus(session, "CANNOT DELETE BONE");
            return;
        }

        selection.Bone = parent >= 0 ? parent : 0;
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, "BONE DELETED");
    }

    private static void NudgeRest(
        XafPackage package,
        AnimatorSelection selection,
        float rotation,
        float scale)
    {
        if (selection.Bone >= 0)
        {
            XafBone bone = package.Skeleton.Bones[selection.Bone];
            bone.Rest = Nudged(bone.Rest, rotation, scale);
        }
        else if (selection.Attachment >= 0)
        {
            XafAttachment attachment = package.Attachments[selection.Attachment];
            attachment.Transform = Nudged(attachment.Transform, rotation, scale);
        }
    }

    private static Transform2D Nudged(
        Transform2D transform,
        float rotation,
        float scale) => new(
        transform.Rotation + rotation,
        transform.Scale + new Vector2(scale, scale),
        0f,
        transform.Origin);

    private static void Apply(ref XafTransform transform, float rotation, float scale)
    {
        transform.Rotation += rotation;
        transform.Scale += new Vector2(scale, scale);
    }
}
