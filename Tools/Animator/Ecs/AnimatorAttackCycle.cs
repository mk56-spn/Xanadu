using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator;

/// <summary>
/// A preview of the contact rig's attacks in context, on a metronome: each
/// attack clip in turn fires on a beat, from its contact frame, over the clip
/// the game plays under it (idle on the ground, the fall in the air), as a
/// note hit does. Idle (no order) means nothing is cycling.
/// </summary>
public struct AnimatorAttackCycle : IComponent
{
    /// <summary>The attacks the rig has, in the order they play.</summary>
    public List<ContactClip>? Order;

    /// <summary>The attack fired last.</summary>
    public int Index;

    /// <summary>Beats counted since the cycle started; the first is beat 0.</summary>
    public int Beat;

    /// <summary>Seconds into the current beat, not counting time paused.</summary>
    public float BeatClock;

    /// <summary>The runtime the layer was put on; a rebuilt runtime ends the cycle.</summary>
    public AnimatorRuntime? Runtime;

    /// <summary>The clip and playhead time being edited when the cycle started, put back when it is turned off.</summary>
    public string OriginClip;
    public float OriginTime;

    public readonly bool Active => Order is not null;
}

/// <summary>The attack preview's tempo, kept between runs.</summary>
public struct AnimatorMetronome : IComponent
{
    public const int MinBpm = 40;
    public const int MaxBpm = 240;
    public const int BpmStep = 5;
    public const int MaxEvery = 4;

    public int Bpm;

    /// <summary>An attack fires on every this many beats; the click sounds on all of them.</summary>
    public int Every;

    public static AnimatorMetronome Default => new() { Bpm = 120, Every = 2 };

    public readonly float BeatSeconds => 60f / System.Math.Clamp(Bpm, MinBpm, MaxBpm);
}

/// <summary>What a tick of the attack preview's metronome did.</summary>
public enum AnimatorBeat
{
    None,

    /// <summary>A beat with no attack on it.</summary>
    Beat,

    /// <summary>A beat an attack fired on.</summary>
    Hit,
}

/// <summary>Sounds the attack preview's metronome.</summary>
public interface IAnimatorClick
{
    void Click(bool accent);
}
