using Friflo.Engine.ECS;



namespace Xanadu.Tools.Animator;

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

    /// <summary>Draws where joints travel over the clip: the selected bones, or the extremities in preview.</summary>
    MotionPaths,
}

/// <summary>Binds a <c>Toggle</c> entity to one animator editing mode.</summary>
public struct ModeToggle(AnimatorMode mode) : IComponent
{
    public AnimatorMode Mode = mode;

    /// <summary>The value the toggle and <see cref="AnimatorModes"/> last agreed on.</summary>
    public bool Synced;
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

    /// <summary>Motion paths are drawn.</summary>
    public bool MotionPaths;

    /// <summary>Motion paths show only the second around the playhead, not the whole clip.</summary>
    public bool PathWindow;

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
        AnimatorMode.MotionPaths => MotionPaths,
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
            case AnimatorMode.MotionPaths:
                MotionPaths = value;
                break;
            default:
                Rest = value;
                break;
        }
    }
}
