using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Keys;
using Xanadu.Model.Animation;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// How the inspector's position, rotation and scale stand against the current
/// clip's keys, for the selected bone or part: keyed at the playhead (yellow),
/// driven by keys elsewhere (green), or posed away from what the keys say and
/// not keyed yet (orange). Nothing in rest mode, without a clip, or for fields
/// that are not keyed per frame.
/// </summary>
public static class AnimatorKeyMarks
{
    /// <summary>How close a key must be to the playhead to count as on it, in seconds.</summary>
    public const float OnKey = 0.001f;

    public static UiValueMark For(Entity session, AnimatorFieldKind kind)
    {
        RigChannelProperty[] properties = kind switch
        {
            AnimatorFieldKind.Position => [RigChannelProperty.TranslationX, RigChannelProperty.TranslationY],
            AnimatorFieldKind.Rotation => [RigChannelProperty.Rotation],
            AnimatorFieldKind.Scale => [RigChannelProperty.ScaleX, RigChannelProperty.ScaleY],
            _ => [],
        };
        if (properties.Length == 0 || session.GetComponent<AnimatorModes>().Rest)
            return UiValueMark.None;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (AnimatorSession.CurrentClip(document) is not { } clip)
            return UiValueMark.None;

        AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
        RigPackage package = document.Package;
        RigChannelTarget target;
        string id;
        int index;
        if (selection.Bone >= 0 && selection.Bone < package.Skeleton.Bones.Count)
            (target, id, index) = (RigChannelTarget.Bone, package.Skeleton.Bones[selection.Bone].Id, selection.Bone);
        else if (selection.Attachment >= 0 && selection.Attachment < package.Attachments.Count)
            (target, id, index) = (RigChannelTarget.Attachment, package.Attachments[selection.Attachment].Id, selection.Attachment);
        else
            return UiValueMark.None;

        float time = document.Runtime.CurrentTime;
        bool keyed = false, animated = false;
        foreach (RigChannelProperty property in properties)
        {
            if (clip.Find(RigChannels.At(target, id, property)) is not { Keys.Count: > 0 } channel)
                continue;
            animated = true;
            foreach (Keyframe key in channel.Keys)
                keyed |= Math.Abs(key.Time - time) <= OnKey;
        }

        RigPose sampled = RigSampler.Sample(package, clip, time);
        RigTransform[] shown = target == RigChannelTarget.Bone ? document.Runtime.Pose.Bones : document.Runtime.Pose.Attachments;
        RigTransform[] keys = target == RigChannelTarget.Bone ? sampled.Bones : sampled.Attachments;
        if (index < shown.Length && index < keys.Length && !Same(kind, shown[index], keys[index]))
            return UiValueMark.Changed;

        return keyed ? UiValueMark.Keyed : animated ? UiValueMark.Animated : UiValueMark.None;
    }

    private static bool Same(AnimatorFieldKind kind, RigTransform a, RigTransform b) => kind switch
    {
        AnimatorFieldKind.Position => a.Translation.DistanceSquaredTo(b.Translation) < 0.0001f,
        AnimatorFieldKind.Rotation => Math.Abs(a.Rotation - b.Rotation) < 0.001f,
        _ => a.Scale.DistanceSquaredTo(b.Scale) < 0.0001f,
    };
}
