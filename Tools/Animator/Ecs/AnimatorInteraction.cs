using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

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

    /// <summary>Turns a childless bone about its own joint by the handle on its stem.</summary>
    Rotate,

    /// <summary>Paints the skin's weights under the brush; Ctrl takes weight away.</summary>
    PaintWeights,

    /// <summary>Moves or resizes an override's bar on the timeline.</summary>
    OverrideSpan,

    /// <summary>Moves the selected limb target, or an ellipse's centre.</summary>
    TargetCenter,

    /// <summary>Sizes and tilts the selected ellipse by the handle on its long side.</summary>
    TargetRadiusX,

    /// <summary>Sizes the selected ellipse across.</summary>
    TargetRadiusY,

    /// <summary>Drags the scrollbar beside the track rows.</summary>
    RowScroll,

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

    /// <summary>Retiming a clip event by its flag on the ruler.</summary>
    Event,
}

/// <summary>Active pointer drag in the preview or timeline.</summary>
public struct AnimatorDrag : IComponent
{
    public AnimatorDragKind Kind;
    public Vector2 StartPointer;
    public Vector2 StartValue;

    /// <summary>The grabbed joint while swinging its parent, or the bone a rotate drag turns.</summary>
    public int Joint;

    /// <summary>Where along the row scrollbar's thumb the pointer took hold.</summary>
    public float ScrollGrab;

    /// <summary>The bone's local rotation when a rotate drag began.</summary>
    public float StartRotation;

    /// <summary>The pointer's angle around the joint last frame during a rotate drag.</summary>
    public float PointerAngle;

    /// <summary>How far the pointer has gone around the joint since a rotate drag began.</summary>
    public float Turned;

    /// <summary>World transform of the dragged value's parent, so drags follow rotated rigs.</summary>
    public Transform2D ParentWorld;

    /// <summary>The children a rest drag holds still when PIN is on.</summary>
    public Xanadu.Tools.Animator.Commands.AnimatorRestPin.Pinned? Pinned;

    /// <summary>
    /// A plain joint grab (a move, swing or IK drag) that switches with the
    /// modes while held: turning IK, REST or PIN on or off takes effect at once.
    /// </summary>
    public bool FollowsModes;

    /// <summary>The joint the press took, for a drag that follows the modes.</summary>
    public int Grabbed;

    /// <summary>Whether the drag was in the rest pose when it last (re)started.</summary>
    public bool Rest;

    /// <summary>A rotate drag turned a pin's facing rather than the pose, so there is nothing to key.</summary>
    public bool Repinned;

    /// <summary>Some part of the gesture was an IK drag, so the chain it bent is keyed at the end.</summary>
    public bool UsedIk;
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
