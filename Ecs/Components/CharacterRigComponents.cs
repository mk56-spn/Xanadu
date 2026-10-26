using Friflo.Engine.ECS;
using Writer.Animation;

namespace Writer.Ecs;

/// <summary>XAF package to load and draw as this player's character.</summary>
public struct CharacterRigSource(string path) : IComponent
{
    public string Path = path;
}

/// <summary>A loaded, animating character rig.</summary>
public struct CharacterRig(AnimatorRuntime runtime) : IComponent
{
    public AnimatorRuntime Runtime = runtime;
}

/// <summary>The rig source could not be loaded; the player keeps its plain box.</summary>
public struct CharacterRigMissing : ITag;

/// <summary>The rig's art is on screen, so the plain player box is not drawn.</summary>
public struct CharacterRigVisible : ITag;

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
