using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Canvas/entity position in screen space.</summary>
public struct Position(Vector2 value) : IComponent
{
    public Vector2 Value = value;
}
