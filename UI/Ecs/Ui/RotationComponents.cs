using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Rotates a drawable, in radians, about its <see cref="Position"/>. Hit
/// testing still uses the unrotated bounds, so keep rotated elements
/// non-interactive.
/// </summary>
public struct UiRotation(float radians) : IComponent
{
    public float Radians = radians;
}
