using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator.Intents;

// Playback, files, history, workspaces, panels and editing modes.

/// <summary>Plays from the playhead.</summary>
public readonly record struct StartPlaying : IIntent<StartPlaying>;

/// <summary>Stops playback; when already stopped, returns to the start.</summary>
public readonly record struct StopPlaying : IIntent<StopPlaying>;

/// <summary>Plays if stopped, stops if playing, as the PLAY and STOP buttons do.</summary>
public readonly record struct TogglePlay : IIntent<TogglePlay>;

/// <summary>Plays the clip <paramref name="Clip"/> names, by its id.</summary>
public readonly record struct PlayClip(string Clip) : IIntent<PlayClip>;

/// <summary>Saves the rig.</summary>
public readonly record struct Save : IIntent<Save>;

/// <summary>Loads a rig; with unsaved changes the first ask only warns.</summary>
public readonly record struct Load : IIntent<Load>;

/// <summary>Starts an empty skeleton; with unsaved changes the first ask only warns.</summary>
public readonly record struct NewSkeleton : IIntent<NewSkeleton>;

/// <summary>Quits the animator; with unsaved changes the first ask only warns.</summary>
public readonly record struct CloseEditor : IIntent<CloseEditor>;

/// <summary>Undoes the last edit.</summary>
public readonly record struct Undo : IIntent<Undo>;

/// <summary>Redoes the last undone edit.</summary>
public readonly record struct Redo : IIntent<Redo>;

/// <summary>Switches workspace.</summary>
public readonly record struct SetWorkspace(AnimatorWorkspace Workspace) : IIntent<SetWorkspace>;

/// <summary>Enters preview mode, or leaves it: the editing panels give way to the viewport.</summary>
public readonly record struct TogglePreviewMode : IIntent<TogglePreviewMode>;

/// <summary>Closes the XIF editor if it is open, else leaves preview mode; does nothing while editing.</summary>
public readonly record struct ExitPreviewMode : IIntent<ExitPreviewMode>;

/// <summary>Opens the file info popup: the file's path and what it holds.</summary>
public readonly record struct OpenInfo : IIntent<OpenInfo>;

/// <summary>Closes the file info popup.</summary>
public readonly record struct CloseInfo : IIntent<CloseInfo>;

/// <summary>Opens or closes the SHORTCUTS sheet listing every key.</summary>
public readonly record struct ToggleShortcuts : IIntent<ToggleShortcuts>;

/// <summary>Opens or closes the EASING window, where every curve can be picked.</summary>
public readonly record struct ToggleEasingWindow : IIntent<ToggleEasingWindow>;

/// <summary>Flips one editing mode.</summary>
public readonly record struct ToggleMode(AnimatorMode Mode) : IIntent<ToggleMode>;

/// <summary>Shows motion paths for the whole clip, or only around the playhead.</summary>
public readonly record struct TogglePathWindow : IIntent<TogglePathWindow>;

/// <summary>Frames the whole rig in the preview.</summary>
public readonly record struct FitPreview : IIntent<FitPreview>;

/// <summary>Puts the preview back at one unit per pixel, centred.</summary>
public readonly record struct ResetPreview : IIntent<ResetPreview>;
