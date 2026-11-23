using Xanadu.Core.Keys;
using System;
using System.Collections.Generic;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;

namespace Xanadu.Model.Animation;

/// <summary>Per-bone and per-part values for one sampled pose.</summary>
public sealed class RigPose
{
    public RigTransform[] Bones;
    public RigTransform[] Attachments;

    /// <summary>One per part: the art, layer, opacity and visibility clips key.</summary>
    public RigAttachmentState[] AttachmentStates;

    public RigPose(int boneCount, int attachmentCount)
    {
        Bones = new RigTransform[boneCount];
        Attachments = new RigTransform[attachmentCount];
        AttachmentStates = new RigAttachmentState[attachmentCount];
    }

    /// <summary>A pose with every part's state as authored: hidden parts start hidden.</summary>
    public static RigPose For(RigPackage package)
    {
        var pose = new RigPose(package.Skeleton.Bones.Count, package.Attachments.Count);
        for (int i = 0; i < pose.AttachmentStates.Length; i++)
            pose.AttachmentStates[i].Hidden = package.Attachments[i].Hidden;
        return pose;
    }

    public RigPose Clone()
    {
        var pose = new RigPose(Bones.Length, Attachments.Length);
        Array.Copy(Bones, pose.Bones, Bones.Length);
        Array.Copy(Attachments, pose.Attachments, Attachments.Length);
        Array.Copy(AttachmentStates, pose.AttachmentStates, AttachmentStates.Length);
        return pose;
    }
}

/// <summary>
/// What a clip keys on a part besides its transform. The default value is
/// the part as authored: its own art, its own layer, opaque, and shown unless
/// the part is one that starts hidden.
/// </summary>
public struct RigAttachmentState
{
    /// <summary>Which art shows; 0 is the part's own (see <see cref="RigAttachment.Art"/>).</summary>
    public int Art;

    /// <summary>Whether a clip has keyed the layer; when not, the part's own is used.</summary>
    public bool HasLayer;
    public int Layer;

    public bool Hidden;

    /// <summary>Whether a clip has keyed the opacity; when not, the part is opaque.</summary>
    public bool HasOpacity;
    public float Opacity;

    public readonly int LayerOf(RigAttachment attachment) => HasLayer ? Layer : attachment.ZOrder;

    /// <summary>How much of it is drawn: its opacity while shown, nothing while hidden.</summary>
    public readonly float Alpha => Hidden ? 0f : HasOpacity ? Math.Clamp(Opacity, 0f, 1f) : 1f;
}

/// <summary>Decomposed 2D transform used for interpolation and additive blending.</summary>
public struct RigTransform
{
    public Vector2 Translation;
    public float Rotation;
    public Vector2 Scale;

    /// <summary>
    /// How far this bone's joint (its origin) sits out of the screen's plane,
    /// in rig units, toward the viewer or away: 0, the default, is in the
    /// plane. It belongs to the joint, not the bone, and is not inherited: a
    /// knee brought toward the viewer leaves the hip and the ankle in the
    /// plane, so the thigh and the shin either side of it both foreshorten
    /// (see <see cref="RigRig.Projection"/>).
    /// </summary>
    public float Depth;

    public static RigTransform Identity => new()
    {
        Translation = Vector2.Zero,
        Rotation = 0f,
        Scale = Vector2.One,
    };

    public static RigTransform From(Transform2D transform) => new()
    {
        Translation = transform.Origin,
        Rotation = transform.Rotation,
        Scale = transform.Scale,
    };

    public readonly Transform2D ToTransform() =>
        new(Rotation, Scale, 0f, Translation);
}

/// <summary>Samples and blends rig pose channels.</summary>
public static class RigSampler
{
    public static RigPose Sample(
        RigPackage package,
        RigClip clip,
        float time,
        bool additive = false) =>
        Sample(package, clip, time, additive, null);

    /// <summary>
    /// Samples a clip over another pose: what the clip keys replaces it, and
    /// everything the clip leaves alone keeps the pose's value. An attack
    /// layered this way moves only the bones it was posed with.
    /// </summary>
    public static RigPose SampleOver(RigPackage package, RigClip clip, float time, RigPose under) =>
        Sample(package, clip, time, false, under);

    private static RigPose Sample(
        RigPackage package,
        RigClip clip,
        float time,
        bool additive,
        RigPose? under)
    {
        if (under is not null)
        {
            RigPose over = under.Clone();
            Apply(package, clip, time, over);
            return over;
        }

        RigPose pose = RigPose.For(package);

        for (int i = 0; i < pose.Bones.Length; i++)
        {
            pose.Bones[i] = additive
                ? RigTransform.Identity
                : RigTransform.From(package.Skeleton.Bones[i].Rest);
        }

        // A part that starts hidden, such as a streak, is drawn as authored even
        // from an additive layer: it is where the clip put it, not an offset from rest.
        for (int i = 0; i < pose.Attachments.Length; i++)
        {
            pose.Attachments[i] = additive && !package.Attachments[i].Hidden
                ? RigTransform.Identity
                : RigTransform.From(package.Attachments[i].Transform);
        }

        Apply(package, clip, time, pose);
        return pose;
    }

    /// <summary>Writes every channel of the clip at the time into the pose.</summary>
    private static void Apply(RigPackage package, RigClip clip, float time, RigPose pose)
    {
        time = clip.Wrap(time);

        var boneIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
            boneIndices[package.Skeleton.Bones[i].Id] = i;

        var attachmentIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < package.Attachments.Count; i++)
            attachmentIndices[package.Attachments[i].Id] = i;

        foreach (KeyChannel channel in clip.Channels)
        {
            float value = channel.Sample(time);
            if (channel.Target == RigChannelTarget.Bone &&
                boneIndices.TryGetValue(channel.TargetId, out int bone))
            {
                // Bones keep their rest length unless explicitly movable.
                if (IsTranslation(channel.Property) &&
                    !package.Skeleton.Bones[bone].CanTranslate)
                {
                    continue;
                }

                Apply(ref pose.Bones[bone], channel.Property, value);
            }
            else if (channel.Target == RigChannelTarget.Attachment &&
                     attachmentIndices.TryGetValue(channel.TargetId, out int attachment))
            {
                if (IsHeld(channel.Property) || channel.Property == RigChannelProperty.Opacity)
                    ApplyState(ref pose.AttachmentStates[attachment], channel, time, package.Attachments[attachment].Hidden);
                else
                    Apply(ref pose.Attachments[attachment], channel.Property, value);
            }
        }
    }


    /// <summary>Properties that switch at their keys rather than blending between them.</summary>
    public static bool IsHeld(RigChannelProperty property) =>
        property is RigChannelProperty.Visible or RigChannelProperty.Art or RigChannelProperty.Layer;

    private static void ApplyState(ref RigAttachmentState state, KeyChannel channel, float time, bool startsHidden)
    {
        switch (channel.Property)
        {
            case RigChannelProperty.Visible:
                // Before the first key a part is as authored: shown, or hidden for one that starts so.
                state.Hidden = channel.SampleHeld(time, before: startsHidden ? 0f : 1f) < 0.5f;
                break;
            case RigChannelProperty.Opacity:
                state.HasOpacity = true;
                state.Opacity = Math.Clamp(channel.Sample(time), 0f, 1f);
                break;
            case RigChannelProperty.Art:
                state.Art = Math.Max(0, (int)MathF.Round(channel.SampleHeld(time)));
                break;
            case RigChannelProperty.Layer:
                // Before the first key the attachment keeps its own layer.
                float layer = channel.SampleHeld(time, before: float.NaN);
                state.HasLayer = !float.IsNaN(layer);
                state.Layer = state.HasLayer ? (int)MathF.Round(layer) : 0;
                break;
        }
    }

    public static bool IsTranslation(RigChannelProperty property) =>
        property is RigChannelProperty.TranslationX or RigChannelProperty.TranslationY;

    private static void Apply(
        ref RigTransform transform,
        RigChannelProperty property,
        float value)
    {
        switch (property)
        {
            case RigChannelProperty.TranslationX:
                transform.Translation.X = value;
                break;
            case RigChannelProperty.TranslationY:
                transform.Translation.Y = value;
                break;
            case RigChannelProperty.Rotation:
                transform.Rotation = value;
                break;
            case RigChannelProperty.ScaleX:
                transform.Scale.X = value;
                break;
            case RigChannelProperty.ScaleY:
                transform.Scale.Y = value;
                break;
            case RigChannelProperty.Depth:
                transform.Depth = value;
                break;
        }
    }
}

/// <summary>Crossfade and additive blending for sampled poses.</summary>
public static class RigBlend
{
    public static RigPose Crossfade(RigPose from, RigPose to, float weight)
    {
        weight = Math.Clamp(weight, 0f, 1f);
        RigPose result = from.Clone();
        for (int i = 0; i < result.Bones.Length; i++)
            result.Bones[i] = Blend(from.Bones[i], to.Bones[i], weight);
        int parts = Math.Min(result.Attachments.Length, to.Attachments.Length);
        for (int i = 0; i < parts; i++)
        {
            RigAttachmentState a = from.AttachmentStates[i];
            RigAttachmentState b = to.AttachmentStates[i];

            // A part shown by only one pose sits where that pose puts it, so one
            // appearing does not slide in from rest.
            bool shownA = a.Alpha > 0f, shownB = b.Alpha > 0f;
            result.Attachments[i] = shownA == shownB
                ? Blend(from.Attachments[i], to.Attachments[i], weight)
                : shownB ? to.Attachments[i] : from.Attachments[i];

            // Art and layer cannot blend; they switch half way through the fade.
            // What is drawn fades from one pose's alpha to the other's.
            RigAttachmentState state = weight >= 0.5f ? b : a;
            float alpha = float.Lerp(a.Alpha, b.Alpha, weight);
            if (alpha != state.Alpha)
            {
                state.HasOpacity = true;
                state.Opacity = alpha;
                state.Hidden = alpha <= 0f;
            }
            result.AttachmentStates[i] = state;
        }
        return result;
    }

    public static RigPose Add(RigPose basePose, RigPose additive, float weight)
    {
        weight = Math.Clamp(weight, 0f, 1f);
        RigPose result = basePose.Clone();
        for (int i = 0; i < result.Bones.Length; i++)
            result.Bones[i] = Add(result.Bones[i], additive.Bones[i], weight);
        int parts = Math.Min(result.Attachments.Length, additive.Attachments.Length);
        for (int i = 0; i < parts; i++)
        {
            // A layer that shows a part the base hides, such as a streak, draws it
            // over the base where the layer puts it, at the layer's weight.
            RigAttachmentState layer = additive.AttachmentStates[i];
            float alpha = layer.Alpha * weight;
            if (alpha > result.AttachmentStates[i].Alpha && result.AttachmentStates[i].Alpha <= 0f)
            {
                result.Attachments[i] = additive.Attachments[i];
                result.AttachmentStates[i] = layer with { HasOpacity = true, Opacity = alpha, Hidden = false };
                continue;
            }
            result.Attachments[i] = Add(result.Attachments[i], additive.Attachments[i], weight);
        }
        return result;
    }

    public static RigTransform Blend(
        RigTransform from,
        RigTransform to,
        float weight) => new()
    {
        Translation = from.Translation.Lerp(to.Translation, weight),
        Rotation = Mathf.LerpAngle(from.Rotation, to.Rotation, weight),
        Scale = from.Scale.Lerp(to.Scale, weight),
        Depth = float.Lerp(from.Depth, to.Depth, weight),
    };

    public static RigTransform Add(
        RigTransform baseTransform,
        RigTransform additive,
        float weight) => new()
    {
        Translation = baseTransform.Translation +
                      additive.Translation * weight,
        Rotation = baseTransform.Rotation + additive.Rotation * weight,
        Scale = baseTransform.Scale *
                Vector2.One.Lerp(additive.Scale, weight),
        Depth = baseTransform.Depth + additive.Depth * weight,
    };
}
