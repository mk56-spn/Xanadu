using System;
using Xanadu.Timeline;

namespace Xanadu.Animation;

/// <summary>
/// The span between two keys: whether anything changes across it, and the one
/// easing it all uses, or null when nothing changes or its channels disagree.
/// </summary>
public readonly record struct XafEaseSegment(bool Moves, EasePreset? Preset);

/// <summary>
/// Applies and reads <see cref="EasePreset"/> on a pose key (every channel
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
        EasePreset preset)
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
        EasePreset preset)
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
    public static EasePreset? Read(
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
    public static EasePreset? Read(
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

    /// <summary>The family and direction of a named-curve interpolation, or null for step, linear and cubic.</summary>
    public static (CurveFamily Family, CurveDirection Direction)? Shape(XafInterpolation interpolation) =>
        (uint)interpolation < (uint)Shapes.Length ? Shapes[(int)interpolation] : null;

    /// <summary>Every interpolation's shape, by value, so sampling never parses a name.</summary>
    private static readonly (CurveFamily, CurveDirection)?[] Shapes = BuildShapes();

    private static (CurveFamily, CurveDirection)?[] BuildShapes()
    {
        XafInterpolation[] values = Enum.GetValues<XafInterpolation>();
        var shapes = new (CurveFamily, CurveDirection)?[values.Length];
        foreach (XafInterpolation value in values)
            shapes[(int)value] = EasePresets.ParseShape(value.ToString());
        return shapes;
    }

    /// <summary>The highest interpolation value a file may hold.</summary>
    public static int MaxInterpolation => Shapes.Length - 1;

    /// <summary>The stored interpolation of a named-curve preset, or null for the ones drawn with tangents.</summary>
    public static XafInterpolation? NamedCurve(EasePreset preset) =>
        preset > EasePreset.EaseOut && Enum.TryParse(preset.ToString(), out XafInterpolation curve)
            ? curve
            : null;

    /// <summary>
    /// How a track, or one channel of it, gets from one key time to the next. A
    /// channel with no key at <paramref name="from"/> counts by the key it is still
    /// leaving, as that is what it is doing across the span.
    /// </summary>
    public static XafEaseSegment ReadSegment(
        XafClip clip,
        XafChannelTarget target,
        string targetId,
        XafChannelProperty? property,
        float from)
    {
        EasePreset? preset = null;
        bool mixed = false;
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId ||
                (property is { } only && channel.Property != only))
            {
                continue;
            }

            int index = channel.Keyframes.FindLastIndex(key => key.Time <= from + XafTracks.TimeTolerance);
            if (index < 0 || index + 1 >= channel.Keyframes.Count ||
                channel.Keyframes[index].Value == channel.Keyframes[index + 1].Value)
            {
                continue;
            }

            EasePreset leaving = Classify(channel, index);
            mixed |= preset is { } seen && seen != leaving;
            preset = leaving;
        }

        return new XafEaseSegment(preset is not null, mixed ? null : preset);
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

    private static void ApplyTo(XafChannel channel, int index, EasePreset preset)
    {
        XafKeyframe key = channel.Keyframes[index];
        key.Interpolation = preset switch
        {
            EasePreset.Step => XafInterpolation.Step,
            EasePreset.Linear => XafInterpolation.Linear,
            _ => NamedCurve(preset) ?? XafInterpolation.CubicBezier,
        };

        key.TangentOut = 0f;
        if (index + 1 >= channel.Keyframes.Count)
            return;

        XafKeyframe next = channel.Keyframes[index + 1];
        next.TangentIn = 0f;
        if (key.Interpolation != XafInterpolation.CubicBezier)
            return;

        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        if (preset == EasePreset.EaseIn)
            next.TangentIn = 2f * slope;
        else if (preset == EasePreset.EaseOut)
            key.TangentOut = 2f * slope;
    }

    private static EasePreset Classify(XafChannel channel, int index)
    {
        XafKeyframe key = channel.Keyframes[index];
        if (key.Interpolation == XafInterpolation.Step)
            return EasePreset.Step;
        if (key.Interpolation == XafInterpolation.Linear)
            return EasePreset.Linear;
        if (key.Interpolation != XafInterpolation.CubicBezier &&
            Enum.TryParse(key.Interpolation.ToString(), out EasePreset named))
        {
            return named;
        }
        if (index + 1 >= channel.Keyframes.Count)
            return EasePreset.Ease;

        XafKeyframe next = channel.Keyframes[index + 1];
        float span = next.Time - key.Time;
        float slope = span > 0f ? (next.Value - key.Value) / span : 0f;
        float tolerance = Tolerance * (1f + Math.Abs(slope));
        bool outFlat = Math.Abs(key.TangentOut) <= tolerance;
        bool inFlat = Math.Abs(next.TangentIn) <= tolerance;
        if (outFlat && !inFlat && Math.Abs(next.TangentIn - 2f * slope) <= tolerance)
            return EasePreset.EaseIn;
        if (inFlat && !outFlat && Math.Abs(key.TangentOut - 2f * slope) <= tolerance)
            return EasePreset.EaseOut;
        return EasePreset.Ease;
    }
}
