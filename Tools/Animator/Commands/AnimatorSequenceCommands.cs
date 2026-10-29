using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Previews clips playing into each other without starting the game: a chain
/// is a list of clips with how long each plays, and each hands over to the
/// next with the fade and curve the package's transition table gives it.
/// </summary>
public static class AnimatorSequenceCommands
{
    /// <summary>The crossfade of a hand-over that has no rule, as in the game.</summary>
    public const float FadeSeconds = XafTransitions.DefaultFade;

    /// <summary>How long the game holds a jump before it can turn into a fall.</summary>
    public const float JumpSeconds = 0.3f;

    /// <summary>A stand-in for the time spent falling, which in the game depends on the drop.</summary>
    public const float FallSeconds = 0.5f;

    /// <summary>How long idle plays after the landing, so the settle into it can be seen.</summary>
    public const float IdleSeconds = 1f;

    private const float MinSeconds = 0.1f;

    /// <summary>Jump, fall, land and settle to idle, using the clips the rig has and the game's own fallbacks.</summary>
    public static List<AnimatorSequenceStep> GameSteps(XafPackage package)
    {
        var steps = new List<AnimatorSequenceStep>();
        if (Has(package, "jumping"))
            Add(steps, "jumping", JumpSeconds);

        // A rig without a falling clip keeps its jump pose while falling.
        string? fall = Has(package, "falling") ? "falling" : Has(package, "jumping") ? "jumping" : null;
        if (fall is not null)
            Add(steps, fall, FallSeconds);

        if (Has(package, "landing"))
            Add(steps, "landing", FullSeconds(package, "landing"));
        if (Has(package, "idle"))
            Add(steps, "idle", IdleSeconds);
        return steps;
    }

    /// <summary>Every clip in the file in turn, each for one full play.</summary>
    public static List<AnimatorSequenceStep> AllSteps(XafPackage package)
    {
        var steps = new List<AnimatorSequenceStep>();
        foreach (XafClip clip in package.Clips)
            Add(steps, clip.Id, FullSeconds(package, clip.Id));
        return steps;
    }

    public static void StartGame(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        Start(session, GameSteps(package), "GAME CHAIN", strict: true);
    }

    public static void StartAll(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        Start(session, AllSteps(package), "ALL CLIPS");
    }

    /// <summary>Begins a chain at its first clip, from the start, and plays it.</summary>
    public static void Start(
        Entity session,
        List<AnimatorSequenceStep> steps,
        string name,
        bool loop = false,
        bool strict = false)
    {
        // A chain of one clip is just playing it, so it needs two to mean anything.
        if (steps.Count < 2)
        {
            AnimatorSession.SetStatus(session, $"{name}: NEEDS AT LEAST TWO CLIPS");
            return;
        }

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        AnimatorSequence running = session.GetComponent<AnimatorSequence>();

        // Restarting a running chain still ends on what was being edited before it.
        string originClip = running.Active ? running.OriginClip : runtime.CurrentClip;
        float originTime = running.Active ? running.OriginTime : runtime.CurrentTime;
        runtime.Play(steps[0].Clip, 0f);
        runtime.Restart();
        runtime.SetPaused(false);
        session.GetComponent<AnimatorSelection>().HasKey = false;

        session.GetComponent<AnimatorSequence>() = new AnimatorSequence
        {
            Steps = steps,
            OriginClip = originClip,
            OriginTime = originTime,
            Name = name,
            Loop = loop,
            Strict = strict,
        };
        AnimatorSession.SetStatus(session, Describe(name, steps, 0));
    }

    /// <summary>
    /// Ends the chain and puts the editor back on the clip and time it started
    /// from, paused, so the preview never leaves you on whatever played last.
    /// </summary>
    public static void Finish(Entity session)
    {
        AnimatorSequence sequence = session.GetComponent<AnimatorSequence>();
        if (!sequence.Active)
            return;

        session.GetComponent<AnimatorSequence>() = default;
        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        if (runtime.Play(sequence.OriginClip, 0f))
            runtime.Seek(sequence.OriginTime);
        runtime.SetPaused(true);
    }

    /// <summary>Moves a running chain on: hands over at the end of a step, and ends if the user took over.</summary>
    public static void Tick(Entity session, float delta)
    {
        ref AnimatorSequence sequence = ref session.GetComponent<AnimatorSequence>();
        if (sequence.Steps is not { } steps)
            return;

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        if (!OnTrack(runtime, steps, sequence.Index))
        {
            sequence = default;
            AnimatorSession.SetStatus(session, "CHAIN ENDED");
            return;
        }

        if (runtime.Paused)
            return;

        sequence.Elapsed += delta;
        if (sequence.Elapsed < steps[sequence.Index].Seconds)
            return;

        sequence.Elapsed -= steps[sequence.Index].Seconds;
        sequence.Index++;
        if (sequence.Index >= steps.Count && sequence.Loop)
        {
            // Back to the start with a cut, so each pass shows the same hand-overs.
            sequence.Index = 0;
            sequence.Elapsed = 0f;
            runtime.Play(steps[0].Clip, 0f);
            runtime.Restart();
            return;
        }

        if (sequence.Index >= steps.Count)
        {
            Finish(session);
            AnimatorSession.SetStatus(session, "CHAIN DONE");
            return;
        }

        string from = steps[sequence.Index - 1].Clip;
        string clip = steps[sequence.Index].Clip;
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        if (sequence.Strict && !runtime.Transition(clip))
        {
            // As in the game: the old clip plays on through the refused step.
            steps[sequence.Index] = steps[sequence.Index] with { Clip = from };
            AnimatorSession.SetStatus(session,
                $"{from.ToUpperInvariant()} ▸ {clip.ToUpperInvariant()} NOT ALLOWED: GAME STAYS ON {from.ToUpperInvariant()}");
            return;
        }

        if (!sequence.Strict)
        {
            (float fade, XafFadeCurve curve) = XafTransitions.Blend(package, from, clip);
            runtime.Play(clip, fade, curve);
        }
        AnimatorSession.SetStatus(session, Describe(sequence.Name, steps, sequence.Index));
    }

    /// <summary>Whether the runtime is still playing what the chain expects, so a clip picked by hand ends it.</summary>
    private static bool OnTrack(AnimatorRuntime runtime, List<AnimatorSequenceStep> steps, int index)
    {
        string clip = steps[index].Clip;
        if (runtime.CurrentClip == clip || runtime.NextClip == clip)
            return true;

        // Fading in from the previous step.
        return index > 0 && runtime.IsCrossfading && runtime.CurrentClip == steps[index - 1].Clip;
    }

    private static string Describe(string name, List<AnimatorSequenceStep> steps, int index) =>
        $"{name} {index + 1}/{steps.Count}: {steps[index].Clip.ToUpperInvariant()}";

    /// <summary>Adds a step, folding it into the last when it is the same clip, since nothing changes between them.</summary>
    private static void Add(List<AnimatorSequenceStep> steps, string clip, float seconds)
    {
        if (steps.Count > 0 && steps[^1].Clip == clip)
            steps[^1] = steps[^1] with { Seconds = steps[^1].Seconds + seconds };
        else
            steps.Add(new AnimatorSequenceStep(clip, Math.Max(MinSeconds, seconds)));
    }

    /// <summary>One full play of a clip: its length, or one pass of the loop range when it loops.</summary>
    public static float FullSeconds(XafPackage package, string id)
    {
        XafClip? clip = package.Clips.Find(candidate => candidate.Id == id);
        if (clip is null)
            return MinSeconds;
        return Math.Max(MinSeconds, clip.Loop ? clip.LoopEnd - clip.LoopStart : clip.Duration);
    }

    private static bool Has(XafPackage package, string id) =>
        package.Clips.Exists(clip => clip.Id == id);
}
