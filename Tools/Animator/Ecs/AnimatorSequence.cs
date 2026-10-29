using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator;

/// <summary>One stop in a clip sequence: play a clip for this long, then fade into the next.</summary>
public readonly record struct AnimatorSequenceStep(string Clip, float Seconds);

/// <summary>
/// A preview of clips playing into each other, as the game does when the
/// player jumps, falls and lands. Idle (no steps) means nothing is chained.
/// </summary>
public struct AnimatorSequence : IComponent
{
    public List<AnimatorSequenceStep>? Steps;

    /// <summary>The step being played.</summary>
    public int Index;

    /// <summary>Seconds spent on the current step, not counting time paused.</summary>
    public float Elapsed;

    /// <summary>The clip and playhead time being edited when the chain started, put back when it ends.</summary>
    public string OriginClip;
    public float OriginTime;

    /// <summary>What the status line calls the chain.</summary>
    public string Name;

    /// <summary>Starts over from the first step at the end, until stopped.</summary>
    public bool Loop;

    /// <summary>
    /// Plays as the game does: a hand-over the transition table does not allow
    /// is refused, and the clip before it keeps playing.
    /// </summary>
    public bool Strict;

    public readonly bool Active => Steps is not null;
}
