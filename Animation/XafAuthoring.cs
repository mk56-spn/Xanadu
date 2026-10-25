using System;
using System.Collections.Generic;
using Godot;

namespace Writer.Animation;

/// <summary>Small mutation helpers used by the animator authoring tool.</summary>
public static class XafAuthoring
{
    public static bool SetBoneProperty(
        XafPackage package,
        XafClip clip,
        string boneId,
        XafChannelProperty property,
        float time,
        float value,
        XafInterpolation interpolation = XafInterpolation.Linear)
    {
        if (!package.Skeleton.Bones.Exists(bone => bone.Id == boneId))
            return false;

        return SetChannel(
            clip,
            XafChannelTarget.Bone,
            boneId,
            property,
            time,
            value,
            interpolation);
    }

    public static bool SetAttachmentProperty(
        XafPackage package,
        XafClip clip,
        string group,
        XafChannelProperty property,
        float time,
        float value,
        XafInterpolation interpolation = XafInterpolation.Linear)
    {
        if (!package.Attachments.Exists(attachment => attachment.Group == group))
            return false;

        return SetChannel(
            clip,
            XafChannelTarget.Attachment,
            group,
            property,
            time,
            value,
            interpolation);
    }

    public static bool SetChannel(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property,
        float time,
        float value,
        XafInterpolation interpolation = XafInterpolation.Linear)
    {
        XafChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);

        if (channel is null)
        {
            channel = new XafChannel
            {
                Target = target,
                TargetId = targetId,
                Property = property,
            };
            clip.Channels.Add(channel);
        }

        float clamped = Math.Clamp(time, 0f, clip.Duration);
        XafKeyframe? existing = channel.Keyframes.Find(key =>
            Math.Abs(key.Time - clamped) < 0.0001f);
        if (existing is not null)
        {
            existing.Value = value;
            existing.Interpolation = interpolation;
            return true;
        }

        var key = new XafKeyframe
        {
            Time = clamped,
            Value = value,
            Interpolation = interpolation,
        };
        int index = channel.Keyframes.FindIndex(candidate =>
            candidate.Time > clamped);
        if (index < 0)
            channel.Keyframes.Add(key);
        else
            channel.Keyframes.Insert(index, key);
        return true;
    }

    public static bool RemoveKey(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property,
        float time,
        float tolerance = 0.0001f)
    {
        XafChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);
        if (channel is null)
            return false;

        int index = channel.Keyframes.FindIndex(key =>
            Math.Abs(key.Time - time) <= tolerance);
        if (index < 0)
            return false;

        channel.Keyframes.RemoveAt(index);
        if (channel.Keyframes.Count == 0)
            clip.Channels.Remove(channel);
        return true;
    }

    public static bool MoveKey(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property,
        float fromTime,
        float toTime,
        float tolerance = 0.0001f)
    {
        XafChannel? channel = clip.Channels.Find(candidate =>
            candidate.Target == target &&
            candidate.TargetId == targetId &&
            candidate.Property == property);
        if (channel is null)
            return false;

        int index = channel.Keyframes.FindIndex(key =>
            Math.Abs(key.Time - fromTime) <= tolerance);
        if (index < 0)
            return false;

        XafKeyframe key = channel.Keyframes[index];
        channel.Keyframes.RemoveAt(index);
        key.Time = Math.Clamp(toTime, 0f, clip.Duration);

        int insert = channel.Keyframes.FindIndex(candidate =>
            candidate.Time > key.Time);
        if (insert < 0)
            channel.Keyframes.Add(key);
        else
            channel.Keyframes.Insert(insert, key);
        return true;
    }

    public static XafClip? FindClip(XafPackage package, string id) =>
        package.Clips.Find(clip => clip.Id == id);

    public static void KeyAttachmentTransform(
        XafPackage package,
        XafClip clip,
        int attachmentIndex,
        float time,
        XafTransform transform)
    {
        if (attachmentIndex < 0 || attachmentIndex >= package.Attachments.Count)
            return;

        string group = package.Attachments[attachmentIndex].Group;
        SetAttachmentProperty(package, clip, group,
            XafChannelProperty.TranslationX, time, transform.Translation.X);
        SetAttachmentProperty(package, clip, group,
            XafChannelProperty.TranslationY, time, transform.Translation.Y);
        SetAttachmentProperty(package, clip, group,
            XafChannelProperty.Rotation, time, transform.Rotation);
        SetAttachmentProperty(package, clip, group,
            XafChannelProperty.ScaleX, time, transform.Scale.X);
        SetAttachmentProperty(package, clip, group,
            XafChannelProperty.ScaleY, time, transform.Scale.Y);
    }

    public static void KeyBoneTransform(
        XafPackage package,
        XafClip clip,
        int boneIndex,
        float time,
        XafTransform transform)
    {
        XafBone bone = package.Skeleton.Bones[boneIndex];
        SetBoneProperty(package, clip, bone.Id,
            XafChannelProperty.TranslationX, time, transform.Translation.X);
        SetBoneProperty(package, clip, bone.Id,
            XafChannelProperty.TranslationY, time, transform.Translation.Y);
        SetBoneProperty(package, clip, bone.Id,
            XafChannelProperty.Rotation, time, transform.Rotation);
        SetBoneProperty(package, clip, bone.Id,
            XafChannelProperty.ScaleX, time, transform.Scale.X);
        SetBoneProperty(package, clip, bone.Id,
            XafChannelProperty.ScaleY, time, transform.Scale.Y);
    }
}
