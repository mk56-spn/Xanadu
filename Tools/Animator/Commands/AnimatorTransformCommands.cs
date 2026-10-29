using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

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
                List<int> edited = Bones(session, selection);
                AnimatorRestPin.Pinned? pinned = AnimatorRestPin.Capture(session, edited);
                foreach (int index in edited)
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(radians, bone.Rest.Scale, 0f, bone.Rest.Origin);
                    changed++;
                }
                AnimatorRestPin.Restore(session, pinned);
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

            if (changed == 0 && EditEffect(session, transform => transform with { Rotation = radians }))
                changed = 1;

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
                List<int> edited = Bones(session, selection);
                AnimatorRestPin.Pinned? pinned = AnimatorRestPin.Capture(session, edited);
                foreach (int index in edited)
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(bone.Rest.Rotation, clamped, 0f, bone.Rest.Origin);
                    changed++;
                }
                AnimatorRestPin.Restore(session, pinned);
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

            if (changed == 0 && EditEffect(session, transform => transform with { Scale = clamped }))
                changed = 1;

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
                List<int> edited = Bones(session, selection);
                AnimatorRestPin.Pinned? pinned = AnimatorRestPin.Capture(session, edited);
                foreach (int index in edited)
                {
                    XafBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = new Transform2D(
                        bone.Rest.Rotation, bone.Rest.Scale, 0f, translation);
                    changed++;
                }
                AnimatorRestPin.Restore(session, pinned);
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

            if (changed == 0 && EditEffect(session, transform => transform with { Translation = translation }))
                changed = 1;

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

            if (changed == 0 && AnimatorEffectCommands.Selected(session) is var effect and >= 0 &&
                effect < document.Runtime.Pose.Effects.Length)
            {
                XafEffect rested = document.Package.Effects[effect];
                document.Runtime.Pose.Effects[effect].Transform = XafTransform.From(rested.Transform);
                name = rested.Id;
                changed = 1;
            }

            if (changed == 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE, ATTACHMENT OR EFFECT FIRST");
                return;
            }

            if (modes.AutoKey)
                AnimatorEditing.KeySelected(session);
            AnimatorSession.SetStatus(
                session,
                changed == 1 ? $"RESET {name} TO REST" : $"RESET {changed} BONES TO REST");
        });

    /// <summary>
    /// Changes the selected effect's transform: its resting place on the bone
    /// in rest mode, its pose otherwise. False when no effect is selected.
    /// </summary>
    private static bool EditEffect(Entity session, System.Func<XafTransform, XafTransform> change)
    {
        int index = AnimatorEffectCommands.Selected(session);
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        if (index < 0)
            return false;

        if (session.GetComponent<AnimatorModes>().Rest)
        {
            XafEffect effect = document.Package.Effects[index];
            effect.Transform = change(XafTransform.From(effect.Transform)).ToTransform();
            return true;
        }

        if (index >= document.Runtime.Pose.Effects.Length)
            return false;

        ref XafEffectState state = ref document.Runtime.Pose.Effects[index];
        state.Transform = change(state.Transform);
        return true;
    }

    /// <summary>The selected bones still inside the skeleton.</summary>
    private static List<int> Bones(Entity session, AnimatorSelection selection)
    {
        int count = session.GetComponent<AnimatorDocument>().Package.Skeleton.Bones.Count;
        List<int> bones = selection.Bones();
        bones.RemoveAll(index => index < 0 || index >= count);
        return bones;
    }
}
