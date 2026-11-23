using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>
/// Exact transform edits from the inspector: typed values replace the pose or
/// rest transform of every selected bone (or the selected attachment).
/// </summary>
public static class AnimatorTransformCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<ResetTransform>((s, _) => ResetSelected(s));
    }

    /// <summary>Sets the selection's rotation exactly, in radians; keyed straight away with auto key.</summary>
    public static void SetRotation(Entity session, float radians) =>
        Set(session, "ROTATION SET", transform => transform with { Rotation = radians });

    /// <summary>Sets the selection's scale exactly, in the pose or the rest transform.</summary>
    public static void SetScale(Entity session, Vector2 scale)
    {
        Vector2 clamped = new(
            Math.Clamp(scale.X, 0.01f, 20f),
            Math.Clamp(scale.Y, 0.01f, 20f));
        Set(session, "SCALE SET", transform => transform with { Scale = clamped });
    }

    /// <summary>
    /// Sets the selection's rotation while a slider is dragged: no status and no
    /// key yet, and no history step of its own; <see cref="FinishDrag"/> does those.
    /// </summary>
    public static void DragRotation(Entity session, float radians) =>
        AnimatorHistoryCommands.DuringGesture(session, () =>
            Set(session, "", transform => transform with { Rotation = radians }, quiet: true));

    /// <summary>
    /// Sets the selection's X scale while a slider is dragged, keeping each
    /// element's Y in proportion so a stretched bone stays stretched.
    /// </summary>
    public static void DragScale(Entity session, float scale) =>
        AnimatorHistoryCommands.DuringGesture(session, () =>
            Set(session, "", transform => transform with { Scale = Proportional(transform.Scale, scale) }, quiet: true));

    /// <summary>Sets the X scale once, as a slider click or reset does, keeping Y in proportion.</summary>
    public static void SetScaleProportional(Entity session, float scale) =>
        Set(session, "SCALE SET", transform => transform with { Scale = Proportional(transform.Scale, scale) });

    /// <summary>
    /// Applies a change to the selection with no status and no key, as a value
    /// field does while it is edited; <see cref="FinishDrag"/> keys and reports.
    /// </summary>
    public static void Quiet(Entity session, System.Func<RigTransform, RigTransform> change, bool posesFixed = true) =>
        Set(session, "", change, posesFixed, quiet: true);

    /// <summary>The selected bone's or part's transform in the mode being edited (rest or pose), if anything is selected.</summary>
    public static bool TryTransform(Entity session, out RigTransform transform)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        bool rest = AnimatorModes.Of(session).Rest;
        transform = RigTransform.Identity;

        if (selection.Bone >= 0 && selection.Bone < document.Package.Skeleton.Bones.Count)
        {
            transform = rest
                ? RigTransform.From(document.Package.Skeleton.Bones[selection.Bone].Rest)
                : document.Runtime.Pose.Bones[selection.Bone];
            return true;
        }

        if (selection.Attachment >= 0 && selection.Attachment < document.Package.Attachments.Count)
        {
            transform = rest
                ? RigTransform.From(document.Package.Attachments[selection.Attachment].Transform)
                : document.Runtime.Pose.Attachments[selection.Attachment];
            return true;
        }

        return false;
    }

    /// <summary>Keys and reports a finished slider drag; run inside <see cref="AnimatorHistoryCommands.EndGesture"/>.</summary>
    public static void FinishDrag(Entity session, string label)
    {
        string keyed = AnimatorModes.Of(session).Rest ||
                       AnimatorPlantCommands.AllHeldFacing(session, Bones(session, session.GetComponent<AnimatorSelection>()))
            ? ""
            : AnimatorEditing.KeyEdit(session);
        AnimatorSession.SetStatus(session, label + keyed);
    }

    private static Vector2 Proportional(Vector2 current, float x)
    {
        x = Math.Clamp(x, 0.01f, 20f);
        float ratio = Math.Abs(current.X) < 0.0001f ? 1f : current.Y / current.X;
        return new Vector2(x, Math.Clamp(x * ratio, 0.01f, 20f));
    }

    /// <summary>
    /// Sets the selection's local translation. Fixed-length bones can only be
    /// moved after turning MOVABLE on, so those are skipped and the status says
    /// why when nothing could move.
    /// </summary>
    public static void SetTranslation(Entity session, Vector2 translation) =>
        Set(session, "POSITION SET", transform => transform with { Translation = translation }, posesFixed: false);

    /// <summary>
    /// Applies <paramref name="change"/> to the selected bones, or else the
    /// selected attachment, or else the selected effect: to the rest transform
    /// in rest mode and to the pose otherwise. Unless <paramref name="posesFixed"/>,
    /// fixed-length bones keep their pose and are counted in the status instead.
    /// </summary>
    private static void Set(
        Entity session,
        string label,
        System.Func<RigTransform, RigTransform> change,
        bool posesFixed = true,
        bool quiet = false) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            AnimatorModes modes = AnimatorModes.Of(session);
            document.Runtime.SetPaused(true);

            int changed = 0;
            int fixedBones = 0;
            if (modes.Rest)
            {
                List<int> edited = Bones(session, selection);
                AnimatorRestPin.Pinned? pinned = AnimatorRestPin.Capture(session, edited);
                foreach (int index in edited)
                {
                    RigBone bone = document.Package.Skeleton.Bones[index];
                    bone.Rest = change(RigTransform.From(bone.Rest)).ToTransform();
                    changed++;
                }
                AnimatorRestPin.Restore(session, pinned);
            }
            else
            {
                foreach (int index in Bones(session, selection))
                {
                    if (!posesFixed && !document.Package.Skeleton.Bones[index].CanTranslate)
                    {
                        fixedBones++;
                        continue;
                    }

                    ref RigTransform pose = ref document.Runtime.Pose.Bones[index];
                    pose = change(pose);
                    changed++;
                }

                // A pinned foot or hand turned here turns its pin, which owns its keys.
                AnimatorPlantCommands.Reface(session, Bones(session, selection));
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                if (modes.Rest)
                {
                    RigAttachment attachment = document.Package.Attachments[selection.Attachment];
                    attachment.Transform = change(RigTransform.From(attachment.Transform)).ToTransform();
                }
                else
                {
                    ref RigTransform pose = ref document.Runtime.Pose.Attachments[selection.Attachment];
                    pose = change(pose);
                }
                changed = 1;
            }

            if (quiet)
                return;

            if (changed == 0)
            {
                if (fixedBones > 0)
                {
                    string name = document.Package.Skeleton.Bones[selection.Bone].Id;
                    AnimatorSession.SetStatus(session, $"{name} IS FIXED LENGTH — USE MOVABLE");
                }
                return;
            }

            string keyed = modes.Rest || AnimatorPlantCommands.AllHeldFacing(session, Bones(session, selection))
                ? ""
                : AnimatorEditing.KeyEdit(session);
            AnimatorSession.SetStatus(
                session,
                (fixedBones > 0
                    ? $"{label} ({fixedBones} FIXED SKIPPED)"
                    : changed == 1
                        ? label
                        : $"{label} ON {changed} BONES") + keyed);
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
            AnimatorModes modes = AnimatorModes.Of(session);
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
                RigBone bone = document.Package.Skeleton.Bones[index];
                document.Runtime.Pose.Bones[index] = RigTransform.From(bone.Rest);
                name = bone.Id;
                changed++;
            }

            if (changed == 0 && selection.Attachment >= 0)
            {
                RigAttachment attachment = document.Package.Attachments[selection.Attachment];
                document.Runtime.Pose.Attachments[selection.Attachment] =
                    RigTransform.From(attachment.Transform);
                name = attachment.Id;
                changed = 1;
            }

            if (changed == 0)
            {
                AnimatorSession.SetStatus(session, "SELECT A BONE OR PART FIRST");
                return;
            }

            string keyed = modes.Rest ? "" : AnimatorEditing.KeyEdit(session);
            AnimatorSession.SetStatus(
                session,
                (changed == 1 ? $"RESET {name} TO REST" : $"RESET {changed} BONES TO REST") + keyed);
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
