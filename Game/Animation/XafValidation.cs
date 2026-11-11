using System;
using System.Collections.Generic;
using Xanadu.Layering;

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
            if (bone.Bend is < -1 or > 1)
                issues.Add($"Bone '{bone.Id}' has an invalid bend lock.");
            if (!float.IsFinite(bone.LimitMin) || !float.IsFinite(bone.LimitMax) ||
                bone.LimitMin > bone.LimitMax ||
                bone.LimitMin < -MathF.PI || bone.LimitMax > MathF.PI)
            {
                issues.Add($"Bone '{bone.Id}' has an invalid angle limit.");
            }
            if (!float.IsFinite(bone.Tip.X) || !float.IsFinite(bone.Tip.Y))
                issues.Add($"Bone '{bone.Id}' has an invalid tip.");
        }

        foreach (XafChain chain in package.Chains)
        {
            if (chain.Layer is < DrawOrder.MinLayer or > DrawOrder.MaxLayer)
                issues.Add($"Chain '{chain.Id}' has a layer out of range.");
        }

        if (rootCount != 1)
            issues.Add("Skeleton must have exactly one root bone.");

        foreach (XafAttachment attachment in package.Attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.SvgPath))
                issues.Add("Attachment has an empty SVG path.");
            foreach (XafArt art in attachment.Alternates)
            {
                if (string.IsNullOrWhiteSpace(art.SvgPath) || string.IsNullOrWhiteSpace(art.Group))
                    issues.Add($"Attachment '{attachment.Group}' has alternate art with no file or group.");
            }
            if (attachment.Mesh is { } mesh)
            {
                if (mesh.Detail < XafMesh.MinDetail || mesh.Detail > XafMesh.MaxDetail ||
                    !(mesh.Blend >= 0f && mesh.Blend <= XafMesh.MaxBlend))
                {
                    issues.Add($"Attachment '{attachment.Group}' has an invalid mesh.");
                }
                foreach (string bone in mesh.Bones)
                {
                    if (!boneIds.Contains(bone))
                        issues.Add($"Attachment '{attachment.Group}' bends with missing bone '{bone}'.");
                }
                if (mesh.Skin is { } skin && !SkinIsWhole(skin))
                    issues.Add($"Attachment '{attachment.Group}' has a broken skin mesh.");
            }
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

        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (XafEffect effect in package.Effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Id))
                issues.Add("Effect has an empty id.");
            else if (!effectIds.Add(effect.Id))
                issues.Add($"Duplicate effect id '{effect.Id}'.");
            if (!boneIds.Contains(effect.BoneId))
                issues.Add($"Effect '{effect.Id}' references missing bone '{effect.BoneId}'.");
            if (effect.ZOrder < DrawOrder.MinLayer || effect.ZOrder > DrawOrder.MaxLayer)
                issues.Add($"Effect '{effect.Id}' has a layer out of range.");
            XafEffects.Validate(effect, issues);
        }

        XafClothValidation.Validate(package, boneIds, issues);

        var clipIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (XafClip clip in package.Clips)
        {
            XafEventAuthoring.Validate(clip, issues);
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
                if (channel.Target == XafChannelTarget.Effect && !effectIds.Contains(channel.TargetId))
                    issues.Add($"Clip '{clip.Id}' targets missing effect '{channel.TargetId}'.");
                if (channel.Target != XafChannelTarget.Attachment &&
                    channel.Property is XafChannelProperty.Art or XafChannelProperty.Layer)
                {
                    issues.Add($"Clip '{clip.Id}' keys {channel.Property} on '{channel.TargetId}', which only attachments have.");
                }
                if ((channel.Target == XafChannelTarget.Bone &&
                     channel.Property is XafChannelProperty.Opacity or XafChannelProperty.Visible) ||
                    (channel.Target != XafChannelTarget.Bone && channel.Property == XafChannelProperty.Depth) ||
                    (channel.Target == XafChannelTarget.Attachment && channel.Property == XafChannelProperty.Opacity))
                {
                    issues.Add($"Clip '{clip.Id}' keys {channel.Property} on '{channel.TargetId}', which it does not have.");
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

    /// <summary>
    /// Whether a stored skin hangs together: whole triangles over its own
    /// points, and a finite weight row per point over its bones. A bone it
    /// names that has since gone only loses its weight when drawn.
    /// </summary>
    private static bool SkinIsWhole(XafSkinMesh skin)
    {
        if (skin.Triangles.Length % 3 != 0 || skin.Weights.Length != skin.Points.Length)
            return false;
        foreach (int index in skin.Triangles)
        {
            if (index < 0 || index >= skin.Points.Length)
                return false;
        }
        foreach (Vector2 point in skin.Points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                return false;
        }
        foreach (float[] row in skin.Weights)
        {
            if (row.Length != skin.Bones.Count)
                return false;
            foreach (float weight in row)
            {
                if (!float.IsFinite(weight) || weight < 0f)
                    return false;
            }
        }
        return true;
    }

    public static void ThrowIfInvalid(XafPackage package)
    {
        IReadOnlyList<string> issues = Validate(package);
        if (issues.Count > 0)
            throw new XafFormatException(string.Join(" ", issues));
    }
}
