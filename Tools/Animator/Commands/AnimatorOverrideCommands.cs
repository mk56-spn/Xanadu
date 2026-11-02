using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>The part of an override's bar on the timeline that a press took.</summary>
public enum AnimatorOverrideGrab
{
    Start,
    End,
    Body,
}

/// <summary>An override being dragged on the timeline, and where it was when the press landed.</summary>
public sealed record AnimatorOverrideDrag(
    XafLimbTarget Target,
    AnimatorOverrideGrab Grab,
    float GrabbedAt,
    float From,
    float To);

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
        AnimatorOverrideGrab grab,
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
        XafLimbTarget target = drag.Target;
        switch (drag.Grab)
        {
            case AnimatorOverrideGrab.Start:
                target.From = Math.Clamp((float)snap(seconds), low, Math.Max(low, drag.To - MinDuration));
                break;
            case AnimatorOverrideGrab.End:
                target.To = Math.Clamp((float)snap(seconds), Math.Min(high, drag.From + MinDuration), high);
                break;
            default:
                float length = drag.To - drag.From;
                float from = (float)snap(drag.From + seconds - drag.GrabbedAt);
                target.From = Math.Clamp(from, low, Math.Max(low, high - length));
                target.To = target.From + length;
                break;
        }
        Report(session, target);
    }

    /// <summary>Ends the drag; history then bakes the new span, dropping what the old span left behind.</summary>
    public static void End(Entity session, XafClip clip, AnimatorOverrideDrag drag) =>
        AnimatorHistoryCommands.EndGesture(session, () =>
            XafTargets.DropKeysLeftBehind(session.GetComponent<AnimatorDocument>().Package, clip, drag.Target, drag.From, drag.To));

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
