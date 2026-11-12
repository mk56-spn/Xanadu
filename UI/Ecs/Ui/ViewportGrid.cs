using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// The grid switch every editor shares. The header button's <see cref="Toggle"/>
/// is the state, so a tool only attaches the button and asks whether to draw.
/// A tool with no button draws its grid.
/// </summary>
public static class ViewportGrid
{
    public const string Label = "GRID";
    public const float ToggleWidth = 64f;

    /// <summary>The switch's name in its store (<see cref="UiToggles.Switch"/>).</summary>
    public const string Id = "viewport-grid";

    /// <summary>Turns a header button into the grid switch, starting on.</summary>
    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, true));
        button.Switch(Id);
        button.AddComponent(new UiTooltip("Show or hide the grid"));
        return button;
    }

    /// <summary>Whether to draw the grid; a tool with no button draws it.</summary>
    public static bool IsVisible(EntityStore store) => UiToggles.On(store, Id, none: true);
}
