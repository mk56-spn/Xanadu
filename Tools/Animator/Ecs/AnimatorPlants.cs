using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Tools.Animator;

/// <summary>A bone planted at the playhead: a spot target as the preview marks it.</summary>
public sealed class AnimatorPlant
{
    public int Bone;

    /// <summary>Where it is held, in rig space.</summary>
    public Vector2 At;

    /// <summary>The limb cannot reach the spot, so the foot has come away.</summary>
    public bool OutOfReach;
}

/// <summary>The plants at the playhead, listed each frame from the clip's targets, which are what the file keeps.</summary>
public struct AnimatorPlants : IComponent
{
    public List<AnimatorPlant> Held;
}
