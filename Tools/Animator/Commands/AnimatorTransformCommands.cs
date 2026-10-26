using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>
/// Exact transform edits from the inspector: typed values replace the pose or
/// rest transform of every selected bone (or the selected attachment).
/// </summary>
public static class AnimatorTransformCommands
{
    /// <summary>Sets the selection's rotation exactly, in radians; keyed straight away with auto key.</summary>
    public static void SetRotation(Entity session, float radians) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            AnimatorModes modes = session.GetComponent<AnimatorModes>();
            document.Runtime.SetPaused(true);

            int changed = 0;
            if (modes.Rest)
            {
                foreach (int index in Bones(session, selection))
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(radians, bone.Rest.Scale, 0f, bone.Rest.Origin);
                    changed++;
                }
            }
            else
            {
                foreach (int index in Bones(session, selection))
                {
                    document.Runtime.Pose.Bones[index].Rotation = radians;
                    changed++;
                }
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                if (modes.Rest)
                {
                    XafAttachment attachment = document.Package.Attachments[selection.Attachment];
                    attachment.Transform = new Transform2D(
                        radians, attachment.Transform.Scale, 0f, attachment.Transform.Origin);
                }
                else
                {
                    document.Runtime.Pose.Attachments[selection.Attachment].Rotation = radians;
                }
                changed = 1;
            }

            if (changed == 0)
                return;

            if (modes.AutoKey && !modes.Rest)
                AnimatorEditing.KeySelected(session);
            AnimatorSession.SetStatus(
                session, changed == 1 ? "ROTATION SET" : $"ROTATION SET ON {changed} BONES");
        });

    /// <summary>Sets the selection's scale exactly, in the pose or the rest transform.</summary>
    public static void SetScale(Entity session, Vector2 scale) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            Vector2 clamped = new(
                Mathf.Clamp(scale.X, 0.01f, 20f),
                Mathf.Clamp(scale.Y, 0.01f, 20f));
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            AnimatorModes modes = session.GetComponent<AnimatorModes>();
            document.Runtime.SetPaused(true);

            int changed = 0;
            if (modes.Rest)
            {
                foreach (int index in Bones(session, selection))
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(bone.Rest.Rotation, clamped, 0f, bone.Rest.Origin);
                    changed++;
                }
            }
            else
            {
                foreach (int index in Bones(session, selection))
                {
                    document.Runtime.Pose.Bones[index].Scale = clamped;
                    changed++;
                }
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                if (modes.Rest)
                {
                    XafAttachment attachment = document.Package.Attachments[selection.Attachment];
                    attachment.Transform = new Transform2D(
                        attachment.Transform.Rotation, clamped, 0f, attachment.Transform.Origin);
                }
                else
                {
                    document.Runtime.Pose.Attachments[selection.Attachment].Scale = clamped;
                }
                changed = 1;
            }

            if (changed == 0)
                return;

            if (modes.AutoKey && !modes.Rest)
                AnimatorEditing.KeySelected(session);
            AnimatorSession.SetStatus(
                session, changed == 1 ? "SCALE SET" : $"SCALE SET ON {changed} BONES");
        });

    /// <summary>
    /// Sets the selection's local translation. Fixed-length bones can only be
    /// moved after turning MOVABLE on, so those are skipped and the status says
    /// why when nothing could move.
    /// </summary>
    public static void SetTranslation(Entity session, Vector2 translation) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            AnimatorModes modes = session.GetComponent<AnimatorModes>();
            document.Runtime.SetPaused(true);

            int changed = 0;
            int fixedBones = 0;
            if (modes.Rest)
            {
                foreach (int index in Bones(session, selection))
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(
                        bone.Rest.Rotation, bone.Rest.Scale, 0f, translation);
                    changed++;
                }
            }
            else
            {
                foreach (int index in Bones(session, selection))
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    if (!bone.CanTranslate)
                    {
                        fixedBones++;
                        continue;
                    }

                    document.Runtime.Pose.Bones[index].Translation = translation;
                    changed++;
                }
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                if (modes.Rest)
                {
                    XafAttachment attachment = document.Package.Attachments[selection.Attachment];
                    attachment.Transform = new Transform2D(
                        attachment.Transform.Rotation,
                        attachment.Transform.Scale,
                        0f,
                        translation);
                }
                else
                {
                    document.Runtime.Pose.Attachments[selection.Attachment].Translation = translation;
                }
                changed = 1;
            }

            if (changed == 0)
            {
                if (fixedBones > 0)
                {
                    string name = document.Package.Skeleton.Bones[selection.Bone].Id;
                    AnimatorSession.SetStatus(session, $"{name} IS FIXED LENGTH — USE MOVABLE");
                }
                return;
            }

            if (modes.AutoKey && !modes.Rest)
                AnimatorEditing.KeySelected(session);
            AnimatorSession.SetStatus(
                session,
                fixedBones > 0
                    ? $"POSITION SET ({fixedBones} FIXED SKIPPED)"
                    : changed == 1
                        ? "POSITION SET"
                        : $"POSITION SET ON {changed} BONES");
        });

    /// <summary>
    /// Clears the selection's pose edits by putting it back on the rest
    /// transform. In rest mode there is nothing to clear, so it only says so.
    /// </summary>
    public static void ResetSelected(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            AnimatorModes modes = session.GetComponent<AnimatorModes>();
            if (modes.Rest)
            {
                AnimatorSession.SetStatus(session, "RESET APPLIES TO THE POSE, NOT THE REST POSE");
                return;
            }

            document.Runtime.SetPaused(true);
            int changed = 0;
            string name = "";
            foreach (int index in Bones(session, selection))
            {
                XafBone bone = document.Package.Skeleton.Bones[index];
                document.Runtime.Pose.Bones[index] = XafTransform.From(bone.Rest);
                name = bone.Id;
                changed++;
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                XafAttachment attachment = document.Package.Attachments[selection.Attachment];
                document.Runtime.Pose.Attachments[selection.Attachment] =
                    XafTransform.From(attachment.Transform);
                name = attachment.Group;
                changed = 1;
            }

            if (changed == 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE OR ATTACHMENT FIRST");
                return;
            }

            if (modes.AutoKey)
                AnimatorEditing.KeySelected(session);
            AnimatorSession.SetStatus(
                session,
                changed == 1 ? $"RESET {name} TO REST" : $"RESET {changed} BONES TO REST");
        });

    /// <summary>The selected bones still inside the skeleton.</summary>
    private static List<int> Bones(Entity session, AnimatorSelection selection)
    {
        int count = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones.Count;
        List<int> bones = selection.Bones();
        bones.RemoveAll(index => index < 0 || index >= count);
        return bones;
    }
}
