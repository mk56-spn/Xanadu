using Friflo.Engine.ECS;
using Xanadu.Core;

namespace Xanadu.Model.Animator;

public enum AnimatorMode
{
    AutoKey,
    Onion,
    Rest,

    /// <summary>Preview picks and drags parts instead of bones.</summary>
    Parts,

    /// <summary>Dragging a joint bends its parent and grandparent to reach the pointer.</summary>
    Ik,

    /// <summary>In the rest pose, moving or turning a bone leaves the bones below it where they are.</summary>
    Pin,

    /// <summary>Draws each bone as a filled kite; off leaves just the joints and labels.</summary>
    Kites,

    /// <summary>Draws the joint dots and bone labels; off leaves just the art and the bone shapes.</summary>
    Handles,

    /// <summary>Draws where joints travel over the clip: the selected bones, or the extremities in preview.</summary>
    MotionPaths,

    /// <summary>A dragged joint lines up with the other joints, with guide lines; Shift drags freely.</summary>
    Align,

    /// <summary>The keyframe sheet shows everything the selection can key, not just what it has keyed.</summary>
    FullKeys,
}

// The session's editing modes are tags on the session entity: each one on
// while its tag is there. AnimatorMode names them for commands and tooltips
// until those become signals (Docs/data-centric-ecs.md, phase 2).

/// <summary>Editing the rig itself: the rest pose every clip starts from.</summary>
public struct RestPose : ITag;

/// <summary>Every pose edit is keyed at the playhead.</summary>
public struct AutoKey : ITag;

/// <summary>Poses just before and after the playhead are drawn faintly.</summary>
public struct OnionSkin : ITag;

/// <summary>Preview picks and drags parts instead of bones.</summary>
public struct PlaceParts : ITag;

/// <summary>Dragging a joint bends its parent and grandparent to reach the pointer.</summary>
public struct IkDrag : ITag;

/// <summary>Rest-pose edits leave the bones below the edited one where they are.</summary>
public struct PinChildren : ITag;

/// <summary>Bones are drawn as filled kites.</summary>
public struct ShowKites : ITag;

/// <summary>Joint dots and bone labels are drawn.</summary>
public struct ShowHandles : ITag;

/// <summary>Dragged joints line up with the other joints.</summary>
public struct AlignJoints : ITag;

/// <summary>Motion paths are drawn.</summary>
public struct ShowMotionPaths : ITag;

/// <summary>Motion paths show only the second around the playhead, not the whole clip.</summary>
public struct PathWindow : ITag;

/// <summary>The keyframe sheet shows everything the selection can key.</summary>
public struct FullKeys : ITag;

/// <summary>The tag each <see cref="AnimatorMode"/> is, and setting modes on a session.</summary>
public static class AnimatorModeTags
{
    public static Tags Of(AnimatorMode mode) => mode switch
    {
        AnimatorMode.AutoKey => Tags.Get<AutoKey>(),
        AnimatorMode.Onion => Tags.Get<OnionSkin>(),
        AnimatorMode.Parts => Tags.Get<PlaceParts>(),
        AnimatorMode.Ik => Tags.Get<IkDrag>(),
        AnimatorMode.Pin => Tags.Get<PinChildren>(),
        AnimatorMode.Kites => Tags.Get<ShowKites>(),
        AnimatorMode.Handles => Tags.Get<ShowHandles>(),
        AnimatorMode.MotionPaths => Tags.Get<ShowMotionPaths>(),
        AnimatorMode.Align => Tags.Get<AlignJoints>(),
        AnimatorMode.FullKeys => Tags.Get<FullKeys>(),
        _ => Tags.Get<RestPose>(),
    };

    /// <summary>The mode a switch's tags are, for the announcements that still name modes.</summary>
    public static AnimatorMode? ModeOf(in Tags tags)
    {
        foreach (AnimatorMode mode in Enum.GetValues<AnimatorMode>())
        {
            if (Of(mode).HasAll(tags) && tags.HasAll(Of(mode)))
                return mode;
        }
        return null;
    }

    /// <summary>The modes a new session opens with.</summary>
    public static Tags Defaults => Tags.Get<ShowKites, ShowHandles, AlignJoints>();

    public static void Set(Entity session, AnimatorMode mode, bool on) => session.SetTags(Of(mode), on);
}

/// <summary>
/// The session's modes read from its tags at one moment, for code that checks
/// several. It is a copy, not a component: set modes with
/// <see cref="AnimatorModeTags.Set"/> or the tags themselves.
/// </summary>
public readonly struct AnimatorModes
{
    public readonly bool Rest;
    public readonly bool AutoKey;
    public readonly bool Onion;
    public readonly bool Parts;
    public readonly bool Ik;
    public readonly bool Pin;
    public readonly bool Kites;
    public readonly bool Handles;
    public readonly bool Align;
    public readonly bool MotionPaths;
    public readonly bool PathWindow;
    public readonly bool FullKeys;

    private AnimatorModes(in Tags tags)
    {
        Rest = tags.Has<RestPose>();
        AutoKey = tags.Has<Xanadu.Model.Animator.AutoKey>();
        Onion = tags.Has<OnionSkin>();
        Parts = tags.Has<PlaceParts>();
        Ik = tags.Has<IkDrag>();
        Pin = tags.Has<PinChildren>();
        Kites = tags.Has<ShowKites>();
        Handles = tags.Has<ShowHandles>();
        Align = tags.Has<AlignJoints>();
        MotionPaths = tags.Has<ShowMotionPaths>();
        PathWindow = tags.Has<Xanadu.Model.Animator.PathWindow>();
        FullKeys = tags.Has<Xanadu.Model.Animator.FullKeys>();
    }

    public static AnimatorModes Of(Entity session) => new(session.Tags);

    /// <summary>
    /// Whether a mode does anything in the workspace the animator is in: pin
    /// children only in the rest pose; onion skins and motion paths, which are
    /// about time, only while animating. In <paramref name="preview"/> only those
    /// two are left: nothing is dragged or keyed, and kites and handles are off.
    /// A toggle that does not apply is hidden.
    /// </summary>
    public bool Applies(AnimatorMode mode, bool preview) => preview
        ? mode is AnimatorMode.Onion or AnimatorMode.MotionPaths
        : mode switch
        {
            AnimatorMode.Pin => Rest,
            AnimatorMode.Onion or AnimatorMode.MotionPaths => !Rest,
            _ => true,
        };
}
