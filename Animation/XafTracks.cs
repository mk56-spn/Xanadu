using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>One animated target of a clip: a bone or an attachment.</summary>
public readonly record struct XafTrack(XafChannelTarget Target, string Id);

/// <summary>Groups a clip's channels into per-target tracks for a dope-sheet view.</summary>
public static class XafTracks
{
    /// <summary>Keys closer than this are one key set.</summary>
    public const float TimeTolerance = 0.0001f;

    /// <summary>
    /// The targets that have keys in the clip: bones in skeleton order, then
    /// attachments in package order. <paramref name="alwaysInclude"/> adds a
    /// target with no keys yet, so a selection still has a row to key into.
    /// </summary>
    public static List<XafTrack> Of(
        XafPackage package,
        XafClip clip,
        XafTrack? alwaysInclude = null)
    {
        var animated = new HashSet<XafTrack>();
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Keyframes.Count > 0)
                animated.Add(new XafTrack(channel.Target, channel.TargetId));
        }

        if (alwaysInclude is { } extra)
            animated.Add(extra);

        var tracks = new List<XafTrack>();
        foreach (XafBone bone in package.Skeleton.Bones)
        {
            var track = new XafTrack(XafChannelTarget.Bone, bone.Id);
            if (animated.Contains(track))
                tracks.Add(track);
        }

        foreach (XafAttachment attachment in package.Attachments)
        {
            var track = new XafTrack(XafChannelTarget.Attachment, attachment.Group);
            if (animated.Contains(track))
                tracks.Add(track);
        }

        return tracks;
    }

    /// <summary>The distinct times at which any channel of the track has a key, ascending.</summary>
    public static List<float> KeyTimes(XafClip clip, XafTrack track)
    {
        var times = new List<float>();
        foreach (XafChannel channel in clip.Channels)
        {
            if (channel.Target != track.Target || channel.TargetId != track.Id)
                continue;

            foreach (XafKeyframe key in channel.Keyframes)
                times.Add(key.Time);
        }

        times.Sort();
        var distinct = new List<float>();
        foreach (float time in times)
        {
            if (distinct.Count == 0 || time - distinct[^1] > TimeTolerance)
                distinct.Add(time);
        }
        return distinct;
    }
}
