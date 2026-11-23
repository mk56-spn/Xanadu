using System;
using Xanadu.Core.Keys;
using Xanadu.Core.Timeline;

namespace Xanadu.Model.Animation;

/// <summary>
/// The span between two keys: whether anything changes across it, and the one
/// easing it all uses, or null when nothing changes or its channels disagree.
/// </summary>
public readonly record struct RigEaseSegment(bool Moves, EasePreset? Preset);

/// <summary>
/// Applies and reads <see cref="EasePreset"/> on a pose key (every channel
/// of a target at one time). Curves are cubic Hermite segments, so a preset is
/// a mode plus the two tangents that bound the segment; bezier handles can be
/// authored later without changing the format.
/// </summary>
public static class RigEasing
{
    /// <summary>Sets the preset on every channel key of the target at the time.</summary>
    public static bool Apply(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        float time,
        EasePreset preset)
    {
        bool changed = false;
        foreach (KeyChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            int index = channel.Keys.FindIndex(key =>
                Math.Abs(key.Time - time) <= RigTracks.TimeTolerance);
            if (index < 0)
                continue;

            ApplyTo(channel, index, preset);
            changed = true;
        }

        return changed;
    }

    /// <summary>Sets the preset on one channel's key at the time.</summary>
    public static bool Apply(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float time,
        EasePreset preset)
    {
        KeyChannel? channel = FindChannel(clip, target, targetId, property);
        if (channel is null)
            return false;

        int index = channel.Keys.FindIndex(key =>
            Math.Abs(key.Time - time) <= RigTracks.TimeTolerance);
        if (index < 0)
            return false;

        ApplyTo(channel, index, preset);
        return true;
    }

    /// <summary>The preset of one channel's key at the time, or null when there is none.</summary>
    public static EasePreset? Read(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property,
        float time)
    {
        KeyChannel? channel = FindChannel(clip, target, targetId, property);
        if (channel is null)
            return null;

        int index = channel.Keys.FindIndex(key =>
            Math.Abs(key.Time - time) <= RigTracks.TimeTolerance);
        return index >= 0 ? Classify(channel, index) : null;
    }

    /// <summary>The preset of the first channel key found at the time, or null when there is none.</summary>
    public static EasePreset? Read(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        float time)
    {
        foreach (KeyChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId)
                continue;

            int index = channel.Keys.FindIndex(key =>
                Math.Abs(key.Time - time) <= RigTracks.TimeTolerance);
            if (index >= 0)
                return Classify(channel, index);
        }

        return null;
    }

    /// <summary>The family and direction of a named-curve ease, or null for step, linear and cubic.</summary>
    public static (CurveFamily Family, CurveDirection Direction)? Shape(KeyEase ease) => KeyEases.Shape(ease);

    /// <summary>The stored interpolation of a named-curve preset, or null for the ones drawn with tangents.</summary>
    public static KeyEase? NamedCurve(EasePreset preset) => KeyEasing.NamedCurve(preset);

    /// <summary>
    /// How a track, or one channel of it, gets from one key time to the next. A
    /// channel with no key at <paramref name="from"/> counts by the key it is still
    /// leaving, as that is what it is doing across the span.
    /// </summary>
    public static RigEaseSegment ReadSegment(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty? property,
        float from)
    {
        EasePreset? preset = null;
        bool mixed = false;
        foreach (KeyChannel channel in clip.Channels)
        {
            if (channel.Target != target || channel.TargetId != targetId ||
                (property is { } only && channel.Property != only))
            {
                continue;
            }

            int index = channel.Keys.FindLastIndex(key => key.Time <= from + RigTracks.TimeTolerance);
            if (index < 0 || index + 1 >= channel.Keys.Count ||
                channel.Keys[index].Value == channel.Keys[index + 1].Value)
            {
                continue;
            }

            EasePreset leaving = Classify(channel, index);
            mixed |= preset is { } seen && seen != leaving;
            preset = leaving;
        }

        return new RigEaseSegment(preset is not null, mixed ? null : preset);
    }

    private static KeyChannel? FindChannel(
        RigClip clip,
        RigChannelTarget target,
        string targetId,
        RigChannelProperty property) =>
        clip.Channels.Find(channel =>
            channel.Target == target &&
            channel.TargetId == targetId &&
            channel.Property == property);

    private static void ApplyTo(KeyChannel channel, int index, EasePreset preset) =>
        KeyEasing.Apply(channel, index, preset);

    private static EasePreset Classify(KeyChannel channel, int index) => KeyEasing.Classify(channel, index);
}
