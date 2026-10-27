using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// Opts an entity into keyboard focus even when it does not use one of the
/// built-in control components. Buttons, text fields, dropdowns, toggles,
/// sliders and wheels are focusable without it.
/// </summary>
public struct Focusable : ITag;

/// <summary>Keeps an entity out of the Tab order even when it looks focusable.</summary>
public struct FocusIgnore : ITag;

/// <summary>The control currently holding keyboard focus.</summary>
public struct Focused : ITag;

/// <summary>One edge of the focus ring; four edges make the rectangle.</summary>
public struct UiFocusRingEdge(int index) : IComponent
{
    public int Index = index;
}
