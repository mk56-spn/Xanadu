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

        foreach (XafChain chain in package.Chains)
        {
            copy.Chains.Add(new XafChain
            {
                Id = chain.Id,
                BoneId = chain.BoneId,
                Offset = chain.Offset,
                Segments = chain.Segments,
                SegmentLength = chain.SegmentLength,
                Gravity = chain.Gravity,
                Damping = chain.Damping,
                SvgPath = chain.SvgPath,
                Group = chain.Group,
                Width = chain.Width,
                Lean = chain.Lean,
                CollideBoneId = chain.CollideBoneId,
                CollideOffset = chain.CollideOffset,
                CollideRadius = chain.CollideRadius,
            });
        }

        foreach (XafClip clip in package.Clips)
            copy.Clips.Add(Clone(clip));
        foreach (XafTransition rule in package.Transitions)
        {
            copy.Transitions.Add(new XafTransition
            {
                From = rule.From,
                To = rule.To,
                Fade = rule.Fade,
                Curve = rule.Curve,
            });
        }
        return copy;
    }

    public static bool AreEqual(XafPackage a, XafPackage b) =>
        ListEqual(a.Skeleton.Bones, b.Skeleton.Bones, BoneEqual) &&
        ListEqual(a.Attachments, b.Attachments, AttachmentEqual) &&
        ListEqual(a.Chains, b.Chains, ChainEqual) &&
        ListEqual(a.Clips, b.Clips, ClipEqual) &&
        ListEqual(a.Transitions, b.Transitions, TransitionEqual);

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

    private static bool ChainEqual(XafChain a, XafChain b) =>
        a.Id == b.Id && a.BoneId == b.BoneId && a.Offset == b.Offset &&
        a.Segments == b.Segments && a.SegmentLength == b.SegmentLength &&
        a.Gravity == b.Gravity && a.Damping == b.Damping &&
        a.SvgPath == b.SvgPath && a.Group == b.Group && a.Width == b.Width &&
        a.Lean == b.Lean && a.CollideBoneId == b.CollideBoneId &&
        a.CollideOffset == b.CollideOffset && a.CollideRadius == b.CollideRadius;

    private static bool ClipEqual(XafClip a, XafClip b) =>
        a.Id == b.Id && a.Duration == b.Duration && a.Loop == b.Loop &&
        a.LoopStart == b.LoopStart && a.LoopEnd == b.LoopEnd &&
        ListEqual(a.Channels, b.Channels, ChannelEqual);

    private static bool TransitionEqual(XafTransition a, XafTransition b) =>
        a.From == b.From && a.To == b.To && a.Fade == b.Fade && a.Curve == b.Curve;

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
