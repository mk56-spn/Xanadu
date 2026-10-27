using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>Per-bone and per-attachment transform values for one sampled pose.</summary>
public sealed class XafPose
{
    public XafTransform[] Bones;
    public XafTransform[] Attachments;

    public XafPose(int boneCount, int attachmentCount)
    {
        Bones = new XafTransform[boneCount];
        Attachments = new XafTransform[attachmentCount];
    }

    public XafPose Clone()
    {
        var pose = new XafPose(Bones.Length, Attachments.Length);
        Array.Copy(Bones, pose.Bones, Bones.Length);
        Array.Copy(Attachments, pose.Attachments, Attachments.Length);
        return pose;
    }
}

/// <summary>Decomposed 2D transform used for interpolation and additive blending.</summary>
public struct XafTransform
{
    public Vector2 Translation;
    public float Rotation;
    public Vector2 Scale;

    public static XafTransform Identity => new()
    {
        Translation = Vector2.Zero,
        Rotation = 0f,
        Scale = Vector2.One,
    };

    public static XafTransform From(Transform2D transform) => new()
    {
        Translation = transform.Origin,
        Rotation = transform.Rotation,
        Scale = transform.Scale,
    };

    public readonly Transform2D ToTransform() =>
        new(Rotation, Scale, 0f, Translation);
}

/// <summary>Samples and blends XAF pose channels.</summary>
public static class XafSampler
{
    public static XafPose Sample(
        XafPackage package,
        XafClip clip,
        float time,
        bool additive = false)
    {
        var pose = new XafPose(
            package.Skeleton.Bones.Count,
            package.Attachments.Count);

        for (int i = 0; i < pose.Bones.Length; i++)
        {
            pose.Bones[i] = additive
                ? XafTransform.Identity
                : XafTransform.From(package.Skeleton.Bones[i].Rest);
        }

        for (int i = 0; i < pose.Attachments.Length; i++)
        {
            pose.Attachments[i] = additive
                ? XafTransform.Identity
                : XafTransform.From(package.Attachments[i].Transform);
        }

        if (clip.Loop)
        {
            float length = Math.Max(0.0001f, clip.LoopEnd - clip.LoopStart);
            time = clip.LoopStart + Mathf.PosMod(time - clip.LoopStart, length);
        }

        var boneIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < package.Skeleton.Bones.Count; i++)
            boneIndices[package.Skeleton.Bones[i].Id] = i;

        var attachmentIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < package.Attachments.Count; i++)
            attachmentIndices[package.Attachments[i].Group] = i;

        foreach (XafChannel channel in clip.Channels)
        {
            float value = SampleChannel(channel, time);
            if (channel.Target == XafChannelTarget.Bone &&
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
            else if (channel.Target == XafChannelTarget.Attachment &&
                     attachmentIndices.TryGetValue(channel.TargetId, out int attachment))
            {
                Apply(ref pose.Attachments[attachment], channel.Property, value);
            }
        }

        return pose;
    }

    public static float SampleChannel(XafChannel channel, float time)
    {
        var keys = channel.Keyframes;
        if (keys.Count == 0)
            return 0f;
        if (keys.Count == 1 || time <= keys[0].Time)
            return keys[0].Value;

        for (int i = 1; i < keys.Count; i++)
        {
            XafKeyframe next = keys[i];
            if (time > next.Time)
                continue;

            XafKeyframe previous = keys[i - 1];
            float span = next.Time - previous.Time;
            if (span <= 0f)
                return next.Value;

            float t = (time - previous.Time) / span;
            return previous.Interpolation switch
            {
                // At the next key's own time the value is that key's.
                XafInterpolation.Step => t >= 1f ? next.Value : previous.Value,
                XafInterpolation.CubicBezier => Hermite(
                    previous.Value,
                    next.Value,
                    previous.TangentOut,
                    next.TangentIn,
                    t,
                    span),
                _ => Mathf.Lerp(previous.Value, next.Value, t),
            };
        }

        return keys[^1].Value;
    }

    private static float Hermite(
        float previous,
        float next,
        float tangentOut,
        float tangentIn,
        float t,
        float span)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;
        return h00 * previous +
               h10 * tangentOut * span +
               h01 * next +
               h11 * tangentIn * span;
    }

    public static bool IsTranslation(XafChannelProperty property) =>
        property is XafChannelProperty.TranslationX or XafChannelProperty.TranslationY;

    private static void Apply(
        ref XafTransform transform,
        XafChannelProperty property,
        float value)
    {
        switch (property)
        {
            case XafChannelProperty.TranslationX:
                transform.Translation.X = value;
                break;
            case XafChannelProperty.TranslationY:
                transform.Translation.Y = value;
                break;
            case XafChannelProperty.Rotation:
                transform.Rotation = value;
                break;
            case XafChannelProperty.ScaleX:
                transform.Scale.X = value;
                break;
            case XafChannelProperty.ScaleY:
                transform.Scale.Y = value;
                break;
        }
    }
}

/// <summary>Crossfade and additive blending for sampled poses.</summary>
public static class XafBlend
{
    public static XafPose Crossfade(XafPose from, XafPose to, float weight)
    {
        weight = Mathf.Clamp(weight, 0f, 1f);
        XafPose result = from.Clone();
        for (int i = 0; i < result.Bones.Length; i++)
            result.Bones[i] = Blend(from.Bones[i], to.Bones[i], weight);
        for (int i = 0; i < result.Attachments.Length; i++)
            result.Attachments[i] = Blend(
                from.Attachments[i],
                to.Attachments[i],
                weight);
        return result;
    }

    public static XafPose Add(XafPose basePose, XafPose additive, float weight)
    {
        weight = Mathf.Clamp(weight, 0f, 1f);
        XafPose result = basePose.Clone();
        for (int i = 0; i < result.Bones.Length; i++)
            result.Bones[i] = Add(result.Bones[i], additive.Bones[i], weight);
        for (int i = 0; i < result.Attachments.Length; i++)
            result.Attachments[i] = Add(
                result.Attachments[i],
                additive.Attachments[i],
                weight);
        return result;
    }

    public static XafTransform Blend(
        XafTransform from,
        XafTransform to,
        float weight) => new()
    {
        Translation = from.Translation.Lerp(to.Translation, weight),
        Rotation = Mathf.LerpAngle(from.Rotation, to.Rotation, weight),
        Scale = from.Scale.Lerp(to.Scale, weight),
    };

    public static XafTransform Add(
        XafTransform baseTransform,
        XafTransform additive,
        float weight) => new()
    {
        Translation = baseTransform.Translation +
                      additive.Translation * weight,
        Rotation = baseTransform.Rotation + additive.Rotation * weight,
        Scale = baseTransform.Scale *
                Vector2.One.Lerp(additive.Scale, weight),
    };
}
