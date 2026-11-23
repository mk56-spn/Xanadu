using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Keys;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>Timeline key and track selection, retiming and removal.</summary>
public static class AnimatorKeyCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<SetEase>((s, e) => SetInterpolation(s, e.Ease));
        session.On<DeleteKey>((s, _) => DeleteSelected(s));
    }

    /// <summary>Selects the bone, attachment or effect a track animates.</summary>
    public static void SelectTrack(Entity session, AnimatorTrackRow row)
    {
        RigPackage package = session.GetComponent<AnimatorDocument>().Package;
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        if (row.Target == RigChannelTarget.Bone)
        {
            int bone = package.Skeleton.Bones.FindIndex(candidate => candidate.Id == row.Id);
            if (bone < 0)
                return;
            selection.SelectOnlyBone(bone);
        }
        else
        {
            int attachment = package.Attachments.FindIndex(candidate => candidate.Id == row.Id);
            if (attachment < 0)
                return;
            selection.SelectOnlyAttachment(attachment);
        }

        AnimatorSession.SetStatus(session, $"SELECTED {row.Id}");
    }

    public static void SelectTrack(Entity session, RigTrack track) =>
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

    public static void SelectKey(Entity session, RigTrack track, float time) =>
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

    public static void ToggleKey(Entity session, RigTrack track, float time) =>
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

    public static void MakePrimary(Entity session, RigTrack track, float time) =>
        MakePrimary(session, new AnimatorTrackRow(track), time);

    /// <summary>Whether a key on a row is in the selection, as the primary key or beside it.</summary>
    public static bool IsSelected(Entity session, AnimatorTrackRow row, float time) =>
        session.GetComponent<AnimatorSelection>().IsKeySelected(row, time);

    public static bool IsSelected(Entity session, RigTrack track, float time) =>
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
        selection.Extra = keys.Count > 1 ? [.. keys.Skip(1)] : null;
    }

    /// <summary>The clip and the key selection as a key drag began.</summary>
    public sealed record KeyDragOrigin(RigClip Clip, List<AnimatorKeyRef> Keys);

    /// <summary>Captures what a key drag starts from, or null when no key is selected.</summary>
    public static KeyDragOrigin? BeginKeyDrag(Entity session)
    {
        RigClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        List<AnimatorKeyRef> keys = session.GetComponent<AnimatorSelection>().Keys();
        return clip is null || keys.Count == 0
            ? null
            : new KeyDragOrigin(RigPackageCopy.Clone(clip), keys);
    }

    /// <summary>
    /// Moves the primary key set to a time and the rest of the selection with
    /// it, by the same amount, stopping where the earliest or latest key
    /// reaches the clip's ends. The playhead follows the primary key.
    /// </summary>
    public static void RetimeSelected(Entity session, float seconds) =>
        RetimeSelected(session, seconds, null);

    /// <summary>
    /// As <see cref="RetimeSelected(Entity, float)"/>, but measured from where
    /// the drag began: the clip is put back first, so keys the drag passed
    /// over on the way are neither overwritten nor picked up. Only the keys at
    /// the drop time are replaced.
    /// </summary>
    public static void RetimeSelected(Entity session, float seconds, KeyDragOrigin? origin)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        RigClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return;

        if (origin is not null)
        {
            Restore(clip, origin.Clip);
            Assign(ref selection, origin.Keys);
        }

        if (!selection.HasKey)
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
                ? RigAuthoring.MoveKey(
                    clip, key.Target, key.Id, property, key.Time, moved[index].Time)
                : RigAuthoring.MoveKeysAt(
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

    /// <summary>
    /// Puts a clip's channels back to a copy's keys. Channels that still exist
    /// keep their identity, so nothing holding one goes stale.
    /// </summary>
    private static void Restore(RigClip clip, RigClip origin)
    {
        var channels = new List<KeyChannel>();
        foreach (KeyChannel saved in RigPackageCopy.Clone(origin).Channels)
        {
            KeyChannel? live = clip.Channels.Find(candidate =>
                candidate.Target == saved.Target &&
                candidate.TargetId == saved.TargetId &&
                candidate.Property == saved.Property);
            if (live is null)
            {
                channels.Add(saved);
                continue;
            }

            live.Keys = saved.Keys;
            channels.Add(live);
        }

        clip.Channels.Clear();
        clip.Channels.AddRange(channels);
    }

    /// <summary>Sets how the selected keys ease into their next key.</summary>
    public static void SetInterpolation(Entity session, EasePreset preset) =>
        AnimatorHistoryCommands.Edit(session, () =>
        {
            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            RigClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
            if (clip is null || !selection.HasKey)
            {
                AnimatorSession.SetStatus(session, "SELECT A KEY ON THE TIMELINE FIRST");
                return;
            }

            foreach (AnimatorKeyRef key in selection.Keys())
            {
                if (key.Property is { } property)
                {
                    RigEasing.Apply(
                        clip, key.Target, key.Id, property, key.Time, preset);
                }
                else
                {
                    RigEasing.Apply(clip, key.Target, key.Id, key.Time, preset);
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

    public static void AddKeyAt(Entity session, RigTrack track, float seconds) =>
        AddKeyAt(session, new AnimatorTrackRow(track), seconds);

    private static void AddKeyAtCore(Entity session, AnimatorTrackRow row, float seconds)
    {
        RigClip? clip = AnimatorSession.CurrentClip(session.GetComponent<AnimatorDocument>());
        if (clip is null)
            return;

        float time = Math.Clamp(seconds, 0f, clip.Duration);
        if (AnimatorTrackRows.KeyTimes(clip, row)
            .Exists(key => Math.Abs(key - time) <= RigTracks.TimeTolerance))
        {
            SelectKey(session, row, time);
            AnimatorSession.SetStatus(session, "KEY ALREADY HERE");
            return;
        }

        // A field with no keys yet (the full view lists them): its first key holds the pose as it is now.
        if (row.Property is { } unkeyed && clip.Find(RigChannels.At(row.Target, row.Id, unkeyed)) is not { Keys.Count: > 0 })
        {
            if (PoseValue(session.GetComponent<AnimatorDocument>(), row.Target, row.Id, unkeyed) is not { } value)
            {
                AnimatorSession.SetStatus(session, "KEY THIS FIELD FROM THE SELECTED CARD");
                return;
            }

            bool step = unkeyed is RigChannelProperty.Visible or RigChannelProperty.Art or RigChannelProperty.Layer;
            RigAuthoring.SetChannel(clip, row.Target, row.Id, unkeyed, time, value, step ? KeyEase.Step : KeyEase.Linear);
            SelectKey(session, row, time);
            AnimatorSession.SetStatus(session, $"KEY ADDED AT {time:0.00}s");
            return;
        }

        foreach (KeyChannel channel in clip.Channels.ToArray())
        {
            if (channel.Target != row.Target || channel.TargetId != row.Id)
                continue;
            if (row.Property is { } property && channel.Property != property)
                continue;

            // Visibility is held, so a new key keeps what shows there and stays a switch.
            bool held = channel.Property == RigChannelProperty.Visible;
            RigAuthoring.SetChannel(
                clip,
                row.Target,
                row.Id,
                channel.Property,
                time,
                held ? channel.SampleHeld(time) : channel.Sample(time),
                held ? KeyEase.Step : InterpolationBefore(channel, time));
        }

        SelectKey(session, row, time);
        AnimatorSession.SetStatus(session, $"KEY ADDED AT {time:0.00}s");
    }

    /// <summary>A field's value in the pose on screen, or null for one the pose does not hold.</summary>
    private static float? PoseValue(in AnimatorDocument document, RigChannelTarget target, string id, RigChannelProperty property)
    {
        RigPose pose = document.Runtime.Pose;
        if (target == RigChannelTarget.Bone)
        {
            int bone = RigRig.FindBone(document.Package, id);
            return bone < 0 || bone >= pose.Bones.Length ? null : Transform(pose.Bones[bone], property);
        }

        int part = document.Package.Attachments.FindIndex(attachment => attachment.Id == id);
        if (part < 0 || part >= pose.Attachments.Length)
            return null;
        RigAttachmentState state = pose.AttachmentStates[part];
        return property switch
        {
            RigChannelProperty.Opacity => state.HasOpacity ? state.Opacity : 1f,
            RigChannelProperty.Visible => state.Hidden ? 0f : 1f,
            RigChannelProperty.Art => state.Art,
            RigChannelProperty.Layer => state.LayerOf(document.Package.Attachments[part]),
            _ => Transform(pose.Attachments[part], property),
        };
    }

    private static float? Transform(in RigTransform transform, RigChannelProperty property) => property switch
    {
        RigChannelProperty.TranslationX => transform.Translation.X,
        RigChannelProperty.TranslationY => transform.Translation.Y,
        RigChannelProperty.Rotation => transform.Rotation,
        RigChannelProperty.ScaleX => transform.Scale.X,
        RigChannelProperty.ScaleY => transform.Scale.Y,
        RigChannelProperty.Depth => transform.Depth,
        _ => null,
    };

    /// <summary>How the segment a new key lands in was easing, so splitting it keeps its character.</summary>
    private static KeyEase InterpolationBefore(KeyChannel channel, float time)
    {
        KeyEase interpolation = KeyEase.Linear;
        foreach (Keyframe key in channel.Keys)
        {
            if (key.Time >= time)
                break;
            interpolation = key.Ease;
        }
        return interpolation;
    }

    public static void DeleteSelected(Entity session) =>
        AnimatorHistoryCommands.Edit(session, () => DeleteSelectedCore(session));

    private static void DeleteSelectedCore(Entity session)
    {
        ref AnimatorDocument document = ref session.GetComponent<AnimatorDocument>();
        ref AnimatorSelection selection = ref session.GetComponent<AnimatorSelection>();
        RigClip? clip = AnimatorSession.CurrentClip(document);
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
                RigAuthoring.RemoveKey(
                    clip, key.Target, key.Id, property, key.Time);
            }
            else
            {
                RigAuthoring.RemoveKeysAt(clip, key.Target, key.Id, key.Time);
            }
        }
        selection.HasKey = false;
        AnimatorSession.SetStatus(session, keys.Count == 1 ? "KEY REMOVED" : $"{keys.Count} KEYS REMOVED");
    }
}
