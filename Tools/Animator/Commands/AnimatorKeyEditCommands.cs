using Xanadu.Core.Keys;
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Tools.Animator.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Editing the selected keys by posing the rig. The edit is measured against
/// what the clip gives at the playhead, and that change is added to every
/// selected key of the edited bone, attachment or effect: one key is simply
/// overwritten, several keep the shape of their motion and move together.
/// Only the channels the edit changed are written.
/// </summary>
public static class AnimatorKeyEditCommands
{
    /// <summary>Changes smaller than this are rounding, not an edit.</summary>
    private const float Epsilon = 1e-5f;

    private static readonly RigChannelProperty[] TransformChannels =
    {
        RigChannelProperty.TranslationX,
        RigChannelProperty.TranslationY,
        RigChannelProperty.Rotation,
        RigChannelProperty.ScaleX,
        RigChannelProperty.ScaleY,
        RigChannelProperty.Depth,
    };

    /// <summary>
    /// Writes the pose edit into the selected keys. Returns how many keys it
    /// changed; zero when no key is selected or none belongs to what was
    /// edited, so the caller can fall back to keying at the playhead.
    /// </summary>
    public static int Apply(Entity session)
    {
        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        if (!selection.HasKey || session.GetComponent<AnimatorModes>().Rest)
            return 0;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        RigClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return 0;

        RigPackage package = document.Package;
        RigPose before = RigSampler.Sample(package, clip, document.Runtime.CurrentTime);
        RigPose after = document.Runtime.Pose;

        // Every write below is planned against the clip as it was, so keys
        // that share a time do not see each other's changes.
        var writes = new List<Write>();
        var sampled = new Dictionary<float, RigPose>();
        var edited = new HashSet<(RigChannelTarget, string, float)>();
        foreach (AnimatorKeyRef key in selection.Keys())
        {
            if (!Transforms(package, before, after, key.Target, key.Id, out RigTransform was, out RigTransform now,
                    out bool canTranslate))
                continue;

            foreach (RigChannelProperty property in TransformChannels)
            {
                if (key.Property is { } only && only != property)
                    continue;
                if (RigSampler.IsTranslation(property) && !canTranslate)
                    continue;

                float delta = Get(now, property) - Get(was, property);
                if (Math.Abs(delta) < Epsilon)
                    continue;

                KeyChannel? channel = Find(clip, key.Target, key.Id, property);
                Keyframe? existing = channel?.Keys.Find(
                    frame => Math.Abs(frame.Time - key.Time) <= RigTracks.TimeTolerance);

                // A key the channel lacks at that time is made from what the clip gives there.
                float value = existing?.Value ?? ValueAt(package, clip, sampled, key, property);
                writes.Add(new Write(key, property, existing, value + delta));
                edited.Add((key.Target, key.Id, key.Time));
            }
        }

        if (writes.Count == 0)
            return 0;

        foreach (Write write in writes)
        {
            if (write.Existing is not null)
                write.Existing.Value = write.Value;
            else
                RigAuthoring.SetChannel(clip, write.Key.Target, write.Key.Id, write.Property, write.Key.Time, write.Value);
        }

        return edited.Count;
    }

    private readonly record struct Write(
        AnimatorKeyRef Key,
        RigChannelProperty Property,
        Keyframe? Existing,
        float Value);

    /// <summary>The target's transform before and after the edit, and whether it may translate.</summary>
    private static bool Transforms(
        RigPackage package,
        RigPose before,
        RigPose after,
        RigChannelTarget target,
        string id,
        out RigTransform was,
        out RigTransform now,
        out bool canTranslate)
    {
        was = now = default;
        canTranslate = true;
        switch (target)
        {
            case RigChannelTarget.Bone:
                int bone = package.Skeleton.Bones.FindIndex(candidate => candidate.Id == id);
                if (bone < 0 || bone >= after.Bones.Length)
                    return false;
                (was, now) = (before.Bones[bone], after.Bones[bone]);
                canTranslate = package.Skeleton.Bones[bone].CanTranslate;
                return true;
            case RigChannelTarget.Attachment:
                int attachment = package.Attachments.FindIndex(candidate => candidate.Id == id);
                if (attachment < 0 || attachment >= after.Attachments.Length)
                    return false;
                (was, now) = (before.Attachments[attachment], after.Attachments[attachment]);
                return true;
            default:
                return false;
        }
    }

    private static float ValueAt(
        RigPackage package,
        RigClip clip,
        Dictionary<float, RigPose> sampled,
        AnimatorKeyRef key,
        RigChannelProperty property)
    {
        if (!sampled.TryGetValue(key.Time, out RigPose? pose))
            sampled[key.Time] = pose = RigSampler.Sample(package, clip, key.Time);

        return Transforms(package, pose, pose, key.Target, key.Id, out RigTransform transform, out _, out _)
            ? Get(transform, property)
            : 0f;
    }

    private static KeyChannel? Find(RigClip clip, RigChannelTarget target, string id, RigChannelProperty property) =>
        clip.Channels.Find(channel =>
            channel.Target == target && channel.TargetId == id && channel.Property == property);

    private static float Get(RigTransform transform, RigChannelProperty property) => property switch
    {
        RigChannelProperty.TranslationX => transform.Translation.X,
        RigChannelProperty.TranslationY => transform.Translation.Y,
        RigChannelProperty.Rotation => transform.Rotation,
        RigChannelProperty.ScaleX => transform.Scale.X,
        RigChannelProperty.ScaleY => transform.Scale.Y,
        RigChannelProperty.Depth => transform.Depth,
        _ => 0f,
    };
}
