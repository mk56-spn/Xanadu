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

        var chainIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (XafChain chain in package.Chains)
        {
            if (string.IsNullOrWhiteSpace(chain.Id))
                issues.Add("Chain has an empty id.");
            else if (!chainIds.Add(chain.Id))
                issues.Add($"Duplicate chain id '{chain.Id}'.");
            if (!boneIds.Contains(chain.BoneId))
                issues.Add($"Chain '{chain.Id}' references missing bone '{chain.BoneId}'.");
            if (chain.Segments < XafChain.MinSegments || chain.Segments > XafChain.MaxSegments)
                issues.Add($"Chain '{chain.Id}' has an invalid segment count.");
            if (!(chain.SegmentLength >= XafChain.MinSegmentLength) ||
                chain.SegmentLength > XafChain.MaxSegmentLength)
            {
                issues.Add($"Chain '{chain.Id}' has an invalid segment length.");
            }
            if (!float.IsFinite(chain.Gravity) || !float.IsFinite(chain.Offset.X) ||
                !float.IsFinite(chain.Offset.Y))
            {
                issues.Add($"Chain '{chain.Id}' has a non-finite value.");
            }
            if (!(chain.Damping >= 0f && chain.Damping <= 1f))
                issues.Add($"Chain '{chain.Id}' has an invalid damping.");
            if (chain.SvgPath.Length > 0 != chain.Group.Length > 0)
                issues.Add($"Chain '{chain.Id}' needs both an SVG path and a group, or neither.");
            if (!(chain.Width > 0f) || !float.IsFinite(chain.Width) || !float.IsFinite(chain.Lean))
                issues.Add($"Chain '{chain.Id}' has an invalid width or lean.");
            if (!(chain.CollideRadius >= 0f) || !float.IsFinite(chain.CollideRadius) ||
                !float.IsFinite(chain.CollideOffset.X) || !float.IsFinite(chain.CollideOffset.Y))
            {
                issues.Add($"Chain '{chain.Id}' has an invalid collider.");
            }
            else if (chain.CollideRadius > 0f && !boneIds.Contains(chain.CollideBoneId))
            {
                issues.Add($"Chain '{chain.Id}' collides with missing bone '{chain.CollideBoneId}'.");
            }
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

        var pairs = new HashSet<(string, string)>();
        foreach (XafTransition rule in package.Transitions)
        {
            string name = $"Transition '{rule.From}' to '{rule.To}'";
            if (!clipIds.Contains(rule.From) || !clipIds.Contains(rule.To))
                issues.Add($"{name} references a missing clip.");
            else if (rule.From == rule.To)
                issues.Add($"{name} goes nowhere.");
            if (!pairs.Add((rule.From, rule.To)))
                issues.Add($"Duplicate {name.ToLowerInvariant()}.");
            if (!(rule.Fade >= 0f && rule.Fade <= XafTransitions.MaxFade))
                issues.Add($"{name} has an invalid fade.");
            if (!Enum.IsDefined(rule.Curve))
                issues.Add($"{name} has an unknown curve.");
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
