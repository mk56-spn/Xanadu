using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// A strip of tab buttons and the floating bodies they switch between. The
/// selected body sits centred on <see cref="BodyCenter"/>; the others are
/// parked off screen, because a container in a stack cannot be hidden.
/// </summary>
public struct UiTabs : IComponent
{
    public Entity[] Buttons;
    public Entity[] Bodies;
    public Vector2 BodyCenter;
    public int Selected;
    public bool[] Held;
    public Color OnColor;
    public Color OffColor;

    public UiTabs(Entity[] buttons, Entity[] bodies, Vector2 bodyCenter, int selected = 0)
    {
        Buttons = buttons;
        Bodies = bodies;
        BodyCenter = bodyCenter;
        Selected = selected;
        Held = new bool[buttons.Length];
        OnColor = Palette.Primary;
        OffColor = Palette.Muted;
    }
}
