using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;

namespace Xanadu.Tools.Animator.Ecs;

/// <summary>
/// The quick test after a key is set: the move from the previous key into the
/// new one loops, and an ease bar under the preview re-eases that move
/// and plays it again. Anything else that moves the playhead, plays, or
/// changes the selection ends it.
/// </summary>
public struct AnimatorKeyTryout : IComponent
{
    public bool Active;

    /// <summary>The move is looping; otherwise it holds on the new key.</summary>
    public bool Playing;

    /// <summary>The previous key's time and the new key's time, in seconds.</summary>
    public float From;
    public float To;

    /// <summary>The clip it was keyed in.</summary>
    public string Clip;

    /// <summary>The time the tryout last put the playhead at, to tell its own seeks from anyone else's.</summary>
    public float Shown;

    /// <summary>Seconds into the current loop (hold on the previous key, move, hold on the new key).</summary>
    public float Elapsed;

    /// <summary>The bones and parts that were keyed.</summary>
    public List<(RigChannelTarget Target, string Id)> Targets;

    /// <summary>The ease last picked from the bar, lit there.</summary>
    public EasePreset? Picked;
}
