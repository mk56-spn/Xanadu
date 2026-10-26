using Friflo.Engine.ECS;
using Godot;
using Writer.Animation;
using Writer.History;
using Writer.Tools.Animator.Files;

namespace Writer.Tools.Animator;

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
    NewClip,
    DuplicateClip,
    DeleteClip,
    DurationDown,
    DurationUp,
    ToggleLoop,
    SelectBone,
    SelectAttachment,
    ToggleMovable,
    AddBone,
    DeleteBone,
    NewSkeleton,
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
}

/// <summary>
/// The selected bone and attachment (indices, -1 for none) and the selected
/// timeline key: every channel key of one target at one time.
/// </summary>
public struct AnimatorSelection : IComponent
{
    public int Bone;
    public int Attachment;
    public bool HasKey;
    public XafChannelTarget KeyTarget;
    public string KeyId;
    public float KeyTime;

    public static AnimatorSelection None => new()
    {
        Bone = -1,
        Attachment = -1,
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

/// <summary>Vertical scroll of the keyframe timeline's track rows.</summary>
public struct AnimatorTracks : IComponent
{
    public int FirstRow;
}

public struct AnimatorModes : IComponent
{
    public bool Rest;
    public bool AutoKey;
    public bool Onion;
    public bool Svg;
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
}

/// <summary>Latest status line message shown in the header.</summary>
public struct AnimatorStatus : IComponent
{
    public string Message;
}

/// <summary>UI entities the animator systems read or write.</summary>
public struct AnimatorUi : IComponent
{
    public Entity Status;
    public Entity Preview;
    public Entity Timeline;
    public Entity BoneList;
    public Entity AttachmentList;
    public Entity ClipList;

    /// <summary>Button of the clip dropdown; rebuilt with the clip list.</summary>
    public Entity ClipDropdown;
    public Entity ClipName;
    public Entity LoopButton;
    public Entity PathField;
    public Entity DirtyLabel;
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
