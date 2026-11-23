using Xanadu.Model.Animation;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator.Intents;

// Sequences, the attack cycle and transitions between clips.

/// <summary>Plays jump, fall, land and idle into each other, as the game does.</summary>
public readonly record struct PlayGameSequence : IIntent<PlayGameSequence>;

/// <summary>Plays every clip in the file into the next.</summary>
public readonly record struct PlayAllClips : IIntent<PlayAllClips>;

/// <summary>Starts or stops cycling the attack clips, each layered over the clip the game plays under it.</summary>
public readonly record struct CycleAttacks : IIntent<CycleAttacks>;

/// <summary>Speeds the attack preview's metronome up (positive) or slows it by one step.</summary>
public readonly record struct StepMetronome(int By) : IIntent<StepMetronome>;

/// <summary>Fires an attack on fewer (positive) or more of the metronome's beats.</summary>
public readonly record struct StepAttackEvery(int By) : IIntent<StepAttackEvery>;

/// <summary>Allows or forbids the current clip fading into clip <paramref name="To"/>, and selects it.</summary>
public readonly record struct AllowTransition(string To) : IIntent<AllowTransition>;

/// <summary>Selects the hand-over from the current clip into clip <paramref name="To"/>, for the FLOW tab to edit.</summary>
public readonly record struct SelectTransition(string To) : IIntent<SelectTransition>;

/// <summary>Lengthens (positive) or shortens the selected transition's fade by one step.</summary>
public readonly record struct StepTransitionFade(int By) : IIntent<StepTransitionFade>;

/// <summary>Sets the selected transition's fade curve.</summary>
public readonly record struct SetTransitionCurve(RigFadeCurve Curve) : IIntent<SetTransitionCurve>;

/// <summary>Plays the current clip into the selected one with its fade, as the game would.</summary>
public readonly record struct PreviewTransition : IIntent<PreviewTransition>;

/// <summary>Makes the transition preview repeat until stopped.</summary>
public readonly record struct ToggleFlowLoop : IIntent<ToggleFlowLoop>;

/// <summary>Plays every preview at a quarter of its speed, to watch a fade closely.</summary>
public readonly record struct ToggleSlow : IIntent<ToggleSlow>;
