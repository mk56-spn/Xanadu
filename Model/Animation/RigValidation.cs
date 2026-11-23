using System.Collections.Generic;
using Xanadu.Core.Keys;
using Xanadu.Core.Layering;

namespace Xanadu.Model.Animation;

/// <summary>Structural validation for rigs.</summary>
public static class RigValidation
{
    public static IReadOnlyList<string> Validate(RigPackage package)
    {
        var issues = new List<string>();
        if (package.Skeleton.Bones.Count == 0)
            issues.Add("Skeleton has no bones.");

        var boneIds = new HashSet<string>(StringComparer.Ordinal);
        int rootCount = 0;
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
        {
            RigBone bone = package.Skeleton.Bones[i];
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

        foreach (RigChain chain in package.Chains)
        {
            if (chain.Layer is < DrawOrder.MinLayer or > DrawOrder.MaxLayer)
                issues.Add($"Chain '{chain.Id}' has a layer out of range.");
        }

        if (rootCount != 1)
            issues.Add("Skeleton must have exactly one root bone.");

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        CheckIds(package.Root, nodeIds, issues);

        for (int i = 0; i < package.Attachments.Count; i++)
        {
            RigAttachment attachment = package.Attachments[i];
            if (attachment.ZOrder < DrawOrder.MinLayer || attachment.ZOrder > DrawOrder.MaxLayer)
                issues.Add($"Part '{attachment.Id}' has a layer out of range.");
            RigEffects.Validate(attachment, issues);
            if (attachment.Mesh is { } mesh)
            {
                if (mesh.Detail < RigMesh.MinDetail || mesh.Detail > RigMesh.MaxDetail ||
                    !(mesh.Blend >= 0f && mesh.Blend <= RigMesh.MaxBlend))
                {
                    issues.Add($"Attachment '{attachment.Id}' has an invalid mesh.");
                }
                foreach (string bone in mesh.Bones)
                {
                    if (!boneIds.Contains(bone))
                        issues.Add($"Attachment '{attachment.Id}' bends with missing bone '{bone}'.");
                }
                if (mesh.Skin is { } skin && !SkinIsWhole(skin))
                    issues.Add($"Attachment '{attachment.Id}' has a broken skin mesh.");
            }
            if (string.IsNullOrWhiteSpace(attachment.Id))
                issues.Add("Part has an empty id.");
            bool onParent = attachment.BoneId.Length == 0 && package.AttachmentParents[i] >= 0;
            if (!onParent && !boneIds.Contains(attachment.BoneId))
                issues.Add($"Part '{attachment.Id}' references missing bone '{attachment.BoneId}'.");
        }

        foreach (RigChain chain in package.Chains)
        {
            if (string.IsNullOrWhiteSpace(chain.Id))
                issues.Add("Chain has an empty id.");
            if (!boneIds.Contains(chain.BoneId))
                issues.Add($"Chain '{chain.Id}' references missing bone '{chain.BoneId}'.");
            if (chain.Segments < RigChain.MinSegments || chain.Segments > RigChain.MaxSegments)
                issues.Add($"Chain '{chain.Id}' has an invalid segment count.");
            if (!(chain.SegmentLength >= RigChain.MinSegmentLength) ||
                chain.SegmentLength > RigChain.MaxSegmentLength)
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

        RigClothValidation.Validate(package, boneIds, issues);

        var clipIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (RigClip clip in package.Clips)
        {
            RigEventAuthoring.Validate(clip, issues);
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

            foreach (KeyChannel channel in clip.Channels)
            {
                if (channel.Target == RigChannelTarget.Bone &&
                    !boneIds.Contains(channel.TargetId))
                {
                    issues.Add($"Clip '{clip.Id}' targets missing bone '{channel.TargetId}'.");
                }
                if (channel.Target != RigChannelTarget.Attachment &&
                    channel.Property is RigChannelProperty.Art or RigChannelProperty.Layer)
                {
                    issues.Add($"Clip '{clip.Id}' keys {channel.Property} on '{channel.TargetId}', which only attachments have.");
                }
                if ((channel.Target == RigChannelTarget.Bone &&
                     channel.Property is RigChannelProperty.Opacity or RigChannelProperty.Visible) ||
                    (channel.Target != RigChannelTarget.Bone && channel.Property == RigChannelProperty.Depth))
                {
                    issues.Add($"Clip '{clip.Id}' keys {channel.Property} on '{channel.TargetId}', which it does not have.");
                }
                if (channel.Target == RigChannelTarget.Attachment &&
                    !package.Attachments.Exists(attachment =>
                        attachment.Id == channel.TargetId))
                {
                    issues.Add($"Clip '{clip.Id}' targets missing attachment '{channel.TargetId}'.");
                }

                float previous = float.NegativeInfinity;
                foreach (Keyframe key in channel.Keys)
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
        foreach (RigTransition rule in package.Transitions)
        {
            string name = $"Transition '{rule.From}' to '{rule.To}'";
            if (!clipIds.Contains(rule.From) || !clipIds.Contains(rule.To))
                issues.Add($"{name} references a missing clip.");
            else if (rule.From == rule.To)
                issues.Add($"{name} goes nowhere.");
            if (!pairs.Add((rule.From, rule.To)))
                issues.Add($"Duplicate {name.ToLowerInvariant()}.");
            if (!(rule.Fade >= 0f && rule.Fade <= RigTransitions.MaxFade))
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
    private static bool SkinIsWhole(RigSkinMesh skin)
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

    public static void ThrowIfInvalid(RigPackage package)
    {
        IReadOnlyList<string> issues = Validate(package);
        if (issues.Count > 0)
            throw new RigFormatException(string.Join(" ", issues));
    }

    /// <summary>Node ids must be unique within a rig: clips key nodes by id.</summary>
    private static void CheckIds(Core.Items.Xif node, HashSet<string> seen, List<string> issues)
    {
        foreach (Core.Items.Xif child in node.Children)
        {
            if (child.Id.Length > 0 && !seen.Add(child.Id))
                issues.Add($"Duplicate node id '{child.Id}'.");
            CheckIds(child, seen, issues);
        }
    }
}
