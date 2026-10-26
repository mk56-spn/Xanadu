using System.Collections.Generic;
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

/// <summary>One selected key set: every channel key of a bone or attachment at one time.</summary>
public readonly record struct AnimatorKeyRef(XafChannelTarget Target, string Id, float Time)
{
    public XafTrack Track => new(Target, Id);

    public bool Is(XafTrack track, float time) =>
        Target == track.Target && Id == track.Id &&
        System.Math.Abs(Time - time) <= XafTracks.TimeTolerance;
}

/// <summary>
/// The selected bone and attachment (indices, -1 for none) and the selected
/// timeline keys. The first key is the primary one, the anchor that the
/// playhead and drags follow; a box select can add more in
/// <see cref="Extra"/>.
/// </summary>
public struct AnimatorSelection : IComponent
{
    public int Bone;
    public int Attachment;

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
    public float KeyTime;

    /// <summary>The other selected keys beside the primary one; null when only one is selected.</summary>
    public List<AnimatorKeyRef>? Extra;

    /// <summary>Every selected key, the primary one first.</summary>
    public readonly List<AnimatorKeyRef> Keys()
    {
        var keys = new List<AnimatorKeyRef>();
        if (!_hasKey)
            return keys;

        keys.Add(new AnimatorKeyRef(KeyTarget, KeyId, KeyTime));
        if (Extra is not null)
            keys.AddRange(Extra);
        return keys;
    }

    public readonly bool IsKeySelected(XafTrack track, float time) =>
        _hasKey && Keys().Exists(key => key.Is(track, time));

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
    public bool Ik;
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

    /// <summary>Seconds into the clip under the pointer, snapped, or the time of the key clicked.</summary>
    public float Time;
}

/// <summary>A copied pose, by bone id, waiting to be pasted.</summary>
public struct AnimatorClipboard : IComponent
{
    public System.Collections.Generic.Dictionary<string, XafTransform>? Pose;
}

/// <summary>One attachment the SVG picker can add: a file and a group inside it.</summary>
public readonly record struct AnimatorSvgChoice(string Path, string Group)
{
    /// <summary>The file's name, plus the group when the file holds more than one.</summary>
    public string Label
    {
        get
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            return string.Equals(name, Group, System.StringComparison.Ordinal)
                ? name
                : $"{name} / {Group}";
        }
    }
}

/// <summary>
/// The ADD SVG picker opened from the bone menu. It lists every group of every
/// SVG the project holds and adds the chosen one to <see cref="Bone"/>.
/// </summary>
public struct AnimatorSvgMenu : IComponent
{
    public bool Open;

    /// <summary>Screen point the picker was asked to open at, before it is kept on screen.</summary>
    public Vector2 Anchor;

    /// <summary>The bone the chosen attachment is added to.</summary>
    public int Bone;

    /// <summary>Every addable group, refreshed each time the picker opens.</summary>
    public List<AnimatorSvgChoice>? Choices;
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

    /// <summary>Header slot that holds the clip dropdown button.</summary>
    public Entity ClipList;

    /// <summary>
    /// Top layer for floating UI: menus, dropdown lists and the history panel.
    /// It draws above the overlay canvases, so lists opened from the header
    /// are not hidden behind the preview.
    /// </summary>
    public Entity MenuLayer;

    /// <summary>The key menu and its two options; the menu system dims the one that does not apply.</summary>
    public Entity KeyMenu;
    public Entity KeyMenuAdd;
    public Entity KeyMenuDelete;

    /// <summary>Screen size the layout was built for, so floating menus can stay on screen.</summary>
    public Vector2 ScreenSize;

    /// <summary>Button of the clip dropdown; rebuilt with the clip list.</summary>
    public Entity ClipDropdown;
    public Entity ClipName;
    public Entity LoopButton;
    public Entity PathField;
    public Entity DirtyLabel;

    /// <summary>Floating bone context menu and the field used to rename a bone.</summary>
    public Entity BoneMenu;
    public Entity BoneNameField;

    /// <summary>The ADD SVG picker and the list its choices are built into.</summary>
    public Entity SvgMenu;
    public Entity SvgMenuList;
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
