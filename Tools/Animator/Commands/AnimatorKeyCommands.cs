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
    public static void SelectTrack(Entity session, AnimatorTrackRow row)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        if (row.Target == XafChannelTarget.Bone)
        {
            int bone = package.Skeleton.Bones.FindIndex(candidate => candidate.Id == row.Id);
            if (bone < 0)
                return;
            selection.SelectOnlyBone(bone);
        }
        else
        {
            int attachment = package.Attachments.FindIndex(candidate => candidate.Group == row.Id);
            if (attachment < 0)
                return;
            selection.SelectOnlyAttachment(attachment);
        }

        AnimatorSession.SetStatus(session, $"SELECTED {row.Id}");
    }

    public static void SelectTrack(Entity session, XafTrack track) =>
        SelectTrack(session, new AnimatorTrackRow(track));

    /// <summary>Selects one row's key at a time and puts the playhead there.</summary>
    public static void SelectKey(Entity session, AnimatorTrackRow row, float time)
    {
        SelectTrack(session, row);
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        Assign(ref selection, new[] { AnimatorTrackRows.Ref(row, time) });

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(time);
        runtime.SetPaused(true);
        AnimatorSession.SetStatus(session, "KEY SELECTED • DRAG TO RETIME • DELETE TO REMOVE");
    }

    public static void SelectKey(Entity session, XafTrack track, float time) =>
        SelectKey(session, new AnimatorTrackRow(track), time);

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
    /// Adds a key to the selection or takes it out, as Shift-clicking does.
    /// Removing the primary key promotes the next one instead of clearing the
    /// whole selection.
    /// </summary>
    public static void ToggleKey(Entity session, AnimatorTrackRow row, float time)
    {
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        List<AnimatorKeyRef> keys = selection.Keys();
        int index = keys.FindIndex(key => key.Is(row, time));
        if (index >= 0)
            keys.RemoveAt(index);
        else
            keys.Add(AnimatorTrackRows.Ref(row, time));

        Assign(ref selection, keys);
        if (keys.Count == 0)
        {
            AnimatorSession.SetStatus(session, "NO KEY SELECTED");
            return;
        }

        AnimatorSession.SetStatus(session, $"{keys.Count} KEYS SELECTED");
    }

    public static void ToggleKey(Entity session, XafTrack track, float time) =>
        ToggleKey(session, new AnimatorTrackRow(track), time);

    /// <summary>
    /// Makes one key of the selection the primary one, so a drag started on
    /// it moves the whole group from that key. A key outside the selection
    /// replaces the selection instead.
    /// </summary>
    public static void MakePrimary(Entity session, AnimatorTrackRow row, float time)
    {
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        List<AnimatorKeyRef> keys = selection.Keys();
        int index = keys.FindIndex(key => key.Is(row, time));
        if (index < 0)
        {
            SelectKey(session, row, time);
            return;
        }

        (keys[0], keys[index]) = (keys[index], keys[0]);
        Assign(ref selection, keys);
        SelectTrack(session, row);
        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        runtime.Seek(keys[0].Time);
        runtime.SetPaused(true);
    }

    public static void MakePrimary(Entity session, XafTrack track, float time) =>
        MakePrimary(session, new AnimatorTrackRow(track), time);

    /// <summary>Whether a key on a row is in the selection, as the primary key or beside it.</summary>
    public static bool IsSelected(Entity session, AnimatorTrackRow row, float time) =>
        session.GetComponent<AnimatorSelection>().IsKeySelected(row, time);

    public static bool IsSelected(Entity session, XafTrack track, float time) =>
        IsSelected(session, new AnimatorTrackRow(track), time);

    /// <summary>Writes a key list into the selection: the first is the primary key, the rest go in <see cref="AnimatorSelection.Extra"/>.</summary>
    private static void Assign(ref AnimatorSelection selection, IReadOnlyList<AnimatorKeyRef> keys)
    {
        selection.HasKey = false;
        if (keys.Count == 0)
            return;

        selection.HasKey = true;
        selection.KeyTarget = keys[0].Target;
        selection.KeyId = keys[0].Id;
        selection.KeyProperty = keys[0].Property;
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
            AnimatorKeyRef key = keys[index];
            any |= key.Property is { } property
                ? XafAuthoring.MoveKey(
                    clip, key.Target, key.Id, property, key.Time, moved[index].Time)
                : XafAuthoring.MoveKeysAt(
                    clip, key.Target, key.Id, key.Time, moved[index].Time);
        }

        if (!any)
            return;

        // The authoring calls clamp to the clip, so keep the stored times the same.
        moved = moved.ConvertAll(key => key with { Time = Math.Clamp(key.Time, 0f, clip.Duration) });
        Assign(ref selection, moved);
        document.Runtime.Seek(moved[0].Time);
        AnimatorSession.SetStatus(
            session,
            moved.Count == 1 ? $"KEY {moved[0].Time:0.00}s" : $"{moved.Count} KEYS  {moved[0].Time:0.00}s");
    }

    /// <summary>Sets how the selected keys ease into their next key.</summary>
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
            {
                if (key.Property is { } property)
                {
                    XafEasing.Apply(
                        clip, key.Target, key.Id, property, key.Time, preset);
                }
                else
                {
                    XafEasing.Apply(clip, key.Target, key.Id, key.Time, preset);
                }
            }
            AnimatorSession.SetStatus(session, $"KEY {preset.ToString().ToUpperInvariant()}");
        });

    /// <summary>
    /// Inserts a key on a row at a time. A channel row keys only that channel;
    /// a target row keys every channel it already animates. Either way the new
    /// key holds the value the clip has there, so the motion does not jump.
    /// </summary>
    public static void AddKeyAt(Entity session, AnimatorTrackRow row, float seconds) =>
        AnimatorHistoryCommands.Edit(session, () => AddKeyAtCore(session, row, seconds));

    public static void AddKeyAt(Entity session, XafTrack track, float seconds) =>
        AddKeyAt(session, new AnimatorTrackRow(track), seconds);

    private static void AddKeyAtCore(Entity session, AnimatorTrackRow row, float seconds)
    {
        XafClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null)
            return;

        float time = Math.Clamp(seconds, 0f, clip.Duration);
        if (AnimatorTrackRows.KeyTimes(clip, row)
            .Exists(key => Math.Abs(key - time) <= XafTracks.TimeTolerance))
        {
            SelectKey(session, row, time);
            AnimatorSession.SetStatus(session, "KEY ALREADY HERE");
            return;
        }

        foreach (XafChannel channel in clip.Channels.ToArray())
        {
            if (channel.Target != row.Target || channel.TargetId != row.Id)
                continue;
            if (row.Property is { } property && channel.Property != property)
                continue;

            XafAuthoring.SetChannel(
                clip,
                row.Target,
                row.Id,
                channel.Property,
                time,
                XafSampler.SampleChannel(channel, time),
                InterpolationBefore(channel, time));
        }

        SelectKey(session, row, time);
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
        {
            if (key.Property is { } property)
            {
                XafAuthoring.RemoveKey(
                    clip, key.Target, key.Id, property, key.Time);
            }
            else
            {
                XafAuthoring.RemoveKeysAt(clip, key.Target, key.Id, key.Time);
            }
        }
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, keys.Count == 1 ? "KEY REMOVED" : $"{keys.Count} KEYS REMOVED");
    }
}
