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

/// <summary>Text content for a drawable entity.</summary>
public struct Text(string value) : IComponent
{
    public string Value = value;
}

/// <summary>Optional font size and color for an ECS text drawable.</summary>
public struct TextStyle(int fontSize, Color color) : IComponent
{
    public int FontSize = fontSize;
    public Color Color = color;
}

public enum TextAlign
{
    Center,
    Left,
    Right,
}

/// <summary>Horizontal alignment for an ECS text drawable.</summary>
public struct TextAlignment(TextAlign align) : IComponent
{
    public TextAlign Align = align;
}

public enum StackDirection
{
    Horizontal,
    Vertical,
}

/// <summary>Cross-axis placement of children inside a container.</summary>
public enum StackCrossAlignment
{
    Start,
    Center,
    End,
}

/// <summary>
/// Container entity that stacks its child entities.  Auto-sized dimensions
/// are derived from the children and <see cref="Padding"/>; <see cref="AnchorTop"/>
/// keeps the current top edge fixed when an auto-height container resizes.
/// </summary>
public struct UiContainer(
    StackDirection direction,
    float spacing = 0,
    bool clip = false,
    float padding = 0,
    StackCrossAlignment cross = StackCrossAlignment.Center,
    bool autoWidth = false,
    bool autoHeight = false,
    bool anchorTop = false) : IComponent
{
    public StackDirection Direction = direction;
    public float Spacing = spacing;
    public bool Clip = clip;
    public float Padding = padding;
    public StackCrossAlignment Cross = cross;
    public bool AutoWidth = autoWidth;
    public bool AutoHeight = autoHeight;
    public bool AnchorTop = anchorTop;
}

/// <summary>Layer entity component. ZIndex determines draw order between layers.</summary>
public struct Layer(int zIndex) : IIndexedComponent<int>
{
    public int ZIndex = zIndex;

    public int GetIndexedValue() => ZIndex;
}

/// <summary>RenderingServer canvas item owned by an entity.</summary>
public struct VisualRid : IComponent
{
    public Rid Value;

    public VisualRid() => Value = RenderingServer.CanvasItemCreate();

    public VisualRid(Rid value) => Value = value;
}

/// <summary>Vertical ECS scroll viewport and its movable content.</summary>
public struct ScrollView(
    Entity content,
    float contentHeight,
    float viewportHeight,
    float baseContentY) : IComponent
{
    public Entity Content = content;
    public float ContentHeight = contentHeight;
    public float ViewportHeight = viewportHeight;
    public float BaseContentY = baseContentY;
    public float Offset;
}


/// <summary>Anchors a drawable to normalized points on its parent's bounds.</summary>
public struct UiAnchor(Vector2 anchor, Vector2 pivot) : IComponent
{
    public Vector2 Anchor = anchor;
    public Vector2 Pivot = pivot;
}

/// <summary>Marks a full-screen backdrop that must draw below sibling UI.</summary>
public struct UiBackground : IComponent;
