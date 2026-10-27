using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Base colours and animated state for a hoverable/clickable button.</summary>
public struct ButtonVisual(Color normal, Color hover, Color flash) : IComponent
{
    public Color Normal = normal;
    public Color Hover = hover;
    public Color Flash = flash;
    public float HoverAmount;
    public float FlashAmount;
    public bool WasPressed;

    public static ButtonVisual Create(Color normal) => new(
        normal,
        normal.Lerp(Colors.White, 0.16f),
        Colors.White);
}
