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

/// <summary>Layer entity component. ZIndex determines draw order between layers.</summary>
public struct Layer(int zIndex) : IComponent
{
    public int ZIndex = zIndex;
}

/// <summary>RenderingServer canvas item owned by an entity.</summary>
public struct VisualRid() : IComponent
{
    public Rid Value = RenderingServer.CanvasItemCreate();
}
