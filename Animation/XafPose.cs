using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>Per-bone, per-attachment and per-effect values for one sampled pose.</summary>
public sealed class XafPose
{
    public XafTransform[] Bones;
    public XafTransform[] Attachments;

    /// <summary>One per package effect; empty for a pose built without them.</summary>
    public XafEffectState[] Effects;

    public XafPose(int boneCount, int attachmentCount, int effectCount = 0)
    {
        Bones = new XafTransform[boneCount];
        Attachments = new XafTransform[attachmentCount];
        Effects = new XafEffectState[effectCount];
    }

    public XafPose Clone()
    {
        var pose = new XafPose(Bones.Length, Attachments.Length, Effects.Length);
        Array.Copy(Bones, pose.Bones, Bones.Length);
        Array.Copy(Attachments, pose.Attachments, Attachments.Length);
        Array.Copy(Effects, pose.Effects, Effects.Length);
        return pose;
    }
}

/// <summary>An effect in one pose: where it is on its bone, how opaque, and whether it shows at all.</summary>
public struct XafEffectState
{
    public XafTransform Transform;
    public float Opacity;
    public bool Visible;

    /// <summary>How much of it is drawn: its opacity while visible, nothing otherwise.</summary>
    public readonly float Alpha => Visible ? Math.Clamp(Opacity, 0f, 1f) : 0f;

    /// <summary>At its resting place on the bone, fully opaque, and hidden until a clip shows it.</summary>
    public static XafEffectState Rest(XafEffect effect) => new()
    {
        Transform = XafTransform.From(effect.Transform),
        Opacity = 1f,
        Visible = false,
    };
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
            package.Attachments.Count,
            package.Effects.Count);

        // Effects are drawn as authored even from an additive layer: a streak
        // in an attack clip is where the clip put it, not an offset from rest.
        for (int i = 0; i < pose.Effects.Length; i++)
            pose.Effects[i] = XafEffectState.Rest(package.Effects[i]);

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

        var effectIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < package.Effects.Count; i++)
            effectIndices[package.Effects[i].Id] = i;

        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target == XafChannelTarget.Effect)
            {
                if (effectIndices.TryGetValue(channel.TargetId, out int effect))
                    ApplyEffect(ref pose.Effects[effect], channel, time);
                continue;
            }

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

    /// <summary>
    /// A held value: the latest key at or before the time, so a visibility
    /// key switches exactly where it sits rather than half way to the next.
    /// Before the first key it is <paramref name="before"/>, so an effect is
    /// hidden until the key that shows it.
    /// </summary>
    public static float SampleHeld(XafChannel channel, float time, float before = 0f)
    {
        var keys = channel.Keyframes;
        float value = before;
        foreach (XafKeyframe key in keys)
        {
            if (key.Time > time + XafTracks.TimeTolerance)
                break;
            value = key.Value;
        }
        return value;
    }

    private static void ApplyEffect(ref XafEffectState effect, XafChannel channel, float time)
    {
        switch (channel.Property)
        {
            case XafChannelProperty.Visible:
                effect.Visible = SampleHeld(channel, time) >= 0.5f;
                break;
            case XafChannelProperty.Opacity:
                effect.Opacity = Math.Clamp(SampleChannel(channel, time), 0f, 1f);
                break;
            default:
                Apply(ref effect.Transform, channel.Property, SampleChannel(channel, time));
                break;
        }
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
        for (int i = 0; i < Math.Min(result.Effects.Length, to.Effects.Length); i++)
            result.Effects[i] = Blend(from.Effects[i], to.Effects[i], weight);
        return result;
    }

    /// <summary>
    /// An effect shown by either pose fades with the crossfade: its drawn
    /// alpha moves from one pose's to the other's, and it sits where the
    /// pose that shows it puts it, so one appearing does not slide in from rest.
    /// </summary>
    public static XafEffectState Blend(XafEffectState from, XafEffectState to, float weight)
    {
        float alpha = Mathf.Lerp(from.Alpha, to.Alpha, weight);
        XafTransform transform = from.Visible == to.Visible
            ? Blend(from.Transform, to.Transform, weight)
            : to.Visible ? to.Transform : from.Transform;
        return new XafEffectState { Transform = transform, Opacity = alpha, Visible = alpha > 0f };
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

        // A layer that shows an effect draws it over the base, at the layer's weight.
        for (int i = 0; i < Math.Min(result.Effects.Length, additive.Effects.Length); i++)
        {
            XafEffectState layer = additive.Effects[i];
            float alpha = layer.Alpha * weight;
            if (alpha > result.Effects[i].Alpha)
                result.Effects[i] = layer with { Opacity = alpha, Visible = alpha > 0f };
        }
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
