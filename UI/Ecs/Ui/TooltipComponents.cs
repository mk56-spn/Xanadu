using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Hover text for a button. The animator's tooltip system shows it while the
/// entity carries <see cref="Hovered"/>, which the input system adds to the
/// topmost hoverable button.
/// </summary>
public struct UiTooltip(string text) : IComponent
{
    public string Text = text;
}
