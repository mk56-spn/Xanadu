#nullable enable
using Friflo.Engine.ECS;
using Godot;

namespace Writer;

/// <summary>Screen-space rectangle for an entity.</summary>
public struct Rectangle : IComponent
{
    public Rect2 Bounds;

    public Rectangle(Rect2 bounds)
    {
        Bounds = bounds;
    }
}

/// <summary>Marks an entity as renderable and defines its draw order.</summary>
public struct Drawable : IComponent
{
    public int ZIndex;

    public Drawable(int zIndex)
    {
        ZIndex = zIndex;
    }
}
