using System;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;

namespace Xanadu.Logic.Animator.Commands;

/// <summary>An override being dragged on the timeline, and the span it had when the press landed.</summary>
public sealed record AnimatorOverrideDrag(RigLimbTarget Target, float From, float To);

/// <summary>
/// Retiming an override, a pin or an ellipse, by its bar on the timeline. The drag
/// only moves the span; keys are baked on release, so dragging over other keys and
/// back loses nothing, and the whole drag is one undo step.
/// </summary>
public static class AnimatorOverrideCommands
{
    /// <summary>Hears this class's intents on <paramref name="session"/>.</summary>
    public static void Listen(Entity session)
    {
        session.On<RemoveOverride>((s, e) => Remove(s, s.GetComponent<AnimatorKeyMenu>().Override, e.KeepKeys));
    }

    /// <summary>The shortest an override can be dragged to.</summary>
    public const float MinDuration = 0.01f;

    public static AnimatorOverrideDrag Begin(Entity session, RigLimbTarget target)
    {
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
        AnimatorKeyCommands.SelectTrack(session, new RigTrack(RigChannelTarget.Bone, target.BoneId));
        session.GetComponent<AnimatorSelection>().HasKey = false;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.OverrideSpan };
        Report(session, target);
        return new AnimatorOverrideDrag(target, target.From, target.To);
    }

    /// <summary>Puts the dragged span at new ends; the timeline has already kept it inside its <see cref="Room"/>.</summary>
    public static void Drag(Entity session, AnimatorOverrideDrag drag, double fromSeconds, double toSeconds)
    {
        RigLimbTarget target = drag.Target;
        target.From = (float)fromSeconds;
        target.To = (float)toSeconds;
        Report(session, target);
    }

    /// <summary>Ends the drag; history then bakes the new span, dropping what the old span left behind.</summary>
    public static void End(Entity session, RigClip clip, AnimatorOverrideDrag drag) =>
        AnimatorHistoryCommands.EndGesture(session, () =>
            RigTargets.DropKeysLeftBehind(session.GetComponent<AnimatorDocument>().Package, clip, drag.Target, drag.From, drag.To));

    /// <summary>
    /// Removes an override from the open clip. Removing drops the keys it baked,
    /// so the limb goes back to its own keys; with <paramref name="keepKeys"/> it
    /// detaches instead, leaving them as ordinary keys.
    /// </summary>
    public static void Remove(Entity session, RigLimbTarget? target, bool keepKeys)
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
                RigTargets.DropBakedKeys(document.Package, clip, target);
            clip.Targets.Remove(target);
            document.Runtime.Seek(document.Runtime.CurrentTime);
            AnimatorSession.SetStatus(session, keepKeys
                ? $"{target.BoneId} {AnimatorTargetLabels.Name(target)} DETACHED · ITS KEYS ARE YOURS"
                : $"{target.BoneId} {AnimatorTargetLabels.Name(target)} REMOVED");
        });
    }

    /// <summary>Where an override can be dragged: inside the clip and clear of the bone's other overrides.</summary>
    public static (float Low, float High) Room(RigClip clip, RigLimbTarget target)
    {
        float low = 0f;
        float high = clip.Duration;
        foreach (RigLimbTarget other in clip.Targets)
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

    private static void Report(Entity session, RigLimbTarget target) =>
        AnimatorSession.SetStatus(session,
            $"{target.BoneId} {AnimatorTargetLabels.Name(target)} · {target.From:0.00}-{target.To:0.00} S · {target.To - target.From:0.00} S LONG");

}
