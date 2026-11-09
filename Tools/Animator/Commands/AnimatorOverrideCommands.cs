using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Timeline;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>The part of an override's bar on the timeline that a press took.</summary>
/// <summary>An override being dragged on the timeline, and where it was when the press landed.</summary>
public sealed record AnimatorOverrideDrag(
    XafLimbTarget Target,
    TimelineGrabPart Grab,
    float GrabbedAt,
    float From,
    float To)
{
    /// <summary>The same drag as the shared timeline span drag, in milliseconds.</summary>
    public TimelineSpanDrag Span => new(Grab, From * 1000.0, To * 1000.0, GrabbedAt * 1000.0);
}

/// <summary>
/// Retiming an override, a pin or an ellipse, by its bar on the timeline. The drag
/// only moves the span; keys are baked on release, so dragging over other keys and
/// back loses nothing, and the whole drag is one undo step.
/// </summary>
public static class AnimatorOverrideCommands
{
    /// <summary>The shortest an override can be dragged to.</summary>
    public const float MinDuration = 0.01f;

    public static AnimatorOverrideDrag Begin(
        Entity session,
        XafLimbTarget target,
        TimelineGrabPart grab,
        float seconds)
    {
        AnimatorHistoryCommands.BeginGesture(session);
        session.GetComponent<AnimatorDocument>().Runtime.SetPaused(true);
        AnimatorKeyCommands.SelectTrack(session, new XafTrack(XafChannelTarget.Bone, target.BoneId));
        session.GetComponent<AnimatorSelection>().HasKey = false;
        session.GetComponent<AnimatorDrag>() = new AnimatorDrag { Kind = AnimatorDragKind.OverrideSpan };
        Report(session, target);
        return new AnimatorOverrideDrag(target, grab, seconds, target.From, target.To);
    }

    /// <summary>Moves the grabbed part to a time, kept inside the clip and clear of the bone's other overrides.</summary>
    public static void Drag(
        Entity session,
        XafClip clip,
        AnimatorOverrideDrag drag,
        double seconds,
        Func<double, double> snap)
    {
        (float low, float high) = Room(clip, drag);
        (double from, double to) = drag.Span.Apply(
            seconds * 1000.0,
            ms => snap(ms / 1000.0) * 1000.0,
            low * 1000.0,
            high * 1000.0,
            MinDuration * 1000.0);
        XafLimbTarget target = drag.Target;
        target.From = (float)(from / 1000.0);
        target.To = (float)(to / 1000.0);
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

    private static (float Low, float High) Room(XafClip clip, AnimatorOverrideDrag drag)
    {
        float low = 0f;
        float high = clip.Duration;
        foreach (XafLimbTarget other in clip.Targets)
        {
            if (ReferenceEquals(other, drag.Target) || other.BoneId != drag.Target.BoneId)
                continue;
            if (other.To <= drag.From)
                low = Math.Max(low, other.To);
            else if (other.From >= drag.To)
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
