using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Rotates a drawable, in radians, about its <see cref="Core.Position"/>. Hit
/// testing still uses the unrotated bounds, so keep rotated elements
/// non-interactive.
/// </summary>
public struct UiRotation(float radians) : IComponent
{
    public float Radians = radians;
}
