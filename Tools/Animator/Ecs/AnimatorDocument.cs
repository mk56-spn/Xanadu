using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.History;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator;

/// <summary>The package being edited together with its playback state.</summary>
public struct AnimatorDocument : IComponent
{
    public XafPackage Package;
    public AnimatorRuntime Runtime;
    public AnimatorPlayback Playback;
    public IAnimatorFiles Files;

    /// <summary>Bumped when the package is replaced or its rig structure changes.</summary>
    public int Version;

    /// <summary>Bumped when only effects' items changed, so the preview rebuilds just those.</summary>
    public int EffectsVersion;
}

/// <summary>
/// Where the document is saved and whether it has unsaved changes. Dirty means
/// the package differs from <see cref="Saved"/>, the state at the last save,
/// load or new document, so undoing back to that state is clean again.
/// </summary>
public struct AnimatorFile : IComponent
{
    public string Path;
    public XafPackage? Saved;
    public bool Dirty;

    /// <summary>Set after Load or New refused to discard unsaved changes; a repeat confirms.</summary>
    public bool DiscardArmed;

    /// <summary>Set once CLOSE EDITOR is confirmed; the tool quits on its next frame.</summary>
    public bool CloseRequested;
}

/// <summary>
/// Vertical scroll of the keyframe timeline's track rows, and the tracks whose
/// channels are expanded into their own rows.
/// </summary>
public struct AnimatorTracks : IComponent
{
    public int FirstRow;
    public HashSet<XafTrack>? Expanded;
}

/// <summary>
/// The session's plain editing state that no system queries: undo history,
/// the two clipboards and the picked clip event. One component for all of it,
/// so each new bit of state is a field here, not a component of its own.
/// </summary>
public struct AnimatorSessionState : IComponent
{
    public AnimatorHistory History;
    public AnimatorClipboard Clipboard;
    public AnimatorKeyClipboard KeyClipboard;
    public AnimatorEventSelection Event;
}

/// <summary>A copied pose, by bone id, waiting to be pasted.</summary>
public struct AnimatorClipboard
{
    public System.Collections.Generic.Dictionary<string, XafTransform>? Pose;
}

/// <summary>Latest status line message shown in the header.</summary>
public struct AnimatorStatus : IComponent
{
    public string Message;

    /// <summary>Bumped on every message, so the same message twice toasts twice.</summary>
    public int Serial;
}

/// <summary>
/// One undo state: the package, plus the pose on screen and where in which clip
/// it was, since an unkeyed pose edit lives only in the runtime.
/// </summary>
public readonly record struct AnimatorSnapshot(XafPackage Package, XafPose Pose, string Clip, float Time);

/// <summary>Document snapshots for undo/redo, on the shared <see cref="UndoHistory{T}"/>.</summary>
public struct AnimatorHistory
{
    public UndoHistory<AnimatorSnapshot> History;

    /// <summary>State at the start of a drag, recorded when the drag ends.</summary>
    public AnimatorSnapshot? Gesture;

    /// <summary>Nesting depth of <c>Edit</c> calls, so only the outermost one records.</summary>
    public int Depth;
}
