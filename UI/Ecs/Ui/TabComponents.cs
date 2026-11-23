using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A strip of tab buttons and the bodies they switch between; only the
/// selected body is shown. A floating body is centred on
/// <see cref="BodyCenter"/>; one in a container's stack is placed by the stack.
/// </summary>
public struct UiTabs(Entity[] buttons, Entity[] bodies, Vector2 bodyCenter, int selected = 0) : IComponent
{
    public Entity[] Buttons = buttons;
    public Entity[] Bodies = bodies;
    public Vector2 BodyCenter = bodyCenter;
    public int Selected = selected;
    public bool[] Held = new bool[buttons.Length];
    public Color OnColor = Palette.Primary;
    public Color OffColor = Palette.Muted;
}
