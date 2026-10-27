using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

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
        List<int> bones = selection.Bones();
        for (int i = bones.Count - 1; i >= 0; i--)
        {
            if (bones[i] >= document.Package.Skeleton.Bones.Count)
                bones.RemoveAt(i);
        }

        if (bones.Count > 0)
        {
            foreach (int bone in bones)
            {
                XafAuthoring.KeyBoneTransform(
                    document.Package, clip, bone, time, pose.Bones[bone]);
            }

            string names = bones.Count == 1
                ? document.Package.Skeleton.Bones[bones[0]].Id
                : $"{bones.Count} BONES";
            AnimatorSession.SetStatus(session, $"KEYED {names}");
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

    /// <summary>Keys the current pose of the given bones at the playhead as one undo step.</summary>
    public static void KeyBones(Entity session, params int[] bones) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            XafClip? clip = AnimatorSession.CurrentClip(document);
            if (clip is null || session.GetComponent<AnimatorModes>().Rest)
                return;

            foreach (int bone in bones)
            {
                XafAuthoring.KeyBoneTransform(document.Package, clip, bone,
                    document.Runtime.CurrentTime, document.Runtime.Pose.Bones[bone]);
            }
            AnimatorSession.SetStatus(session, $"KEYED {bones.Length} BONES");
        });

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
            AnimatorRestPin.Pinned? pinned = AnimatorRestPin.Capture(session, selection.Bones());
            NudgeRest(document.Package, selection, rotation, scale);
            AnimatorRestPin.Restore(session, pinned);
            return;
        }

        List<int> bones = selection.Bones();
        if (bones.Count > 0)
        {
            foreach (int bone in bones)
            {
                if (bone < document.Runtime.Pose.Bones.Length)
                    Apply(ref document.Runtime.Pose.Bones[bone], rotation, scale);
            }
        }
        else if (selection.Attachment >= 0)
        {
            Apply(ref document.Runtime.Pose.Attachments[selection.Attachment], rotation, scale);
        }
        else
        {
            return;
        }

        if (modes.AutoKey)
            KeySelected(session);
    }

    /// <summary>Lets the selected bone translate, or locks it back to its rest length.</summary>
    public static void ToggleMovable(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => ToggleMovableCore(session));

    private static void ToggleMovableCore(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        List<int> bones = session.GetComponent<AnimatorSelection>().Bones();
        int changed = 0;
        bool movable = false;
        string changedId = "";
        foreach (int index in bones)
        {
            if (index < 0 || index >= package.Skeleton.Bones.Count)
                continue;

            XafBone bone = package.Skeleton.Bones[index];
            if (bone.Parent < 0)
                continue;

            bone.AllowTranslation = !bone.AllowTranslation;
            movable = bone.AllowTranslation;
            changedId = bone.Id;
            changed++;
        }

        if (changed == 0)
        {
            AnimatorSession.SetStatus(session, "ROOT IS ALWAYS MOVABLE");
            return;
        }

        AnimatorSession.SetStatus(
            session,
            changed == 1
                ? $"{changedId} {(movable ? "MOVABLE" : "FIXED LENGTH")}"
                : $"{changed} BONES {(movable ? "MOVABLE" : "FIXED LENGTH")}");
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

        selection.SelectOnlyBone(created);
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

        selection.SelectOnlyBone(parent >= 0 ? parent : 0);
        AnimatorSession.RigChanged(session);
        AnimatorSession.SetStatus(session, "BONE DELETED");
    }

    /// <summary>Renames a bone; false when the name is empty or already used.</summary>
    public static bool RenameBone(Entity session, int index, string id)
    {
        bool renamed = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (!XafRigAuthoring.RenameBone(package, index, id))
                return;

            renamed = true;
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"RENAMED TO {id}");
        });

        if (!renamed)
            AnimatorSession.SetStatus(session, "NAME EMPTY OR ALREADY USED");
        return renamed;
    }

    /// <summary>Moves a bone under a new parent and keeps it selected.</summary>
    public static bool ReparentBone(Entity session, int index, int parent)
    {
        bool moved = false;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            XafPackage package = session.GetComponent<AnimatorDocument>().Package;
            if (index < 0 || index >= package.Skeleton.Bones.Count ||
                parent < 0 || parent >= package.Skeleton.Bones.Count)
            {
                return;
            }

            string id = package.Skeleton.Bones[index].Id;
            string parentId = package.Skeleton.Bones[parent].Id;
            if (!XafRigAuthoring.ReparentBone(package, index, parent))
                return;

            // Reparenting may reorder the bones, so find the moved one again.
            moved = true;
            ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
            selection.SelectOnlyBone(XafRig.FindBone(package, id));
            AnimatorSession.RigChanged(session);
            AnimatorSession.SetStatus(session, $"{id} NOW UNDER {parentId}");
        });

        if (!moved)
            AnimatorSession.SetStatus(session, "CANNOT REPARENT THERE");
        return moved;
    }

    private static void NudgeRest(
        XafPackage package,
        AnimatorSelection selection,
        float rotation,
        float scale)
    {
        List<int> bones = selection.Bones();
        if (bones.Count > 0)
        {
            foreach (int index in bones)
            {
                if (index < 0 || index >= package.Skeleton.Bones.Count)
                    continue;

                XafBone bone = package.Skeleton.Bones[index];
                bone.Rest = Nudged(bone.Rest, rotation, scale);
            }
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
