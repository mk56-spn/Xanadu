using System.Collections.Generic;
using Xanadu.Core.Keys;

namespace Xanadu.Model.Animation;

/// <summary>One animated target of a clip: a bone, an attachment or an effect.</summary>
public readonly record struct RigTrack(RigChannelTarget Target, string Id);

/// <summary>Groups a clip's channels into per-target tracks for a dope-sheet view.</summary>
public static class RigTracks
{
    /// <summary>Keys closer than this are one key set.</summary>
    public const float TimeTolerance = KeyChannel.TimeTolerance;

    /// <summary>
    /// The targets that have keys in the clip: bones in skeleton order, then
    /// attachments, then effects, in package order. <paramref name="alwaysInclude"/> adds a
    /// target with no keys yet, so a selection still has a row to key into.
    /// </summary>
    public static List<RigTrack> Of(
        RigPackage package,
        RigClip clip,
        RigTrack? alwaysInclude = null)
    {
        var animated = new HashSet<RigTrack>();
        foreach (KeyTarget target in KeyRows.Animated(clip))
        {
            animated.Add(target.Part.Length > 0
                ? new RigTrack(RigChannelTarget.Bone, target.Part)
                : new RigTrack(RigChannelTarget.Attachment, target.Node));
        }

        if (alwaysInclude is { } extra)
            animated.Add(extra);

        var tracks = new List<RigTrack>();
        foreach (RigBone bone in package.Skeleton.Bones)
        {
            var track = new RigTrack(RigChannelTarget.Bone, bone.Id);
            if (animated.Contains(track))
                tracks.Add(track);
        }

        foreach (RigAttachment attachment in package.Attachments)
        {
            var track = new RigTrack(RigChannelTarget.Attachment, attachment.Id);
            if (animated.Contains(track))
                tracks.Add(track);
        }

        return tracks;
    }

    /// <summary>The distinct times at which any channel of the track has a key, ascending.</summary>
    public static List<float> KeyTimes(RigClip clip, RigTrack track) =>
        KeyRows.KeyTimes(clip, RigChannels.TargetOf(track.Target, track.Id));
}
