using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;

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

    private static readonly XafChannelProperty[] TransformChannels =
    {
        XafChannelProperty.TranslationX,
        XafChannelProperty.TranslationY,
        XafChannelProperty.Rotation,
        XafChannelProperty.ScaleX,
        XafChannelProperty.ScaleY,
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
        XafClip? clip = AnimatorSession.CurrentClip(document);
        if (clip is null)
            return 0;

        XafPackage package = document.Package;
        XafPose before = XafSampler.Sample(package, clip, document.Runtime.CurrentTime);
        XafPose after = document.Runtime.Pose;

        // Every write below is planned against the clip as it was, so keys
        // that share a time do not see each other's changes.
        var writes = new List<Write>();
        var sampled = new Dictionary<float, XafPose>();
        var edited = new HashSet<(XafChannelTarget, string, float)>();
        foreach (AnimatorKeyRef key in selection.Keys())
        {
            if (!Transforms(package, before, after, key.Target, key.Id, out XafTransform was, out XafTransform now,
                    out bool canTranslate))
                continue;

            foreach (XafChannelProperty property in TransformChannels)
            {
                if (key.Property is { } only && only != property)
                    continue;
                if (XafSampler.IsTranslation(property) && !canTranslate)
                    continue;

                float delta = Get(now, property) - Get(was, property);
                if (Math.Abs(delta) < Epsilon)
                    continue;

                XafChannel? channel = Find(clip, key.Target, key.Id, property);
                XafKeyframe? existing = channel?.Keyframes.Find(
                    frame => Math.Abs(frame.Time - key.Time) <= XafTracks.TimeTolerance);

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
                XafAuthoring.SetChannel(clip, write.Key.Target, write.Key.Id, write.Property, write.Key.Time, write.Value);
        }

        return edited.Count;
    }

    private readonly record struct Write(
        AnimatorKeyRef Key,
        XafChannelProperty Property,
        XafKeyframe? Existing,
        float Value);

    /// <summary>The target's transform before and after the edit, and whether it may translate.</summary>
    private static bool Transforms(
        XafPackage package,
        XafPose before,
        XafPose after,
        XafChannelTarget target,
        string id,
        out XafTransform was,
        out XafTransform now,
        out bool canTranslate)
    {
        was = now = default;
        canTranslate = true;
        switch (target)
        {
            case XafChannelTarget.Bone:
                int bone = package.Skeleton.Bones.FindIndex(candidate => candidate.Id == id);
                if (bone < 0 || bone >= after.Bones.Length)
                    return false;
                (was, now) = (before.Bones[bone], after.Bones[bone]);
                canTranslate = package.Skeleton.Bones[bone].CanTranslate;
                return true;
            case XafChannelTarget.Attachment:
                int attachment = package.Attachments.FindIndex(candidate => candidate.Group == id);
                if (attachment < 0 || attachment >= after.Attachments.Length)
                    return false;
                (was, now) = (before.Attachments[attachment], after.Attachments[attachment]);
                return true;
            case XafChannelTarget.Effect:
                int effect = package.Effects.FindIndex(candidate => candidate.Id == id);
                if (effect < 0 || effect >= after.Effects.Length)
                    return false;
                (was, now) = (before.Effects[effect].Transform, after.Effects[effect].Transform);
                return true;
            default:
                return false;
        }
    }

    private static float ValueAt(
        XafPackage package,
        XafClip clip,
        Dictionary<float, XafPose> sampled,
        AnimatorKeyRef key,
        XafChannelProperty property)
    {
        if (!sampled.TryGetValue(key.Time, out XafPose? pose))
            sampled[key.Time] = pose = XafSampler.Sample(package, clip, key.Time);

        return Transforms(package, pose, pose, key.Target, key.Id, out XafTransform transform, out _, out _)
            ? Get(transform, property)
            : 0f;
    }

    private static XafChannel? Find(XafClip clip, XafChannelTarget target, string id, XafChannelProperty property) =>
        clip.Channels.Find(channel =>
            channel.Target == target && channel.TargetId == id && channel.Property == property);

    private static float Get(XafTransform transform, XafChannelProperty property) => property switch
    {
        XafChannelProperty.TranslationX => transform.Translation.X,
        XafChannelProperty.TranslationY => transform.Translation.Y,
        XafChannelProperty.Rotation => transform.Rotation,
        XafChannelProperty.ScaleX => transform.Scale.X,
        XafChannelProperty.ScaleY => transform.Scale.Y,
        _ => 0f,
    };
}
