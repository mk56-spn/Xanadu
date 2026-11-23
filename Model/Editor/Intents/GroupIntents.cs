using Xanadu.Core.Timeline;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Editor.Intents;

// The level's groups. Every change but picking, toggles and live drags is one undo step.

/// <summary>A new group holding the selected decos, picked straight away.</summary>
public readonly record struct NewGroup : IIntent<NewGroup>;

/// <summary>Picks the group at <paramref name="Index"/> to edit and selects its members.</summary>
public readonly record struct PickGroup(int Index) : IIntent<PickGroup>;

/// <summary>Puts the selected decos in the picked group.</summary>
public readonly record struct AddToGroup : IIntent<AddToGroup>;

/// <summary>Takes the selected decos out of the picked group, with their keys.</summary>
public readonly record struct RemoveFromGroup : IIntent<RemoveFromGroup>;

/// <summary>Deletes the picked group.</summary>
public readonly record struct DeleteGroup : IIntent<DeleteGroup>;

/// <summary>Renames the picked group; a blank name is ignored.</summary>
public readonly record struct RenameGroup(string Name) : IIntent<RenameGroup>;

/// <summary>
/// Keys a field of the picked group (its index in <c>LevelItemFields.All</c>)
/// at the playhead. While <paramref name="Live"/> a drag is still going; the
/// drag is one undo step, recorded when it ends.
/// </summary>
public readonly record struct KeyGroup(int Field, float Value, bool Live) : IIntent<KeyGroup>;

/// <summary>Takes out the picked group's keys at the playhead.</summary>
public readonly record struct DeleteGroupKeys : IIntent<DeleteGroupKeys>;

/// <summary>Steps the picked group's loop through its lengths (off, then 1 to 32 beats); <paramref name="Direction"/> is -1 or +1.</summary>
public readonly record struct StepGroupLoop(int Direction) : IIntent<StepGroupLoop>;

/// <summary>Turns the one-member escape hatch on or off: keys go to the one selected member instead of the whole group.</summary>
public readonly record struct ToggleOneMember : IIntent<ToggleOneMember>;

/// <summary>Opens or closes the ease picker for the picked group's keys at the playhead.</summary>
public readonly record struct ShowGroupEase(bool Open) : IIntent<ShowGroupEase>;

/// <summary>Sets an ease on the keys at the playhead that the group keys go to.</summary>
public readonly record struct EaseGroupKeys(EasePreset Preset) : IIntent<EaseGroupKeys>;
