using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Cycles the contact rig's attacks in context, on a metronome. Each attack
/// fires on a beat the way <see cref="Xanadu.Systems.Characters.CharacterAttackSystem"/>
/// fires one on a note hit (<see cref="XafStrike"/>: from its contact frame,
/// over the base, fading back out), so the hit can be judged against the click.
/// </summary>
public static class AnimatorAttackCycleCommands
{
    /// <summary>High to low on the ground, then the same in the air.</summary>
    public static readonly ContactClip[] Attacks =
    {
        ContactClip.AttackHigh,
        ContactClip.AttackMid,
        ContactClip.AttackLow,
        ContactClip.AttackAirHigh,
        ContactClip.AttackAirMid,
        ContactClip.AttackAirLow,
    };

    /// <summary>The attacks the rig has whose base it also has, in play order.</summary>
    public static List<ContactClip> Order(XafPackage package)
    {
        var order = new List<ContactClip>();
        foreach (ContactClip attack in Attacks)
        {
            if (package.Has(attack) && Base(package, attack) is not null)
                order.Add(attack);
        }

        return order;
    }

    /// <summary>
    /// What the game plays under an attack: idle on the ground; in the air the
    /// fall, or the jump pose a rig without a falling clip keeps instead.
    /// </summary>
    public static string? Base(XafPackage package, ContactClip attack)
    {
        bool air = attack is ContactClip.AttackAirHigh or ContactClip.AttackAirMid or ContactClip.AttackAirLow;
        ContactClip[] candidates = air
            ? new[] { ContactClip.Falling, ContactClip.Jumping }
            : new[] { ContactClip.Idle };
        foreach (ContactClip candidate in candidates)
        {
            if (package.Has(candidate))
                return candidate.Id();
        }

        return null;
    }

    public static void Toggle(Entity session)
    {
        if (session.GetComponent<AnimatorAttackCycle>().Active)
        {
            Finish(session);
            AnimatorSession.SetStatus(session, "ATTACKS OFF");
        }
        else
        {
            Start(session);
        }
    }

    /// <summary>
    /// Starts on a beat, with the first attack on it, in place of any running
    /// chain. Returns the beat so the caller can sound it.
    /// </summary>
    public static AnimatorBeat Start(Entity session)
    {
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        List<ContactClip> order = Order(document.Package);

        // Editing one attack: fire only that one, so it can be judged against the click.
        if (ContactClips.FromId(document.Runtime.CurrentClip) is { } editing && order.Contains(editing))
            order = new List<ContactClip> { editing };

        if (order.Count == 0)
        {
            AnimatorSession.SetStatus(session, "ATTACKS: NO ATTACK CLIPS");
            return AnimatorBeat.None;
        }

        AnimatorSequenceCommands.Finish(session);
        AnimatorRuntime runtime = document.Runtime;
        session.GetComponent<AnimatorAttackCycle>() = new AnimatorAttackCycle
        {
            Order = order,
            Runtime = runtime,
            OriginClip = runtime.CurrentClip,
            OriginTime = runtime.CurrentTime,
        };
        session.GetComponent<AnimatorSelection>().HasKey = false;
        runtime.SetPaused(false);
        Fire(session, cut: true);
        return AnimatorBeat.Hit;
    }

    /// <summary>
    /// Takes the layer off and puts the editor back on the clip and time it
    /// started from, paused, as a chain does when it ends.
    /// </summary>
    public static void Finish(Entity session)
    {
        AnimatorAttackCycle cycle = Drop(session);
        if (!cycle.Active)
            return;

        AnimatorRuntime runtime = session.GetComponent<AnimatorDocument>().Runtime;
        if (runtime.Play(cycle.OriginClip, 0f))
            runtime.Seek(cycle.OriginTime);
        runtime.SetPaused(true);
    }

    /// <summary>Counts the beat on and fires the next attack when one is due; says which beat it was, if any.</summary>
    public static AnimatorBeat Tick(Entity session, float delta)
    {
        ref AnimatorAttackCycle cycle = ref session.GetComponent<AnimatorAttackCycle>();
        if (cycle.Order is not { } order)
            return AnimatorBeat.None;

        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorRuntime runtime = document.Runtime;
        if (!ReferenceEquals(runtime, cycle.Runtime) || !OnBase(runtime, Base(document.Package, order[cycle.Index])))
        {
            // A clip picked by hand, a chain started or the rig rebuilt: leave
            // what is playing alone and just stop layering.
            Drop(session);
            AnimatorSession.SetStatus(session, "ATTACKS ENDED");
            return AnimatorBeat.None;
        }

        if (runtime.Paused)
            return AnimatorBeat.None;

        if (Clip(document.Package, order[cycle.Index]) is { } playing)
            XafStrike.Step(runtime, playing);

        AnimatorMetronome metronome = session.GetComponent<AnimatorMetronome>();
        cycle.BeatClock += delta;
        if (cycle.BeatClock < metronome.BeatSeconds)
            return AnimatorBeat.None;

        // One beat a frame at most: a long frame drops beats rather than stacking hits.
        cycle.BeatClock = Math.Min(cycle.BeatClock - metronome.BeatSeconds, metronome.BeatSeconds);
        cycle.Beat++;
        if (cycle.Beat % Math.Max(1, metronome.Every) != 0)
            return AnimatorBeat.Beat;

        runtime.RemoveLayer(order[cycle.Index].Id());
        cycle.Index = (cycle.Index + 1) % order.Count;
        Fire(session, cut: false);
        return AnimatorBeat.Hit;
    }

    /// <summary>Steps the tempo by <see cref="AnimatorMetronome.BpmStep"/>.</summary>
    public static void StepBpm(Entity session, int direction)
    {
        ref AnimatorMetronome metronome = ref session.GetComponent<AnimatorMetronome>();
        metronome.Bpm = Math.Clamp(metronome.Bpm + direction * AnimatorMetronome.BpmStep,
            AnimatorMetronome.MinBpm, AnimatorMetronome.MaxBpm);
        AnimatorSession.SetStatus(session, $"METRONOME {metronome.Bpm} BPM");
    }

    /// <summary>Steps how many beats pass between attacks.</summary>
    public static void StepEvery(Entity session, int direction)
    {
        ref AnimatorMetronome metronome = ref session.GetComponent<AnimatorMetronome>();
        metronome.Every = Math.Clamp(metronome.Every + direction, 1, AnimatorMetronome.MaxEvery);
        AnimatorSession.SetStatus(session, metronome.Every == 1
            ? "AN ATTACK ON EVERY BEAT"
            : $"AN ATTACK EVERY {metronome.Every} BEATS");
    }

    /// <summary>Fires the current attack from its contact frame, moving to its base first when that changes.</summary>
    private static void Fire(Entity session, bool cut)
    {
        ref AnimatorAttackCycle cycle = ref session.GetComponent<AnimatorAttackCycle>();
        List<ContactClip> order = cycle.Order!;
        ContactClip attack = order[cycle.Index];
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        AnimatorRuntime runtime = document.Runtime;
        string baseClip = Base(document.Package, attack)!;

        if (cut)
        {
            runtime.Play(baseClip, 0f);
            runtime.Restart();
        }
        else if (!OnBase(runtime, baseClip))
        {
            (float fade, XafFadeCurve curve) = XafTransitions.Blend(document.Package, runtime.CurrentClip, baseClip);
            runtime.Play(baseClip, fade, curve);
        }

        if (Clip(document.Package, attack) is { } clip)
            XafStrike.Start(runtime, clip);
        AnimatorSession.SetStatus(session,
            $"ATTACKS {cycle.Index + 1}/{order.Count}: {attack.Id().ToUpperInvariant()} OVER {baseClip.ToUpperInvariant()}");
    }

    private static XafClip? Clip(XafPackage package, ContactClip attack) =>
        package.Clips.Find(clip => clip.Id == attack.Id());

    /// <summary>Clears the cycle and takes its layer off the runtime it was on; returns what it was.</summary>
    private static AnimatorAttackCycle Drop(Entity session)
    {
        ref AnimatorAttackCycle cycle = ref session.GetComponent<AnimatorAttackCycle>();
        AnimatorAttackCycle was = cycle;
        if (was.Order is { } order)
            was.Runtime?.RemoveLayer(order[was.Index].Id());
        cycle = default;
        return was;
    }

    /// <summary>Whether the runtime is playing the base, or fading into it.</summary>
    private static bool OnBase(AnimatorRuntime runtime, string? baseClip) =>
        baseClip is not null &&
        (runtime.IsCrossfading ? runtime.NextClip == baseClip : runtime.CurrentClip == baseClip);
}
