using System;

namespace Writer.Animation;

/// <summary>Interpolation presets for the segment that leaves a key.</summary>
public enum XafEasePreset
{
    Step,
    Linear,

    /// <summary>Slow at both ends.</summary>
    Ease,

    /// <summary>Slow start, full speed at the end.</summary>
    EaseIn,

    /// <summary>Full speed at the start, slow end.</summary>
    EaseOut,
}

/// <summary>
/// Applies and reads <see cref="XafEasePreset"/> on a pose key (every channel
/// of a target at one time). Curves are cubic Hermite segments, so a preset is
/// a mode plus the two tangents that bound the segment; bezier handles can be
/// authored later without changing the format.
/// </summary>
public static class XafEasing
{
    private const float Tolerance = 0.001f;

    /// <summary>Sets the preset on every channel key of the target at the time.</summary>
    public static bool Apply(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        float time,
        XafEasePreset preset)
    {
        bool changed = false;
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            int index = channel.Keyframes.FindIndex(key =>
                Math.Abs(key.Time - time) <= XafTracks.TimeTolerance);
            if (index < 0)
                continue;

            ApplyTo(channel, index, preset);
            changed = true;
        }

        return changed;
    }

    /// <summary>Sets the preset on one channel's key at the time.</summary>
    public static bool Apply(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property,
        float time,
        XafEasePreset preset)
    {
        XafChannel? channel = FindChannel(clip, target, targetId, property);
        if (channel is null)
            return false;

        int index = channel.Keyframes.FindIndex(key =>
            Math.Abs(key.Time - time) <= XafTracks.TimeTolerance);
        if (index < 0)
            return false;

        ApplyTo(channel, index, preset);
        return true;
    }

    /// <summary>The preset of one channel's key at the time, or null when there is none.</summary>
    public static XafEasePreset? Read(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property,
        float time)
    {
        XafChannel? channel = FindChannel(clip, target, targetId, property);
        if (channel is null)
            return null;

        int index = channel.Keyframes.FindIndex(key =>
            Math.Abs(key.Time - time) <= XafTracks.TimeTolerance);
        return index >= 0 ? Classify(channel, index) : null;
    }

    /// <summary>The preset of the first channel key found at the time, or null when there is none.</summary>
    public static XafEasePreset? Read(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        float time)
    {
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            int index = channel.Keyframes.FindIndex(key =>
                Math.Abs(key.Time - time) <= XafTracks.TimeTolerance);
            if (index >= 0)
                return Classify(channel, index);
        }

        return null;
    }

    private static XafChannel? FindChannel(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty property) =>
        clip.Channels.Find(channel =>
            channel.Target == target &&
            channel.TargetId == targetId &&
            channel.Property == property);

    private static void ApplyTo(XafChannel channel, int index, XafEasePreset preset)
    {
        XafKeyframe key = channel.Keyframes[index];
        key.Interpolation = preset switch
        {
            XafEasePreset.Step => XafInterpolation.Step,
            XafEasePreset.Linear => XafInterpolation.Linear,
            _ => XafInterpolation.CubicBezier,
        };

        key.TangentOut = 0f;
        if (index + 1 >= channel.Keyframes.Count)
            return;

        XafKeyframe next = channel.Keyframes[index + 1];
        next.TangentIn = 0f;
        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        if (preset == XafEasePreset.EaseIn)
            next.TangentIn = 2f * slope;
        else if (preset == XafEasePreset.EaseOut)
            key.TangentOut = 2f * slope;
    }

    private static XafEasePreset Classify(XafChannel channel, int index)
    {
        XafKeyframe key = channel.Keyframes[index];
        if (key.Interpolation == XafInterpolation.Step)
            return XafEasePreset.Step;
        if (key.Interpolation == XafInterpolation.Linear)
            return XafEasePreset.Linear;
        if (index + 1 >= channel.Keyframes.Count)
            return XafEasePreset.Ease;

        XafKeyframe next = channel.Keyframes[index + 1];
        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        float tolerance = Tolerance * (1f + Math.Abs(slope));
        bool outFlat = Math.Abs(key.TangentOut) <= tolerance;
        bool inFlat = Math.Abs(next.TangentIn) <= tolerance;
        if (outFlat && !inFlat && Math.Abs(next.TangentIn - 2f * slope) <= tolerance)
            return XafEasePreset.EaseIn;
        if (inFlat && !outFlat && Math.Abs(key.TangentOut - 2f * slope) <= tolerance)
            return XafEasePreset.EaseOut;
        return XafEasePreset.Ease;
    }
}
