using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Local rectangle centered on the entity canvas item origin.</summary>
public struct Rectangle(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

/// <summary>Marks an entity as renderable and defines its draw order.</summary>
public struct Drawable(int zIndex) : IComponent
{
    public int ZIndex = zIndex;
}

/// <summary>Fill color for a drawable rectangle.</summary>
public struct FillColor(Color value) : IComponent
{
    public Color Value = value;
}

/// <summary>Canvas item position in screen space.</summary>
public struct Position(Vector2 value) : IComponent
{
    public Vector2 Value = value;
}

/// <summary>Text content for a drawable entity.</summary>
public struct Text(string value) : IComponent
{
    public string Value = value;
}

public enum StackDirection
{
    Horizontal,
    Vertical,
}

/// <summary>Container entity that stacks its child entities.</summary>
public struct UiContainer(
    StackDirection direction,
    float spacing = 0,
    bool clip = false) : IComponent
{
    public StackDirection Direction = direction;
    public float Spacing = spacing;
    public bool Clip = clip;
}

/// <summary>Layer entity component. ZIndex determines draw order between layers.</summary>
public struct Layer(int zIndex) : IIndexedComponent<int>
{
    public int ZIndex = zIndex;

    public int GetIndexedValue() => ZIndex;
}

/// <summary>RenderingServer canvas item owned by an entity.</summary>
public struct VisualRid() : IComponent
{
    public Rid Value = RenderingServer.CanvasItemCreate();
}
