using Friflo.Engine.ECS;
using Xanadu.Model.Animation;

namespace Xanadu.Model.Ecs.Components;

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

/// <summary>The rig's art is on screen, so the plain player box is not drawn.</summary>
public struct CharacterRigVisible : ITag;

/// <summary>Which way a rigged player faces; the art is drawn facing right until a leftward dash or a hit on the left flips it.</summary>
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

    /// <summary>On the ground but still carried sideways by momentum.</summary>
    Sliding,
}

/// <summary>A rigged player's motion state and what it is derived from.</summary>
public struct CharacterMotion : IComponent
{
    /// <summary>Whether the player was rising last frame.</summary>
    public bool WasRising;

    /// <summary>The upward speed last frame, in pixels per second.</summary>
    public float LastRiseSpeed;

    public CharacterMotionState State;

    /// <summary>Seconds since <see cref="State"/> was entered.</summary>
    public float StateTime;

    /// <summary>Screen Y of the player's centre last frame; up is negative.</summary>
    public float LastY;

    /// <summary>Seconds since the last puff of dust off the feet while sliding.</summary>
    public float SinceSlideDust;

    /// <summary>
    /// How far she is into bracing for a landing she is about to make, 0 to 1:
    /// the landing clip's first pose laid over her fall (<c>CharacterMotionSystem.Brace</c>).
    /// </summary>
    public float Brace;
}

/// <summary>
/// An attack pose layered additively over a rigged player's base animation,
/// triggered by a note hit; which one plays depends on the note's height
/// relative to the player.
/// </summary>
public struct CharacterAttack : IComponent
{
    /// <summary>The attack clip currently layered on, or null when none is active.</summary>
    public ContactClip? Clip;

    /// <summary>Seconds since <see cref="Clip"/> started.</summary>
    public float StateTime;
}


/// <summary>
/// A rigged player's last bump into a block's side or underside, for the
/// flinch it shows (CharacterBumpSystem notices it, CharacterBumpPose draws it).
/// Purely visual: the physics never feels it.
/// </summary>
public struct CharacterBump : IComponent
{
    /// <summary>How long a flinch lasts, in seconds.</summary>
    public const float Seconds = 0.45f;

    /// <summary>Which way the block pushed, on screen: from the block towards the player.</summary>
    public Vector2 Normal;

    /// <summary>How hard the bump was, 0 to 1.</summary>
    public float Strength;

    /// <summary>Seconds since the bump; past <see cref="Seconds"/> it shows nothing.</summary>
    public float Age;

    /// <summary>The sides touching last frame (<c>CharacterBumpSystem</c>'s bits), so only a new touch bumps.</summary>
    public int Touching;

    /// <summary>The body's velocity last frame, in pixels per second; this frame's is already stopped by the block.</summary>
    public Vector2 LastVelocity;
}
