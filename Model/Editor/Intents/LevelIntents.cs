using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Editor.Intents;

// The level, its song, transport, placement and history. Emitted on the
// editor entity; the editor's command system applies them on its update.

/// <summary>Renames the level.</summary>
public readonly record struct SetLevelName(string Name) : IIntent<SetLevelName>;

/// <summary>Sets who made the level's song.</summary>
public readonly record struct SetLevelArtist(string Artist) : IIntent<SetLevelArtist>;

/// <summary>Sets the level's difficulty label.</summary>
public readonly record struct SetLevelDifficulty(string Difficulty) : IIntent<SetLevelDifficulty>;

/// <summary>Sets the beat snap to 1/<paramref name="Division"/>; one the editor doesn't offer falls back to 1/4.</summary>
public readonly record struct SetSnap(int Division) : IIntent<SetSnap>;

/// <summary>Picks the placement tool.</summary>
public readonly record struct PickPlacement(EditorPlacementMode Mode) : IIntent<PickPlacement>;

/// <summary>Plays if stopped (from the start when at the end), stops if playing.</summary>
public readonly record struct TogglePlayback : IIntent<TogglePlayback>;

/// <summary>Stops and returns to the start.</summary>
public readonly record struct StopPlayback : IIntent<StopPlayback>;

/// <summary>Moves the playhead to <paramref name="TimeMs"/> (never before 0).</summary>
public readonly record struct Seek(long TimeMs) : IIntent<Seek>;

/// <summary>Turns the metronome click on or off.</summary>
public readonly record struct SetMetronome(bool On) : IIntent<SetMetronome>;

/// <summary>Times the level to the library song with this id.</summary>
public readonly record struct UseSong(string Id) : IIntent<UseSong>;

/// <summary>Takes in unsaved work from a crash: the level as it was, and the file it belongs to.</summary>
public readonly record struct RecoverLevel(LevelDocument Level, string Path) : IIntent<RecoverLevel>;

/// <summary>The level was just written to <paramref name="Path"/>, which is now its file.</summary>
public readonly record struct SetLevelFile(string Path) : IIntent<SetLevelFile>;

/// <summary>Steps back one edit.</summary>
public readonly record struct Undo : IIntent<Undo>;

/// <summary>Steps forward one undone edit.</summary>
public readonly record struct Redo : IIntent<Redo>;

/// <summary>Zooms the timeline in (+1) or out (-1).</summary>
public readonly record struct ZoomTimeline(int Direction) : IIntent<ZoomTimeline>;

// The selection.

/// <summary>Deletes the selected items.</summary>
public readonly record struct DeleteSelection : IIntent<DeleteSelection>;

/// <summary>Copies the selected items.</summary>
public readonly record struct CopySelection : IIntent<CopySelection>;

/// <summary>Pastes the copied items at the playhead.</summary>
public readonly record struct PasteSelection : IIntent<PasteSelection>;

/// <summary>Moves the selection one snap step earlier (-1) or later (+1).</summary>
public readonly record struct MoveSelection(int Direction) : IIntent<MoveSelection>;

/// <summary>Sets the key new notes get, and the selected notes' key.</summary>
public readonly record struct SetNoteTarget(KeybindAction Action) : IIntent<SetNoteTarget>;
