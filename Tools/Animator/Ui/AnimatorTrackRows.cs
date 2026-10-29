using System.Collections.Generic;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// One row of the keyframe dope sheet: a target's merged key set, or, when the
/// target is expanded, one of its channels.
/// </summary>
public readonly record struct AnimatorTrackRow(XafTrack Track, XafChannelProperty? Property)
{
    public AnimatorTrackRow(XafTrack track)
        : this(track, null)
    {
    }

    public XafChannelTarget Target => Track.Target;
    public string Id => Track.Id;
    public bool IsChannel => Property.HasValue;

    /// <summary>Compact channel name shown on a child row.</summary>
    public static string PropertyLabel(XafChannelProperty property) => property switch
    {
        XafChannelProperty.TranslationX => "pos x",
        XafChannelProperty.TranslationY => "pos y",
        XafChannelProperty.Rotation => "rot",
        XafChannelProperty.ScaleX => "scl x",
        XafChannelProperty.ScaleY => "scl y",
        XafChannelProperty.Opacity => "alpha",
        _ => "show",
    };

    public static implicit operator AnimatorTrackRow(XafTrack track) => new(track);
}

/// <summary>Builds and reads the dope sheet rows, including expanded channel rows.</summary>
public static class AnimatorTrackRows
{
    private static readonly XafChannelProperty[] Order =
    {
        XafChannelProperty.TranslationX,
        XafChannelProperty.TranslationY,
        XafChannelProperty.Rotation,
        XafChannelProperty.ScaleX,
        XafChannelProperty.ScaleY,
        XafChannelProperty.Visible,
        XafChannelProperty.Opacity,
    };

    /// <summary>
    /// The targets that have keys plus the selected one. An expanded target is
    /// followed by a row per channel it actually animates.
    /// </summary>
    public static List<AnimatorTrackRow> Build(
        XafPackage package,
        XafClip clip,
        XafTrack? selected,
        IReadOnlySet<XafTrack>? expanded)
    {
        var rows = new List<AnimatorTrackRow>();
        foreach (XafTrack track in XafTracks.Of(package, clip, selected))
        {
            rows.Add(new AnimatorTrackRow(track));
            if (expanded is null || !expanded.Contains(track))
                continue;

            foreach (XafChannelProperty property in Channels(clip, track))
                rows.Add(new AnimatorTrackRow(track, property));
        }
        return rows;
    }

    /// <summary>The channels that have any key for a target, in a stable order.</summary>
    public static List<XafChannelProperty> Channels(XafClip clip, XafTrack track)
    {
        var channels = new List<XafChannelProperty>();
        foreach (XafChannelProperty property in Order)
        {
            foreach (XafChannel channel in clip.Channels)
            {
                if (channel.Target == track.Target &&
                    channel.TargetId == track.Id &&
                    channel.Property == property &&
                    channel.Keyframes.Count > 0)
                {
                    channels.Add(property);
                    break;
                }
            }
        }
        return channels;
    }

    public static bool HasChannels(XafClip clip, XafTrack track) =>
        Channels(clip, track).Count > 0;

    /// <summary>Key times of one row: a channel's own keys, or every channel's for a set row.</summary>
    public static List<float> KeyTimes(XafClip clip, AnimatorTrackRow row)
    {
        if (row.Property is not { } property)
            return XafTracks.KeyTimes(clip, row.Track);

        var times = new List<float>();
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != row.Target ||
                channel.TargetId != row.Id ||
                channel.Property != property)
            {
                continue;
            }

            foreach (XafKeyframe key in channel.Keyframes)
                times.Add(key.Time);
        }

        times.Sort();
        var distinct = new List<float>();
        foreach (float time in times)
        {
            if (distinct.Count == 0 || time - distinct[^1] > XafTracks.TimeTolerance)
                distinct.Add(time);
        }
        return distinct;
    }

    /// <summary>The row a hit test selects: the target row or its channel child.</summary>
    public static AnimatorKeyRef Ref(AnimatorTrackRow row, float time) =>
        new(row.Target, row.Id, time, row.Property);
}
