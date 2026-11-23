using System;
using System.Collections.Generic;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Animation;

/// <summary>Small mutation helpers used by the animator authoring tool.</summary>
public static class RigAuthoring
{
    public static bool SetBoneProperty(
        RigPackage package,
        RigClip clip,
        string boneId,
        RigChannelProperty property,
        float time,
        float value,
        KeyEase interpolation = KeyEase.Linear)
    {
        if (!package.Skeleton.Bones.Exists(bone => bone.Id == boneId))
            return false;

        return SetChannel(
            clip,
            RigChannelTarget.Bone,
            boneId,
            property,
            time,
            value,
            interpolation);
    }

    public static bool SetAttachmentProperty(
        RigPackage package,
        RigClip clip,
        string group,
        RigChannelProperty property,
        float time,
        float value,
        KeyEase interpolation = KeyEase.Linear)
    {
        if (!package.Attachments.Exists(attachment => attachment.Id == group))
            return false;

        return SetChannel(
            clip,
            RigChannelTarget.Attachment,
            group,
            property,
            time,
            value,
            interpolation);
    }

    public static bool SetChannel(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float time,
        float value,
        KeyEase interpolation = KeyEase.Linear)
    {
        KeyChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);

        if (channel is null)
        {
            channel = RigChannels.New(target, targetId, property);
            clip.Channels.Add(channel);
        }

        float clamped = Math.Clamp(time, 0f, clip.Duration);
        Keyframe? existing = channel.Keys.Find(key =>
            Math.Abs(key.Time - clamped) < 0.0001f);
        if (existing is not null)
        {
            existing.Value = value;
            existing.Ease = interpolation;
            return true;
        }

        var key = new Keyframe
        {
            Time = clamped,
            Value = value,
            Ease = interpolation,
        };
        int index = channel.Keys.FindIndex(candidate =>
            candidate.Time > clamped);
        if (index < 0)
            channel.Keys.Add(key);
        else
            channel.Keys.Insert(index, key);
        return true;
    }

    /// <summary>
    /// Sets a channel key with its exact curve, including the tangents a
    /// copy/paste keeps. Existing keys at the same time are replaced.
    /// </summary>
    public static bool SetKeyCurve(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float time,
        float value,
        KeyEase interpolation,
        float tangentIn,
        float tangentOut)
    {
        if (!SetChannel(clip, target, targetId, property, time, value, interpolation))
            return false;

        KeyChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);
        Keyframe? key = channel?.Keys.Find(candidate =>
            Math.Abs(candidate.Time - Math.Clamp(time, 0f, clip.Duration)) < 0.0001f);
        if (key is null)
            return false;

        key.TangentIn = tangentIn;
        key.TangentOut = tangentOut;
        return true;
    }

    public static bool RemoveKey(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float time,
        float tolerance = 0.0001f)
    {
        KeyChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);
        if (channel is null)
            return false;

        int index = channel.Keys.FindIndex(key =>
            Math.Abs(key.Time - time) <= tolerance);
        if (index < 0)
            return false;

        channel.Keys.RemoveAt(index);
        if (channel.Keys.Count == 0)
            clip.Channels.Remove(channel);
        return true;
    }

    public static bool MoveKey(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float fromTime,
        float toTime,
        float tolerance = 0.0001f)
    {
        KeyChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);
        if (channel is null)
            return false;

        int index = channel.Keys.FindIndex(key =>
            Math.Abs(key.Time - fromTime) <= tolerance);
        if (index < 0)
            return false;

        Keyframe key = channel.Keys[index];
        channel.Keys.RemoveAt(index);
        key.Time = Math.Clamp(toTime, 0f, clip.Duration);
        channel.Keys.RemoveAll(other =>
            Math.Abs(other.Time - key.Time) <= tolerance);

        int insert = channel.Keys.FindIndex(candidate =>
            candidate.Time > key.Time);
        if (insert < 0)
            channel.Keys.Add(key);
        else
            channel.Keys.Insert(insert, key);
        return true;
    }

    /// <summary>
    /// Moves every channel key of a target at one time to another time, as a
    /// pose key. A key already at the destination is replaced.
    /// </summary>
    public static bool MoveKeysAt(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        float fromTime,
        float toTime,
        float tolerance = RigTracks.TimeTolerance)
    {
        float destination = Math.Clamp(toTime, 0f, clip.Duration);
        bool moved = false;
        foreach (KeyChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            int index = channel.Keys.FindIndex(key =>
                Math.Abs(key.Time - fromTime) <= tolerance);
            if (index < 0)
                continue;

            Keyframe key = channel.Keys[index];
            channel.Keys.RemoveAt(index);
            channel.Keys.RemoveAll(other =>
                Math.Abs(other.Time - destination) <= tolerance);

            key.Time = destination;
            int insert = channel.Keys.FindIndex(other => other.Time > destination);
            if (insert < 0)
                channel.Keys.Add(key);
            else
                channel.Keys.Insert(insert, key);
            moved = true;
        }

        return moved;
    }

    /// <summary>Removes every channel key of a target at one time.</summary>
    public static bool RemoveKeysAt(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        float time,
        float tolerance = RigTracks.TimeTolerance)
    {
        bool removed = false;
        foreach (KeyChannel channel in clip.Channels.ToArray())
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            removed |= RemoveKey(clip, target, targetId, channel.Property, time, tolerance);
        }

        return removed;
    }

    public static RigClip? FindClip(RigPackage package, string id) =>
        package.Clips.Find(clip => clip.Id == id);

    public static void KeyAttachmentTransform(
        RigPackage package,
        RigClip clip,
        int attachmentIndex,
        float time,
        RigTransform transform)
    {
        if (attachmentIndex < 0 || attachmentIndex >= package.Attachments.Count)
            return;

        string group = package.Attachments[attachmentIndex].Id;
        SetAttachmentProperty(package, clip, group,
            RigChannelProperty.TranslationX, time, transform.Translation.X);
        SetAttachmentProperty(package, clip, group,
            RigChannelProperty.TranslationY, time, transform.Translation.Y);
        SetAttachmentProperty(package, clip, group,
            RigChannelProperty.Rotation, time, transform.Rotation);
        SetAttachmentProperty(package, clip, group,
            RigChannelProperty.ScaleX, time, transform.Scale.X);
        SetAttachmentProperty(package, clip, group,
            RigChannelProperty.ScaleY, time, transform.Scale.Y);
    }

    /// <summary>Keys a part's opacity at a time.</summary>
    public static void KeyOpacity(RigPackage package, RigClip clip, int attachmentIndex, float time, float opacity)
    {
        if (attachmentIndex < 0 || attachmentIndex >= package.Attachments.Count)
            return;

        SetChannel(clip, RigChannelTarget.Attachment, package.Attachments[attachmentIndex].Id,
            RigChannelProperty.Opacity, time, Math.Clamp(opacity, 0f, 1f));
    }

    /// <summary>Keys a part shown or hidden from a time on, as a held key.</summary>
    public static void KeyVisible(RigPackage package, RigClip clip, int attachmentIndex, float time, bool visible)
    {
        if (attachmentIndex < 0 || attachmentIndex >= package.Attachments.Count)
            return;

        SetChannel(clip, RigChannelTarget.Attachment, package.Attachments[attachmentIndex].Id,
            RigChannelProperty.Visible, time, visible ? 1f : 0f, KeyEase.Step);
    }

    public static void KeyBoneTransform(
        RigPackage package,
        RigClip clip,
        int boneIndex,
        float time,
        RigTransform transform)
    {
        RigBone bone = package.Skeleton.Bones[boneIndex];
        if (bone.CanTranslate)
        {
            SetBoneProperty(package, clip, bone.Id,
                RigChannelProperty.TranslationX, time, transform.Translation.X);
            SetBoneProperty(package, clip, bone.Id,
                RigChannelProperty.TranslationY, time, transform.Translation.Y);
        }
        SetBoneProperty(package, clip, bone.Id,
            RigChannelProperty.Rotation, time, transform.Rotation);
        SetBoneProperty(package, clip, bone.Id,
            RigChannelProperty.ScaleX, time, transform.Scale.X);
        SetBoneProperty(package, clip, bone.Id,
            RigChannelProperty.ScaleY, time, transform.Scale.Y);

        // Depth only once a joint has some, or the clip already moves it, so
        // keying a pose does not litter every bone with a flat depth channel.
        if (transform.Depth != 0f || clip.Channels.Exists(channel =>
                channel.Target == RigChannelTarget.Bone && channel.TargetId == bone.Id &&
                channel.Property == RigChannelProperty.Depth))
        {
            SetBoneProperty(package, clip, bone.Id, RigChannelProperty.Depth, time, transform.Depth);
        }
    }
}
