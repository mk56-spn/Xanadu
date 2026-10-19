using Friflo.Engine.ECS;
using Godot;

namespace Writer;

/// <summary>Screen-space rectangle for an entity.</summary>
public struct Rectangle(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

/// <summary>Marks an entity as renderable and defines its draw order.</summary>
public struct Drawable(int zIndex) : IComponent
{
    public int ZIndex = zIndex;
}
