using System;
using Friflo.Engine.ECS;
using Writer.Animation;

namespace Writer.Tools.Animator.Commands;

/// <summary>Timeline key and track selection, retiming and removal.</summary>
public static class AnimatorKeyCommands
{
    /// <summary>Selects the bone or attachment a track animates.</summary>
    public static void SelectTrack(Entity session, XafTrack track)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        if (track.Target == XafChannelTarget.Bone)
        {
            int bone = package.Skeleton.Bones.FindIndex(candidate => candidate.Id == track.Id);
            if (bone < 0)
                return;
            selection.Bone = bone;
            selection.Attachment = -1;
        }
        else
        {
            int attachment = package.Attachments.FindIndex(candidate => candidate.Group == track.Id);
            if (attachment < 0)
                return;
            selection.Attachment = attachment;
            selection.Bone = -1;
        }

        AnimatorSession.SetStatus(session, $"SELECTED {track.Id}");
    }

    /// <summary>Selects one target's keys at a time and puts the playhead there.</summary>
    public static void SelectKey(Entity session, XafTrack track, float time)
    {
        SelectTrack(session, track);
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        selection.HasKey = true;
        selection.KeyTarget = track.Target;
        selection.KeyId = track.Id;
        selection.KeyTime = time;

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(time);
        runtime.SetPaused(true);
        AnimatorSession.SetStatus(session, "KEY SELECTED • DRAG TO RETIME • DELETE TO REMOVE");
    }

    /// <summary>Moves the selected key set to a time; the playhead follows it.</summary>
    public static void RetimeSelected(Entity session, float seconds)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null || !selection.HasKey)
            return;

        float target = Math.Clamp(seconds, 0f, clip.Duration);
        if (!XafAuthoring.MoveKeysAt(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime, target))
            return;

        selection.KeyTime = target;
        document.Runtime.Seek(target);
        AnimatorSession.SetStatus(session, $"KEY {target:0.00}s");
    }

    /// <summary>Sets how the selected key set eases into the next key.</summary>
    public static void SetInterpolation(Entity session, XafEasePreset preset) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
            if (clip is null || !selection.HasKey)
            {
                AnimatorSession.SetStatus(session, "SELECT A KEY ON THE TIMELINE FIRST");
                return;
            }

            XafEasing.Apply(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime, preset);
            AnimatorSession.SetStatus(session, $"KEY {preset.ToString().ToUpperInvariant()}");
        });

    public static void DeleteSelected(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteSelectedCore(session));

    private static void DeleteSelectedCore(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null || !selection.HasKey)
            return;

        XafAuthoring.RemoveKeysAt(clip, selection.KeyTarget, selection.KeyId, selection.KeyTime);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, "KEY REMOVED");
    }
}
