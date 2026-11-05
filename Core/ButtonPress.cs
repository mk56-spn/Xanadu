using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Turns a held ECS button into a single press event. Every panel that acts on
/// the start of a press rather than the hold shares this: the first frame the
/// button is seen down counts, later frames do not, and releasing re-arms it.
/// </summary>
public sealed class ButtonPress
{
    private readonly HashSet<int> _held = new();

    /// <summary>True on the one frame <paramref name="button"/> starts being pressed.</summary>
    public bool Started(Entity button) => Started(button, button.Tags.Has<Pressed>());

    /// <summary>
    /// True on the one frame the effective press starts. Callers whose press is
    /// gated, such as a panel that hides itself, pass their own down state so the
    /// latch re-arms exactly when their control would.
    /// </summary>
    public bool Started(Entity button, bool pressed)
    {
        bool wasHeld = !_held.Add(button.Id);
        if (!pressed)
            _held.Remove(button.Id);

        return pressed && !wasHeld;
    }
}
