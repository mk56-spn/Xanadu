using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator.Intents;

// Posing and keying: keys, poses, overrides, events, the key tryout and limb targets.

/// <summary>Keys the selected bones at the playhead.</summary>
public readonly record struct KeyBone : IIntent<KeyBone>;

/// <summary>Turns the selected bones by <paramref name="Steps"/> rotation steps (negative is left).</summary>
public readonly record struct TurnSelected(int Steps) : IIntent<TurnSelected>;

/// <summary>Scales the selected bones by <paramref name="Steps"/> scale steps.</summary>
public readonly record struct ScaleSelected(int Steps) : IIntent<ScaleSelected>;

/// <summary>Sets the selected keys' ease.</summary>
public readonly record struct SetEase(EasePreset Ease) : IIntent<SetEase>;

/// <summary>Adds a key from the key menu.</summary>
public readonly record struct AddKey : IIntent<AddKey>;

/// <summary>Deletes the selected keys.</summary>
public readonly record struct DeleteKey : IIntent<DeleteKey>;

/// <summary>Keys every bone's pose.</summary>
public readonly record struct KeyPose : IIntent<KeyPose>;

/// <summary>Copies the whole pose.</summary>
public readonly record struct CopyPose : IIntent<CopyPose>;

/// <summary>Pastes the copied pose.</summary>
public readonly record struct PastePose : IIntent<PastePose>;

/// <summary>Mirrors the pose left to right.</summary>
public readonly record struct MirrorPose : IIntent<MirrorPose>;

/// <summary>Clears the selected bone's or part's pose back to its rest transform.</summary>
public readonly record struct ResetTransform : IIntent<ResetTransform>;

/// <summary>Copies the selected key sets, for pasting at another time or in another clip.</summary>
public readonly record struct CopyKeys : IIntent<CopyKeys>;

/// <summary>Pastes the copied key sets at the playhead.</summary>
public readonly record struct PasteKeys : IIntent<PasteKeys>;

/// <summary>Selects every key set in the current clip.</summary>
public readonly record struct SelectAllKeys : IIntent<SelectAllKeys>;

/// <summary>Removes the right-clicked override; <paramref name="KeepKeys"/> keeps what it baked as ordinary keys.</summary>
public readonly record struct RemoveOverride(bool KeepKeys) : IIntent<RemoveOverride>;

/// <summary>Marks the playhead's moment in this clip with a named event.</summary>
public readonly record struct AddEvent : IIntent<AddEvent>;

/// <summary>Deletes the selected event.</summary>
public readonly record struct DeleteEvent : IIntent<DeleteEvent>;

/// <summary>Eases the move into the key just set.</summary>
public readonly record struct TryoutEase(EasePreset Ease) : IIntent<TryoutEase>;

/// <summary>Plays the move into the key just set again.</summary>
public readonly record struct TryoutReplay : IIntent<TryoutReplay>;

/// <summary>Ends the quick test after keying.</summary>
public readonly record struct TryoutClose : IIntent<TryoutClose>;

/// <summary>Holds the selected foot or hand at its spot on the rig, or releases and keys it.</summary>
public readonly record struct PlantBone : IIntent<PlantBone>;

/// <summary>Sends the selected hand, foot or root round a new ellipse.</summary>
public readonly record struct AddOrbit : IIntent<AddOrbit>;

/// <summary>Steps the bone the selected limb target travels with.</summary>
public readonly record struct StepTargetAnchor : IIntent<StepTargetAnchor>;

/// <summary>Stops the selected limb target driving its limb; its baked keys stay.</summary>
public readonly record struct DetachTarget : IIntent<DetachTarget>;

/// <summary>Starts the selected limb target's span at the playhead.</summary>
public readonly record struct SetTargetFrom : IIntent<SetTargetFrom>;

/// <summary>Ends the selected limb target's span at the playhead.</summary>
public readonly record struct SetTargetTo : IIntent<SetTargetTo>;

/// <summary>Steps how time maps to the way round the selected limb target.</summary>
public readonly record struct StepTargetEase : IIntent<StepTargetEase>;

/// <summary>Sends the selected limb target round the other way.</summary>
public readonly record struct FlipTarget : IIntent<FlipTarget>;
