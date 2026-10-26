using System;
using System.Collections.Generic;
using System.Linq;
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
        Assign(ref selection, new[] { new AnimatorKeyRef(track.Target, track.Id, time) });

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(time);
        runtime.SetPaused(true);
        AnimatorSession.SetStatus(session, "KEY SELECTED • DRAG TO RETIME • DELETE TO REMOVE");
    }

    /// <summary>
    /// Selects a group of keys, such as the ones a box covers. The first is
    /// the primary key; the playhead and the bone selection stay where they are.
    /// </summary>
    public static void SelectKeys(Entity session, IReadOnlyList<AnimatorKeyRef> keys)
    {
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        Assign(ref selection, keys);
        if (keys.Count > 1)
        {
            AnimatorSession.SetStatus(
                session,
                $"{keys.Count} KEYS SELECTED • DRAG TO MOVE • DELETE TO REMOVE");
        }
        else if (keys.Count == 1)
        {
            AnimatorSession.SetStatus(session, "KEY SELECTED • DRAG TO RETIME • DELETE TO REMOVE");
        }
    }

    /// <summary>
    /// Makes one key of the selection the primary one, so a drag started on
    /// it moves the whole group from that key. A key outside the selection
    /// replaces the selection instead.
    /// </summary>
    public static void MakePrimary(Entity session, XafTrack track, float time)
    {
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        List<AnimatorKeyRef> keys = selection.Keys();
        int index = keys.FindIndex(key => key.Is(track, time));
        if (index < 0)
        {
            SelectKey(session, track, time);
            return;
        }

        (keys[0], keys[index]) = (keys[index], keys[0]);
        Assign(ref selection, keys);
        SelectTrack(session, track);
        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(keys[0].Time);
        runtime.SetPaused(true);
    }

    /// <summary>Whether a key set is in the selection, as the primary key or beside it.</summary>
    public static bool IsSelected(Entity session, XafTrack track, float time) =>
        session.GetComponent<AnimatorSelection>().IsKeySelected(track, time);

    /// <summary>Writes a key list into the selection: the first is the primary key, the rest go in <see cref="AnimatorSelection.Extra"/>.</summary>
    private static void Assign(ref AnimatorSelection selection, IReadOnlyList<AnimatorKeyRef> keys)
    {
        selection.HasKey = false;
        if (keys.Count == 0)
            return;

        selection.HasKey = true;
        selection.KeyTarget = keys[0].Target;
        selection.KeyId = keys[0].Id;
        selection.KeyTime = keys[0].Time;
        selection.Extra = keys.Count > 1 ? new List<AnimatorKeyRef>(keys.Skip(1)) : null;
    }

    /// <summary>
    /// Moves the primary key set to a time and the rest of the selection with
    /// it, by the same amount, stopping where the earliest or latest key
    /// reaches the clip's ends. The playhead follows the primary key.
    /// </summary>
    public static void RetimeSelected(Entity session, float seconds)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null || !selection.HasKey)
            return;

        List<AnimatorKeyRef> keys = selection.Keys();
        float delta = Math.Clamp(seconds, 0f, clip.Duration) - selection.KeyTime;
        delta = Math.Clamp(delta, -keys.Min(key => key.Time), clip.Duration - keys.Max(key => key.Time));
        List<AnimatorKeyRef> moved = keys.ConvertAll(key => key with { Time = key.Time + delta });

        // Furthest first, so a key never lands on one that has yet to move.
        bool any = false;
        foreach (int index in Enumerable.Range(0, keys.Count)
                     .OrderBy(i => delta > 0 ? -keys[i].Time : keys[i].Time))
        {
            any |= XafAuthoring.MoveKeysAt(clip, keys[index].Target, keys[index].Id, keys[index].Time, moved[index].Time);
        }

        if (!any)
            return;

        // MoveKeysAt clamps to the clip, so keep the stored times the same.
        moved = moved.ConvertAll(key => key with { Time = Math.Clamp(key.Time, 0f, clip.Duration) });
        Assign(ref selection, moved);
        document.Runtime.Seek(moved[0].Time);
        AnimatorSession.SetStatus(
            session,
            moved.Count == 1 ? $"KEY {moved[0].Time:0.00}s" : $"{moved.Count} KEYS  {moved[0].Time:0.00}s");
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

            foreach (AnimatorKeyRef key in selection.Keys())
                XafEasing.Apply(clip, key.Target, key.Id, key.Time, preset);
            AnimatorSession.SetStatus(session, $"KEY {preset.ToString().ToUpperInvariant()}");
        });

    /// <summary>
    /// Inserts a key on a track at a time: every channel the track already
    /// animates gets a key holding the value the clip has there, so the motion
    /// is unchanged until the new key is edited. The new key is selected.
    /// </summary>
    public static void AddKeyAt(Entity session, XafTrack track, float seconds) =>
        AnimatorHistoryCommands.Edit(session, () => AddKeyAtCore(session, track, seconds));

    private static void AddKeyAtCore(Entity session, XafTrack track, float seconds)
    {
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null)
            return;

        float time = Math.Clamp(seconds, 0f, clip.Duration);
        if (XafTracks.KeyTimes(clip, track).Exists(key => Math.Abs(key - time) <= XafTracks.TimeTolerance))
        {
            SelectKey(session, track, time);
            AnimatorSession.SetStatus(session, "KEY ALREADY HERE");
            return;
        }

        foreach (XafChannel channel in clip.Channels.ToArray())
        {
            if (channel.Target != track.Target || channel.TargetId != track.Id)
                continue;

            XafAuthoring.SetChannel(
                clip,
                track.Target,
                track.Id,
                channel.Property,
                time,
                XafSampler.SampleChannel(channel, time),
                InterpolationBefore(channel, time));
        }

        SelectKey(session, track, time);
        AnimatorSession.SetStatus(session, $"KEY ADDED AT {time:0.00}s");
    }

    /// <summary>How the segment a new key lands in was easing, so splitting it keeps its character.</summary>
    private static XafInterpolation InterpolationBefore(XafChannel channel, float time)
    {
        XafInterpolation interpolation = XafInterpolation.Linear;
        foreach (XafKeyframe key in channel.Keyframes)
        {
            if (key.Time >= time)
                break;
            interpolation = key.Interpolation;
        }
        return interpolation;
    }

    public static void DeleteSelected(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteSelectedCore(session));

    private static void DeleteSelectedCore(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return;

        if (!selection.HasKey)
        {
            AnimatorSession.SetStatus(session, "NO KEY SELECTED");
            return;
        }

        List<AnimatorKeyRef> keys = selection.Keys();
        foreach (AnimatorKeyRef key in keys)
            XafAuthoring.RemoveKeysAt(clip, key.Target, key.Id, key.Time);
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, keys.Count == 1 ? "KEY REMOVED" : $"{keys.Count} KEYS REMOVED");
    }
}
