namespace Xanadu.Model.Animator;

/// <summary>What a limb target button does; each verb is sent as its own intent (<c>AnimatorLayout.TargetIntent</c>).</summary>
public enum AnimatorTargetVerb
{
    /// <summary>Pins the selected foot or hand to its spot from the playhead on, or ends the pin there.</summary>
    Pin,

    /// <summary>Sends the selected hand, foot or root round a new ellipse.</summary>
    Orbit,

    /// <summary>Steps the bone the target travels with.</summary>
    Anchor,

    /// <summary>Stops the target driving its limb; its baked keys stay.</summary>
    Detach,

    /// <summary>Starts the target's span at the playhead.</summary>
    From,

    /// <summary>Ends the target's span at the playhead.</summary>
    To,

    /// <summary>Steps how time maps to the way round.</summary>
    Ease,

    /// <summary>Goes round the other way.</summary>
    Flip,
}
