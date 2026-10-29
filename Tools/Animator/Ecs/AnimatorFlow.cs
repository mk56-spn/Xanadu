using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The FLOW tab's state: the transition it edits and how its preview plays.
/// The transition runs from the clip being edited into <see cref="Target"/>.
/// </summary>
public struct AnimatorFlow : IComponent
{
    /// <summary>The clip the selected transition fades into; empty for none.</summary>
    public string Target;

    /// <summary>The transition preview repeats until stopped.</summary>
    public bool Loop;

    /// <summary>Every preview plays at <see cref="SlowSpeed"/>.</summary>
    public bool Slow;

    public const float SlowSpeed = 0.25f;

    public readonly float Speed => Slow ? SlowSpeed : 1f;
}
