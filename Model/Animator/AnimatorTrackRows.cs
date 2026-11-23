using System.Collections.Generic;
using Xanadu.Core.Keys;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>
/// One row of the keyframe dope sheet: a target's merged key set, or, when the
/// target is expanded, one of its channels.
/// </summary>
public readonly record struct AnimatorTrackRow(RigTrack Track, RigChannelProperty? Property, bool IsOverride = false)
{
    public AnimatorTrackRow(RigTrack track)
        : this(track, null)
    {
    }

    public RigChannelTarget Target => Track.Target;
    public string Id => Track.Id;
    public bool IsChannel => Property.HasValue;

    /// <summary>Compact channel name shown on a child row: the label its module declares.</summary>
    public static string PropertyLabel(RigChannelProperty property)
    {
        string name = RigChannels.Name(property);
        foreach (KeyField field in RigChannels.PartFields)
        {
            if (field.Name == name)
                return field.Shown;
        }
        foreach (KeyField field in RigChannels.BoneFields)
        {
            if (field.Name == name)
                return field.Shown;
        }
        return name;
    }

    public static implicit operator AnimatorTrackRow(RigTrack track) => new(track);
}

/// <summary>Builds and reads the dope sheet rows, including expanded channel rows.</summary>
public static class AnimatorTrackRows
{
    /// <summary>
    /// The targets that have keys plus the selected one. An expanded target is
    /// followed by a row per channel it actually animates.
    /// </summary>
    public static List<AnimatorTrackRow> Build(
        RigPackage package,
        RigClip clip,
        RigTrack? selected,
        IReadOnlySet<RigTrack>? expanded)
    {
        var rows = new List<AnimatorTrackRow>();
        foreach (RigTrack track in RigTracks.Of(package, clip, selected))
        {
            rows.Add(new AnimatorTrackRow(track));
            if (expanded is null || !expanded.Contains(track))
                continue;

            foreach (RigChannelProperty property in Channels(clip, track))
                rows.Add(new AnimatorTrackRow(track, property));
        }
        return rows;
    }

    /// <summary>A row per bone with overrides, pins and ellipses, in the order they were added; they hold bars, not keys.</summary>
    public static List<AnimatorTrackRow> Overrides(RigClip clip)
    {
        var rows = new List<AnimatorTrackRow>();
        foreach (RigLimbTarget target in clip.Targets)
        {
            var row = new AnimatorTrackRow(new RigTrack(RigChannelTarget.Bone, target.BoneId), null, true);
            if (!rows.Contains(row))
                rows.Add(row);
        }
        return rows;
    }

    /// <summary>The channels that have any key for a target, in the order its module declares its fields.</summary>
    public static List<RigChannelProperty> Channels(RigClip clip, RigTrack track)
    {
        var channels = new List<RigChannelProperty>();
        KeyTarget target = RigChannels.TargetOf(track.Target, track.Id);
        foreach (KeyField field in KeyRows.Fields(clip, target, RigChannels.FieldsOf(track.Target)))
        {
            if (RigChannels.TryProperty(field.Name, out RigChannelProperty property))
                channels.Add(property);
        }
        return channels;
    }

    /// <summary>
    /// The full view for a selected bone or part: the target, its pins and
    /// ellipses (a bone's), every field it can key, keyed or not, and for a
    /// bone each part on it with that part's fields. A bone that cannot move
    /// leaves out x and y.
    /// </summary>
    public static List<AnimatorTrackRow> Full(RigPackage package, RigClip clip, RigTrack selected)
    {
        var rows = new List<AnimatorTrackRow>();
        AddFull(rows, package, clip, selected);
        if (selected.Target == RigChannelTarget.Bone)
        {
            foreach (RigAttachment part in package.Attachments)
            {
                if (part.BoneId == selected.Id)
                    AddFull(rows, package, clip, new RigTrack(RigChannelTarget.Attachment, part.Id));
            }
        }
        return rows;
    }

    private static void AddFull(List<AnimatorTrackRow> rows, RigPackage package, RigClip clip, RigTrack track)
    {
        rows.Add(new AnimatorTrackRow(track));
        if (track.Target == RigChannelTarget.Bone)
        {
            foreach (RigLimbTarget target in clip.Targets)
            {
                if (target.BoneId == track.Id)
                {
                    rows.Add(new AnimatorTrackRow(track, null, true));
                    break;
                }
            }
        }

        foreach (RigChannelProperty property in Keyable(package, track))
            rows.Add(new AnimatorTrackRow(track, property));
    }

    /// <summary>Every field a target can key, in the order its module declares them.</summary>
    public static List<RigChannelProperty> Keyable(RigPackage package, RigTrack track)
    {
        var properties = new List<RigChannelProperty>();
        bool fixedBone = track.Target == RigChannelTarget.Bone &&
                         RigRig.FindBone(package, track.Id) is int bone && bone >= 0 &&
                         !package.Skeleton.Bones[bone].CanTranslate;
        foreach (KeyField field in RigChannels.FieldsOf(track.Target))
        {
            if (!RigChannels.TryProperty(field.Name, out RigChannelProperty property))
                continue;
            if (fixedBone && property is RigChannelProperty.TranslationX or RigChannelProperty.TranslationY)
                continue;
            properties.Add(property);
        }
        return properties;
    }

    public static bool HasChannels(RigClip clip, RigTrack track) =>
        Channels(clip, track).Count > 0;

    /// <summary>Key times of one row: a channel's own keys, or every channel's for a set row.</summary>
    public static List<float> KeyTimes(RigClip clip, AnimatorTrackRow row)
    {
        if (row.IsOverride)
            return [];

        return KeyRows.KeyTimes(clip, RigChannels.TargetOf(row.Target, row.Id),
            row.Property is { } property ? RigChannels.Name(property) : null);
    }

    /// <summary>The row a hit test selects: the target row or its channel child.</summary>
    public static AnimatorKeyRef Ref(AnimatorTrackRow row, float time) =>
        new(row.Target, row.Id, time, row.Property);
}
