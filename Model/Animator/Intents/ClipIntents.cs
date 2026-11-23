using Xanadu.Core.Timeline;
using Xanadu.Model.Animation;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator.Intents;

// Clips: creating, deleting, length and loop.

/// <summary>Opens the NEW CLIP dialog; <paramref name="Copy"/> starts it copying the current clip.</summary>
public readonly record struct OpenNewClip(bool Copy) : IIntent<OpenNewClip>;

/// <summary>Creates the clip described by the NEW CLIP dialog.</summary>
public readonly record struct CreateClip : IIntent<CreateClip>;

/// <summary>Closes the NEW CLIP dialog without creating a clip.</summary>
public readonly record struct CloseNewClip : IIntent<CloseNewClip>;

/// <summary>Duplicates the current clip directly.</summary>
public readonly record struct DuplicateClip : IIntent<DuplicateClip>;

/// <summary>Deletes the current clip.</summary>
public readonly record struct DeleteClip : IIntent<DeleteClip>;

/// <summary>Removes every key of the current clip, so it plays the rest pose again; length, loop and events stay.</summary>
public readonly record struct ClearClipKeys : IIntent<ClearClipKeys>;

/// <summary>Lengthens (<paramref name="Direction"/> 1) or shortens (-1) the clip by step <paramref name="Size"/> (1 to 3; 0 is the default step).</summary>
public readonly record struct StepClipLength(int Direction, int Size = 0) : IIntent<StepClipLength>;

/// <summary>Makes the current clip loop, or stop looping.</summary>
public readonly record struct ToggleLoop : IIntent<ToggleLoop>;

/// <summary>Opens the clip length field and step buttons in the CONFIG menu.</summary>
public readonly record struct OpenClipLength : IIntent<OpenClipLength>;
