using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Timeline;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>An override being dragged on the timeline, and the span it had when the press landed.</summary>
public sealed record AnimatorOverrideDrag(XafLimbTarget Target, float From, float To);

/// <summary>
/// Retiming an override, a pin or an ellipse, by its bar on the timeline. The drag
/// only moves the span; keys are baked on release, so dragging over other keys and
/// back loses nothing, and the whole drag is one undo step.
/// </summary>
public static class AnimatorOverrideCommands
{
    /// <summary>The shortest an override can be dragged to.</summary>
    public const float MinDuration = 0.01f;

    public static AnimatorOverrideDrag Begin(Entity session, XafLimbTarget target)
    {
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
        AnimatorKeyCommands.SelectTrack(session, new XafTrack(XafChannelTarget.Bone, target.BoneId));
        session.GetComponent<AnimatorSelection>().HasKey = false;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.OverrideSpan };
        Report(session, target);
        return new AnimatorOverrideDrag(target, target.From, target.To);
    }

    /// <summary>Puts the dragged span at new ends; the timeline has already kept it inside its <see cref="Room"/>.</summary>
    public static void Drag(Entity session, AnimatorOverrideDrag drag, double fromSeconds, double toSeconds)
    {
        XafLimbTarget target = drag.Target;
        target.From = (float)fromSeconds;
        target.To = (float)toSeconds;
        Report(session, target);
    }

    /// <summary>Ends the drag; history then bakes the new span, dropping what the old span left behind.</summary>
    public static void End(Entity session, XafClip clip, AnimatorOverrideDrag drag) =>
        AnimatorHistoryCommands.EndGesture(session, () =>
            XafTargets.DropKeysLeftBehind(session.GetComponent<AnimatorDocument>().Package, clip, drag.Target, drag.From, drag.To));

    /// <summary>
    /// Removes an override from the open clip. Removing drops the keys it baked,
    /// so the limb goes back to its own keys; with <paramref name="keepKeys"/> it
    /// detaches instead, leaving them as ordinary keys.
    /// </summary>
    public static void Remove(Entity session, XafLimbTarget? target, bool keepKeys)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (target is null || AnimatorSession.CurrentClip(document) is not { } clip || !clip.Targets.Contains(target))
        {
            AnimatorSession.SetStatus(session, "RIGHT-CLICK AN OVERRIDE BAR FIRST");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (!keepKeys)
                XafTargets.DropBakedKeys(document.Package, clip, target);
            clip.Targets.Remove(target);
            document.Runtime.Seek(document.Runtime.CurrentTime);
            AnimatorSession.SetStatus(session, keepKeys
                ? $"{target.BoneId} {Name(target)} DETACHED · ITS KEYS ARE YOURS"
                : $"{target.BoneId} {Name(target)} REMOVED");
        });
    }

    /// <summary>Where an override can be dragged: inside the clip and clear of the bone's other overrides.</summary>
    public static (float Low, float High) Room(XafClip clip, XafLimbTarget target)
    {
        float low = 0f;
        float high = clip.Duration;
        foreach (XafLimbTarget other in clip.Targets)
        {
            if (ReferenceEquals(other, target) || other.BoneId != target.BoneId)
                continue;
            if (other.To <= target.From)
                low = Math.Max(low, other.To);
            else if (other.From >= target.To)
                high = Math.Min(high, other.From);
        }
        return (low, high);
    }

    private static void Report(Entity session, XafLimbTarget target) =>
        AnimatorSession.SetStatus(session,
            $"{target.BoneId} {Name(target)} · {target.From:0.00}-{target.To:0.00} S · {target.To - target.From:0.00} S LONG");

    /// <summary>What the override is called on its bar and in the status line.</summary>
    public static string Name(XafLimbTarget target) =>
        target.Shape == XafTargetShape.Spot ? "PIN" : "ELLIPSE";
}
