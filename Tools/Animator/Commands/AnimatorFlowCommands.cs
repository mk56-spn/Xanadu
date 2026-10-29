using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Systems.Characters;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// The FLOW tab: which clips the clip being edited may fade into, the fade
/// and curve of each, and a preview of one hand-over as the game plays it.
/// </summary>
public static class AnimatorFlowCommands
{
    /// <summary>The longest the old clip plays before the preview hands over.</summary>
    public const float MaxLeadSeconds = 2f;

    /// <summary>The new clip plays at least this long after its fade, so the settle can be seen.</summary>
    public const float SettleSeconds = 0.5f;

    /// <summary>The longest the new clip plays before the preview ends or starts over.</summary>
    public const float MaxHoldSeconds = 1.5f;

    /// <summary>Joint jumps at a hand-over below this many rig units read as smooth.</summary>
    public const float SmallGap = 8f;

    /// <summary>Joint jumps above this many rig units pop unless the fade covers them.</summary>
    public const float LargeGap = 30f;

    /// <summary>
    /// The clip transitions are edited from: the one being edited, or while a
    /// preview plays, the one that was being edited when it started.
    /// </summary>
    public static string From(Entity session)
    {
        AnimatorSequence sequence = session.GetComponent<AnimatorSequence>();
        return sequence.Active
            ? sequence.OriginClip
            : session.GetComponent<AnimatorDocument>().Runtime.CurrentClip;
    }

    /// <summary>The selected target clip, if it still exists and is not the clip itself.</summary>
    public static XafClip? Target(Entity session)
    {
        string target = session.GetComponent<AnimatorFlow>().Target ?? "";
        if (target.Length == 0 || target == From(session))
            return null;
        return session.GetComponent<AnimatorDocument>().Package.Clips.Find(clip => clip.Id == target);
    }

    /// <summary>The rule from the clip being edited into the selected target, if there is one.</summary>
    public static XafTransition? Rule(Entity session) =>
        Target(session) is { } target
            ? XafTransitions.Find(session.GetComponent<AnimatorDocument>().Package, From(session), target.Id)
            : null;

    /// <summary>The playback speed of every preview: a quarter while SLOW is on.</summary>
    public static float Speed(Entity session) =>
        session.TryGetComponent(out AnimatorFlow flow) ? flow.Speed : 1f;

    /// <summary>The hand-overs the game's motion system can ask this rig for.</summary>
    public static List<(string From, string To)> GameTransitions(XafPackage package) =>
        CharacterMotionSystem.Transitions(package);

    public static void Select(Entity session, string target)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (!package.Clips.Exists(clip => clip.Id == target) || target == From(session))
            return;

        session.GetComponent<AnimatorFlow>().Target = target;
        AnimatorSession.SetStatus(session, $"{Pair(From(session), target)} SELECTED");
    }

    /// <summary>
    /// Allows the hand-over into the target, or forbids it if it was allowed,
    /// and selects it either way. The first rule closes an open table, so the
    /// status says that every other hand-over now needs a rule too.
    /// </summary>
    public static void Toggle(Entity session, string target)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        string from = From(session);
        if (!package.Clips.Exists(clip => clip.Id == target) || target == from)
            return;

        session.GetComponent<AnimatorFlow>().Target = target;
        bool wasOpen = XafTransitions.IsOpen(package);
        bool allowed = XafTransitions.Find(package, from, target) is null;
        AnimatorHistoryCommands.Edit(session, () =>
        {
            if (allowed)
                XafTransitions.Allow(package, from, target);
            else
                XafTransitions.Forbid(package, from, target);
        });

        string message = allowed ? $"{Pair(from, target)} ALLOWED" : $"{Pair(from, target)} FORBIDDEN";
        if (allowed && wasOpen)
            message += ": OTHER HAND-OVERS NOW NEED A RULE";
        else if (!allowed && XafTransitions.IsOpen(package))
            message += ": NO RULES LEFT, ANY CLIP CAN FOLLOW ANY";
        AnimatorSession.SetStatus(session, message);
    }

    public static void StepFade(Entity session, float delta)
    {
        if (Rule(session) is not { } rule)
        {
            AnimatorSession.SetStatus(session, "ALLOW A TRANSITION FIRST");
            return;
        }

        // Snap to the step grid, so a few presses land on round values.
        float steps = MathF.Round(rule.Fade / XafTransitions.FadeStep);
        float fade = XafTransitions.ClampFade((steps + MathF.Sign(delta)) * XafTransitions.FadeStep);
        AnimatorHistoryCommands.Edit(session, () => rule.Fade = fade);
        AnimatorSession.SetStatus(session, $"{Pair(rule.From, rule.To)} FADE {fade:0.00} S");
    }

    public static void SetCurve(Entity session, string argument)
    {
        if (!Enum.TryParse(argument, out XafFadeCurve curve))
            return;
        if (Rule(session) is not { } rule)
        {
            AnimatorSession.SetStatus(session, "ALLOW A TRANSITION FIRST");
            return;
        }

        AnimatorHistoryCommands.Edit(session, () => rule.Curve = curve);
        AnimatorSession.SetStatus(session, $"{Pair(rule.From, rule.To)} {CurveName(curve)}");
    }

    /// <summary>
    /// Plays the edited clip into the selected one: the old clip for one full
    /// play, the hand-over with its fade, then the new clip settling. A
    /// hand-over with no rule previews the default fade and says so.
    /// </summary>
    public static void Preview(Entity session)
    {
        string from = From(session);
        if (Target(session) is not { } target)
        {
            AnimatorSession.SetStatus(session, "SELECT A CLIP TO FADE INTO");
            return;
        }

        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        AnimatorFlow flow = session.GetComponent<AnimatorFlow>();
        AnimatorSequenceCommands.Start(session, Steps(package, from, target.Id), "FLOW", flow.Loop);
        if (!XafTransitions.Allows(package, from, target.Id))
            AnimatorSession.SetStatus(session, $"{Pair(from, target.Id)} NOT ALLOWED: PREVIEWING THE DEFAULT FADE");
    }

    /// <summary>The two steps of a hand-over preview: lead in on the old clip, settle on the new.</summary>
    public static List<AnimatorSequenceStep> Steps(XafPackage package, string from, string to)
    {
        (float fade, _) = XafTransitions.Blend(package, from, to);
        float lead = Math.Min(MaxLeadSeconds, AnimatorSequenceCommands.FullSeconds(package, from));
        float hold = Math.Max(
            fade + SettleSeconds,
            Math.Min(MaxHoldSeconds, AnimatorSequenceCommands.FullSeconds(package, to)));
        return new List<AnimatorSequenceStep>
        {
            new(from, lead),
            new(to, hold),
        };
    }

    public static void ToggleLoop(Entity session)
    {
        ref AnimatorFlow flow = ref session.GetComponent<AnimatorFlow>();
        flow.Loop = !flow.Loop;
        ref AnimatorSequence sequence = ref session.GetComponent<AnimatorSequence>();
        if (sequence.Active && sequence.Name == "FLOW")
            sequence.Loop = flow.Loop;
        AnimatorSession.SetStatus(session, flow.Loop ? "FLOW PREVIEW REPEATS" : "FLOW PREVIEW PLAYS ONCE");
    }

    public static void ToggleSlow(Entity session)
    {
        ref AnimatorFlow flow = ref session.GetComponent<AnimatorFlow>();
        flow.Slow = !flow.Slow;
        AnimatorSession.SetStatus(session, flow.Slow ? "SLOW MOTION: QUARTER SPEED" : "FULL SPEED");
    }

    /// <summary>
    /// How far the rig jumps when the edited clip hands over to the target:
    /// measured where the preview hands over, after one full play of the old clip.
    /// </summary>
    public static (float Distance, string BoneId)? Gap(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        XafClip? from = package.Clips.Find(clip => clip.Id == From(session));
        if (from is null || Target(session) is not { } target)
            return null;

        float lead = Math.Min(MaxLeadSeconds, AnimatorSequenceCommands.FullSeconds(package, from.Id));
        return XafTransitions.Gap(package, from, AnimatorRuntime.WrapTime(from, lead), target);
    }

    public static string CurveName(XafFadeCurve curve) => curve switch
    {
        XafFadeCurve.Smooth => "SMOOTH",
        XafFadeCurve.EaseIn => "EASE IN",
        XafFadeCurve.EaseOut => "EASE OUT",
        _ => "LINEAR",
    };

    public static string Pair(string from, string to) =>
        $"{from.ToUpperInvariant()} ▸ {to.ToUpperInvariant()}";
}
