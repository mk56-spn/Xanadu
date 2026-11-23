using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator.Ecs;

/// <summary>Clicking bones in the preview toggles which of them bend <see cref="Attachment"/>'s mesh.</summary>
public struct AnimatorMeshPick : IComponent
{
    public bool Active;
    public int Attachment;
}

/// <summary>
/// Painting a skin's weights in the preview: <see cref="Attachment"/>'s skin,
/// for <see cref="Bone"/>, with a round brush <see cref="Radius"/> screen
/// pixels across that adds <see cref="Strength"/> a frame at its middle.
/// </summary>
public struct AnimatorWeightPaint : IComponent
{
    public const float MinRadius = 4f;
    public const float MaxRadius = 120f;
    public const float RadiusStep = 4f;
    public const float MinStrength = 0.01f;
    public const float MaxStrength = 0.5f;
    public const float StrengthStep = 0.02f;

    public bool Active;
    public int Attachment;
    public int Bone;
    public float Radius;
    public float Strength;

    /// <summary>Where the pointer last was over the preview, for drawing the brush.</summary>
    public Vector2 Pointer;

    public static AnimatorWeightPaint Default => new() { Attachment = -1, Bone = -1, Radius = 18f, Strength = 0.08f };
}
