using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.History;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator;

public enum ToolActionKind
{
    Play,
    Stop,
    Save,
    Load,
    Clip,
    RotateLeft,
    RotateRight,
    ScaleUp,
    ScaleDown,
    KeyBone,
    Undo,
    Redo,
    Interpolate,
    /// <summary>Duplicates the current clip directly; the NEW CLIP dialog can also copy it.</summary>
    DuplicateClip,
    DeleteClip,
    DurationDown,
    DurationUp,
    ToggleLoop,

    /// <summary>Opens the clip length field and step buttons in the CONFIG menu.</summary>
    OpenClipLength,

    /// <summary>Opens the NEW CLIP dialog; the argument "duplicate" starts it copying the current clip.</summary>
    OpenNewClip,

    /// <summary>Creates the clip described by the NEW CLIP dialog.</summary>
    CreateClip,

    /// <summary>Closes the NEW CLIP dialog without creating a clip.</summary>
    CloseNewClip,
    SelectBone,
    SelectAttachment,
    ToggleMovable,
    AddBone,
    DeleteBone,
    NewSkeleton,
    RenameBone,
    ReparentBone,
    KeyPose,
    CopyPose,
    PastePose,
    MirrorPose,
    AddKey,
    DeleteKey,

    /// <summary>Opens the ADD SVG picker on the selected bone; no argument.</summary>
    AddSvg,

    /// <summary>Attaches one picker choice, by its index in the open picker.</summary>
    AttachSvg,

    /// <summary>Plays if stopped, stops if playing, as the PLAY and STOP buttons do.</summary>
    TogglePlay,

    /// <summary>Flips one editing mode; the argument names the <see cref="AnimatorMode"/>.</summary>
    ToggleMode,

    /// <summary>Frames the whole rig in the preview.</summary>
    FitPreview,

    /// <summary>Puts the preview back at one unit per pixel, centred.</summary>
    ResetPreview,

    /// <summary>Clears the selected bone's or attachment's pose back to its rest transform.</summary>
    ResetTransform,

    /// <summary>Copies the selected key sets, for pasting at another time or in another clip.</summary>
    CopyKeys,

    /// <summary>Pastes the copied key sets at the playhead.</summary>
    PasteKeys,

    /// <summary>Selects every key set in the current clip.</summary>
    SelectAllKeys,

    /// <summary>Hangs a new Verlet chain off the selected bone; no argument.</summary>
    AddChain,

    /// <summary>Selects a chain, by its index in the package.</summary>
    SelectChain,

    /// <summary>Deletes the selected chain; no argument.</summary>
    DeleteChain,

    /// <summary>Opens the SVG picker to choose the art of the selected chain; no argument.</summary>
    PickChainArt,

    /// <summary>The next bone clicked becomes the one the selected chain hangs from; no argument.</summary>
    PickChainBone,

    /// <summary>The next bone clicked becomes the one the selected chain's collider follows; no argument.</summary>
    PickColliderBone,

    /// <summary>Plays jump, fall, land and idle into each other, as the game does.</summary>
    SequenceGame,

    /// <summary>Plays every clip in the file into the next.</summary>
    SequenceAll,

    /// <summary>Allows or forbids the current clip fading into the clip the argument names, and selects it.</summary>
    AllowTransition,

    /// <summary>Selects the hand-over from the current clip into the clip the argument names, for the FLOW tab to edit.</summary>
    SelectTransition,

    /// <summary>Shortens the selected transition's fade by one step.</summary>
    TransitionFadeDown,

    /// <summary>Lengthens the selected transition's fade by one step.</summary>
    TransitionFadeUp,

    /// <summary>Sets the selected transition's fade curve; the argument names the <see cref="XafFadeCurve"/>.</summary>
    TransitionCurve,

    /// <summary>Plays the current clip into the selected one with its fade, as the game would.</summary>
    PreviewTransition,

    /// <summary>Makes the transition preview repeat until stopped.</summary>
    ToggleFlowLoop,

    /// <summary>Plays every preview at a quarter of its speed, to watch a fade closely.</summary>
    ToggleSlow,

    /// <summary>Hangs a speed streak off the selected bone, shown from the playhead; no argument.</summary>
    AddEffect,

    /// <summary>Selects an effect, by its index in the package.</summary>
    SelectEffect,

    /// <summary>Deletes the selected effect and its keys; no argument.</summary>
    DeleteEffect,

    /// <summary>Keys the selected effect shown, or hidden if it is showing, at the playhead.</summary>
    ToggleEffectVisible,

    /// <summary>Keys the selected effect's opacity one step lower at the playhead.</summary>
    EffectOpacityDown,

    /// <summary>Keys the selected effect's opacity one step higher at the playhead.</summary>
    EffectOpacityUp,
}

/// <summary>Command carried by a pressable UI entity.</summary>
public struct ToolAction(ToolActionKind kind, string argument = "") : IComponent
{
    public ToolActionKind Kind = kind;
    public string Argument = argument;
}

public enum AnimatorMode
{
    AutoKey,
    Onion,
    Rest,

    /// <summary>Preview picks and drags SVG attachments instead of bones.</summary>
    Svg,

    /// <summary>Dragging a joint bends its parent and grandparent to reach the pointer.</summary>
    Ik,

    /// <summary>In the rest pose, moving or turning a bone leaves the bones below it where they are.</summary>
    Pin,

    /// <summary>Draws each bone as a filled kite; off leaves just the joints and labels.</summary>
    Kites,

    /// <summary>Draws the joint dots and bone labels; off leaves just the art and the bone shapes.</summary>
    Handles,
}

/// <summary>Binds a <c>Toggle</c> entity to one animator editing mode.</summary>
public struct ModeToggle(AnimatorMode mode) : IComponent
{
    public AnimatorMode Mode = mode;

    /// <summary>The value the toggle and <see cref="AnimatorModes"/> last agreed on.</summary>
    public bool Synced;
}

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

/// <summary>One selected key set: every channel key of a bone or attachment at one time.</summary>
public readonly record struct AnimatorKeyRef(
    XafChannelTarget Target,
    string Id,
    float Time,
    XafChannelProperty? Property = null)
{
    public XafTrack Track => new(Target, Id);

    /// <summary>Whether this ref is the key set of a target at a time.</summary>
    public bool Is(XafTrack track, float time) =>
        Property is null &&
        Target == track.Target && Id == track.Id &&
        System.Math.Abs(Time - time) <= XafTracks.TimeTolerance;

    /// <summary>Whether this ref is exactly the key on a row at a time.</summary>
    public bool Is(AnimatorTrackRow row, float time) =>
        Target == row.Target && Id == row.Id && Property == row.Property &&
        System.Math.Abs(Time - time) <= XafTracks.TimeTolerance;
}

/// <summary>
/// The selected bone and attachment (indices, -1 for none) and the selected
/// timeline keys. The first key is the primary one, the anchor that the
/// playhead and drags follow; a box select can add more in
/// <see cref="Extra"/>.
/// </summary>
public partial struct AnimatorSelection : IComponent
{
    public int Bone;
    public int Attachment;

    /// <summary>The selected Verlet chain; -1 for none.</summary>
    public int Chain;

    /// <summary>The selected effect; -1 for none.</summary>
    public int Effect;

    /// <summary>The selected bones beside the primary one; null when only one is selected.</summary>
    public List<int>? ExtraBones;

    private bool _hasKey;

    /// <summary>
    /// A key is selected. Clearing it drops <see cref="Extra"/> too, so a
    /// leftover group never outlives its primary key.
    /// </summary>
    public bool HasKey
    {
        readonly get => _hasKey;
        set
        {
            _hasKey = value;
            if (!value)
                Extra = null;
        }
    }

    public XafChannelTarget KeyTarget;
    public string KeyId;
    public XafChannelProperty? KeyProperty;
    public float KeyTime;

    /// <summary>The other selected keys beside the primary one; null when only one is selected.</summary>
    public List<AnimatorKeyRef>? Extra;

    /// <summary>Every selected key, the primary one first.</summary>
    public readonly List<AnimatorKeyRef> Keys()
    {
        var keys = new List<AnimatorKeyRef>();
        if (!_hasKey)
            return keys;

        keys.Add(new AnimatorKeyRef(KeyTarget, KeyId, KeyTime, KeyProperty));
        if (Extra is not null)
            keys.AddRange(Extra);
        return keys;
    }

    public readonly bool IsKeySelected(XafTrack track, float time) =>
        _hasKey && Keys().Exists(key => key.Is(track, time));

    public readonly bool IsKeySelected(AnimatorTrackRow row, float time) =>
        _hasKey && Keys().Exists(key => key.Is(row, time));

    public static AnimatorSelection None => new()
    {
        Bone = -1,
        Attachment = -1,
        Chain = -1,
        Effect = -1,
        HasKey = false,
        KeyId = "",
    };
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

public struct AnimatorModes : IComponent
{
    public bool Rest;
    public bool AutoKey;
    public bool Onion;
    public bool Svg;
    public bool Ik;

    /// <summary>Rest-pose edits leave the bones below the edited one where they are.</summary>
    public bool Pin;

    /// <summary>Bones are drawn as kites; on by default, since the tool opens showing them.</summary>
    public bool Kites;

    /// <summary>Joint dots and bone labels are drawn; on by default.</summary>
    public bool Handles;

    /// <summary>Whether one editing mode is on.</summary>
    public readonly bool Get(AnimatorMode mode) => mode switch
    {
        AnimatorMode.AutoKey => AutoKey,
        AnimatorMode.Onion => Onion,
        AnimatorMode.Svg => Svg,
        AnimatorMode.Ik => Ik,
        AnimatorMode.Pin => Pin,
        AnimatorMode.Kites => Kites,
        AnimatorMode.Handles => Handles,
        _ => Rest,
    };

    /// <summary>Turns one editing mode on or off, leaving the others alone.</summary>
    public void Set(AnimatorMode mode, bool value)
    {
        switch (mode)
        {
            case AnimatorMode.AutoKey:
                AutoKey = value;
                break;
            case AnimatorMode.Onion:
                Onion = value;
                break;
            case AnimatorMode.Svg:
                Svg = value;
                break;
            case AnimatorMode.Ik:
                Ik = value;
                break;
            case AnimatorMode.Pin:
                Pin = value;
                break;
            case AnimatorMode.Kites:
                Kites = value;
                break;
            case AnimatorMode.Handles:
                Handles = value;
                break;
            default:
                Rest = value;
                break;
        }
    }
}

public enum AnimatorDragKind
{
    None,
    Bone,
    Attachment,
    Key,
    Scrub,

    /// <summary>Rotates a bone's parent so the grabbed joint follows the pointer.</summary>
    Swing,

    /// <summary>Bends the grabbed joint's parent and grandparent so it reaches the pointer.</summary>
    Ik,

    /// <summary>Drags a box over the timeline to select the keys it covers.</summary>
    KeyBox,

    /// <summary>Drags the loop's start handle in the ruler.</summary>
    LoopStart,

    /// <summary>Drags the loop's end handle in the ruler.</summary>
    LoopEnd,

    /// <summary>Drags the first point of the selected chain along its bone.</summary>
    ChainRoot,

    /// <summary>Drags the collider of the selected chain along its bone.</summary>
    ChainCollider,
}

/// <summary>Active pointer drag in the preview or timeline.</summary>
public struct AnimatorDrag : IComponent
{
    public AnimatorDragKind Kind;
    public Vector2 StartPointer;
    public Vector2 StartValue;

    /// <summary>The grabbed joint while swinging its parent.</summary>
    public int Joint;

    /// <summary>World transform of the dragged value's parent, so drags follow rotated rigs.</summary>
    public Transform2D ParentWorld;

    /// <summary>The children a rest drag holds still when PIN is on.</summary>
    public Xanadu.Tools.Animator.Commands.AnimatorRestPin.Pinned? Pinned;
}

/// <summary>
/// The alignment guides a joint drag is snapped to, in skeleton space, for
/// the preview to draw. Empty when nothing is snapped.
/// </summary>
public struct AnimatorSnapGuides : IComponent
{
    public bool HasX;
    public float X;
    public bool HasY;
    public float Y;
}

/// <summary>
/// What the pointer is over in the preview this frame, so the drawing can
/// highlight it. Bone and attachment indices are -1 for none.
/// </summary>
public struct AnimatorHover : IComponent
{
    public int Bone;
    public int Attachment;

    public static AnimatorHover None => new() { Bone = -1, Attachment = -1 };
}

/// <summary>
/// The bone context menu opened by right-clicking a bone label in the preview,
/// and the rename or reparent it started. Bone indices are -1 for none.
/// </summary>
public struct AnimatorBoneMenu : IComponent
{
    public bool Open;

    /// <summary>Screen point the menu opens at.</summary>
    public Vector2 Anchor;

    /// <summary>Opened from the bone tree rather than the preview, so renames edit in the tree row.</summary>
    public bool FromTree;

    /// <summary>Bone whose name field is shown, waiting for focus or being typed.</summary>
    public int Renaming;

    /// <summary>Bone waiting for its new parent to be clicked in the preview.</summary>
    public int Reparenting;

    public static AnimatorBoneMenu Closed => new() { Renaming = -1, Reparenting = -1 };
}

/// <summary>
/// The clip length field and its six step buttons, opened from the CONFIG
/// menu. The panel floats under the header so the timeline stays visible.
/// </summary>
public struct AnimatorClipLength : IComponent
{
    public bool Open;
}

/// <summary>
/// The NEW CLIP dialog: whether it is open. Its name, length and duplicate
/// choice live in the UI fields, and creating the clip reads them.
/// </summary>
public struct AnimatorNewClipDialog : IComponent
{
    public bool Open;
}

/// <summary>
/// The key menu opened by right-clicking a track row in the timeline, and
/// where in the clip it was opened, which is what ADD KEY keys.
/// </summary>
public struct AnimatorKeyMenu : IComponent
{
    public bool Open;

    /// <summary>Screen point of the menu's top-left corner.</summary>
    public Vector2 Anchor;

    /// <summary>The bone or attachment whose row was clicked.</summary>
    public XafTrack Track;

    /// <summary>The channel row clicked, or null for the target's merged row.</summary>
    public XafChannelProperty? Property;

    /// <summary>Seconds into the clip under the pointer, snapped, or the time of the key clicked.</summary>
    public float Time;
}

/// <summary>A copied pose, by bone id, waiting to be pasted.</summary>
public struct AnimatorClipboard : IComponent
{
    public System.Collections.Generic.Dictionary<string, XafTransform>? Pose;
}

/// <summary>Latest status line message shown in the header.</summary>
public struct AnimatorStatus : IComponent
{
    public string Message;
}

/// <summary>Package snapshots for undo/redo, on the shared <see cref="UndoHistory{T}"/>.</summary>
public struct AnimatorHistory : IComponent
{
    public UndoHistory<XafPackage> History;

    /// <summary>Package state at the start of a drag, recorded when the drag ends.</summary>
    public XafPackage? Gesture;

    /// <summary>Nesting depth of <c>Edit</c> calls, so only the outermost one records.</summary>
    public int Depth;
}
