using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>Structural validation for XAF packages.</summary>
public static class XafValidation
{
    public static IReadOnlyList<string> Validate(XafPackage package)
    {
        var issues = new List<string>();
        if (package.Skeleton.Bones.Count == 0)
            issues.Add("Skeleton has no bones.");

        var boneIds = new HashSet<string>(StringComparer.Ordinal);
        int rootCount = 0;
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            XafBone bone = package.Skeleton.Bones[i];
            if (string.IsNullOrWhiteSpace(bone.Id))
                issues.Add($"Bone {i} has an empty id.");
            else if (!boneIds.Add(bone.Id))
                issues.Add($"Duplicate bone id '{bone.Id}'.");
            if (bone.Parent < -1 ||
                bone.Parent >= package.Skeleton.Bones.Count ||
                bone.Parent == i)
            {
                issues.Add($"Bone '{bone.Id}' has an invalid parent.");
            }
            if (bone.Parent == -1)
                rootCount++;
        }

        if (rootCount != 1)
            issues.Add("Skeleton must have exactly one root bone.");

        foreach (XafAttachment attachment in package.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.SvgPath))
                issues.Add("Attachment has an empty SVG path.");
            if (string.IsNullOrWhiteSpace(attachment.Group))
                issues.Add("Attachment has an empty SVG group.");
            if (!boneIds.Contains(attachment.BoneId))
                issues.Add($"Attachment references missing bone '{attachment.BoneId}'.");
        }

        var clipIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (XafClip clip in package.Clips)
        {
            if (string.IsNullOrWhiteSpace(clip.Id))
                issues.Add("Clip has an empty id.");
            else if (!clipIds.Add(clip.Id))
                issues.Add($"Duplicate clip id '{clip.Id}'.");
            if (clip.Duration < 0)
                issues.Add($"Clip '{clip.Id}' has negative duration.");
            if (clip.Loop &&
                (clip.LoopStart < 0 || clip.LoopEnd < clip.LoopStart ||
                 clip.LoopEnd > clip.Duration))
            {
                issues.Add($"Clip '{clip.Id}' has an invalid loop range.");
            }

            foreach (XafChannel channel in clip.Channels)
            {
                if (channel.Target == XafChannelTarget.Bone &&
                    !boneIds.Contains(channel.TargetId))
                {
                    issues.Add($"Clip '{clip.Id}' targets missing bone '{channel.TargetId}'.");
                }
                if (channel.Target == XafChannelTarget.Attachment &&
                    !package.Attachments.Exists(attachment =>
                        attachment.Group == channel.TargetId))
                {
                    issues.Add($"Clip '{clip.Id}' targets missing attachment '{channel.TargetId}'.");
                }

                float previous = float.NegativeInfinity;
                foreach (XafKeyframe key in channel.Keyframes)
                {
                    if (key.Time < 0 || key.Time > clip.Duration)
                        issues.Add($"Clip '{clip.Id}' has a key outside its duration.");
                    if (key.Time < previous)
                        issues.Add($"Clip '{clip.Id}' has unsorted keys.");
                    previous = key.Time;
                }
            }
        }

        return issues;
    }

    public static void ThrowIfInvalid(XafPackage package)
    {
        IReadOnlyList<string> issues = Validate(package);
        if (issues.Count > 0)
            throw new XafFormatException(string.Join(" ", issues));
    }
}
