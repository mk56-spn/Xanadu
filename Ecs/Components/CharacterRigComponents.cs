using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Ecs;

/// <summary>XAF package to load and draw as this player's character.</summary>
public struct CharacterRigSource(string path) : IComponent
{
    public string Path = path;
}

/// <summary>A loaded, animating character rig.</summary>
public struct CharacterRig(AnimatorRuntime runtime) : IComponent
{
    public readonly AnimatorRuntime Runtime = runtime;
}

/// <summary>The rig source could not be loaded; the player keeps its plain box.</summary>
public struct CharacterRigMissing : ITag;

/// <summary>The rig's art is on screen, so the plain player box is not drawn.</summary>
public struct CharacterRigVisible : ITag;

/// <summary>Which way a rigged player faces; the art is drawn facing right until a leftward dash flips it.</summary>
public struct CharacterFacing : IComponent
{
    public bool Left;
}

/// <summary>What a rigged player is doing, which picks the clip it plays.</summary>
public enum CharacterMotionState
{
    Grounded,
    Jumping,
    Falling,

    /// <summary>Just touched down; plays the landing clip once, then goes back to grounded.</summary>
    Landing,
}

/// <summary>A rigged player's motion state and what it is derived from.</summary>
public struct CharacterMotion : IComponent
{
    public CharacterMotionState State;

    /// <summary>Seconds since <see cref="State"/> was entered.</summary>
    public float StateTime;

    /// <summary>Screen Y of the player's centre last frame; up is negative.</summary>
    public float LastY;
}

/// <summary>
/// An attack pose layered additively over a rigged player's base animation,
/// triggered by a note hit; which one plays depends on the note's height
/// relative to the player.
/// </summary>
public struct CharacterAttack : IComponent
{
    /// <summary>The attack clip currently layered on, or "" when none is active.</summary>
    public string ClipId;

    /// <summary>Seconds since <see cref="ClipId"/> started.</summary>
    public float StateTime;
}
