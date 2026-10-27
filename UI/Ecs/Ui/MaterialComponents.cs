using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>A material, such as a shader, applied to a drawable's canvas item.</summary>
public struct UiMaterial(Material material) : IComponent
{
    public Material Material = material;
}
