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
