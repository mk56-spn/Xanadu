using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// A strip of tab buttons and the bodies they switch between; only the
/// selected body is shown. A floating body is centred on
/// <see cref="BodyCenter"/>; one in a container's stack is placed by the stack.
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
