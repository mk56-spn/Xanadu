using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>Deep copy and structural comparison of packages, used for undo snapshots.</summary>
public static class XafPackageCopy
{
    public static XafPackage Clone(XafPackage package)
    {
        var copy = new XafPackage { Major = package.Major, Minor = package.Minor };
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            copy.Skeleton.Bones.Add(new XafBone
            {
                Id = bone.Id,
                Parent = bone.Parent,
                Rest = bone.Rest,
                ZOrder = bone.ZOrder,
                AllowTranslation = bone.AllowTranslation,
            });
        }

        foreach (XafAttachment attachment in package.Attachments)
        {
            copy.Attachments.Add(new XafAttachment
            {
                SvgPath = attachment.SvgPath,
                Group = attachment.Group,
                BoneId = attachment.BoneId,
                Transform = attachment.Transform,
                ZOrder = attachment.ZOrder,
            });
        }

        foreach (XafClip clip in package.Clips)
            copy.Clips.Add(Clone(clip));
        return copy;
    }

    public static bool AreEqual(XafPackage a, XafPackage b) =>
        ListEqual(a.Skeleton.Bones, b.Skeleton.Bones, BoneEqual) &&
        ListEqual(a.Attachments, b.Attachments, AttachmentEqual) &&
        ListEqual(a.Clips, b.Clips, ClipEqual);

    public static XafClip Clone(XafClip clip)
    {
        var copy = new XafClip
        {
            Id = clip.Id,
            Duration = clip.Duration,
            Loop = clip.Loop,
            LoopStart = clip.LoopStart,
            LoopEnd = clip.LoopEnd,
        };
        foreach (XafChannel channel in clip.Channels)
        {
            var channelCopy = new XafChannel
            {
                Target = channel.Target,
                TargetId = channel.TargetId,
                Property = channel.Property,
            };
            foreach (XafKeyframe key in channel.Keyframes)
            {
                channelCopy.Keyframes.Add(new XafKeyframe
                {
                    Time = key.Time,
                    Value = key.Value,
                    Interpolation = key.Interpolation,
                    TangentIn = key.TangentIn,
                    TangentOut = key.TangentOut,
                });
            }
            copy.Channels.Add(channelCopy);
        }
        return copy;
    }

    private static bool BoneEqual(XafBone a, XafBone b) =>
        a.Id == b.Id && a.Parent == b.Parent && a.Rest == b.Rest &&
        a.ZOrder == b.ZOrder && a.AllowTranslation == b.AllowTranslation;

    private static bool AttachmentEqual(XafAttachment a, XafAttachment b) =>
        a.SvgPath == b.SvgPath && a.Group == b.Group && a.BoneId == b.BoneId &&
        a.Transform == b.Transform && a.ZOrder == b.ZOrder;

    private static bool ClipEqual(XafClip a, XafClip b) =>
        a.Id == b.Id && a.Duration == b.Duration && a.Loop == b.Loop &&
        a.LoopStart == b.LoopStart && a.LoopEnd == b.LoopEnd &&
        ListEqual(a.Channels, b.Channels, ChannelEqual);

    private static bool ChannelEqual(XafChannel a, XafChannel b) =>
        a.Target == b.Target && a.TargetId == b.TargetId && a.Property == b.Property &&
        ListEqual(a.Keyframes, b.Keyframes, KeyEqual);

    private static bool KeyEqual(XafKeyframe a, XafKeyframe b) =>
        a.Time == b.Time && a.Value == b.Value && a.Interpolation == b.Interpolation &&
        a.TangentIn == b.TangentIn && a.TangentOut == b.TangentOut;

    private static bool ListEqual<T>(
        List<T> a,
        List<T> b,
        System.Func<T, T, bool> equal)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!equal(a[i], b[i]))
                return false;
        }
        return true;
    }
}
