using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>Marks the header button that shows or hides an editor's viewport grid.</summary>
public struct ViewportGridToggle : ITag;

/// <summary>
/// The grid switch every editor shares. The header button's <see cref="Toggle"/>
/// is the state, so a tool only attaches the button and asks whether to draw.
/// A tool with no button draws its grid.
/// </summary>
public static class ViewportGrid
{
    public const string Label = "GRID";
    public const float ToggleWidth = 64f;

    /// <summary>Turns a header button into the grid switch, starting on.</summary>
    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, true));
        button.AddTag<ViewportGridToggle>();
        button.AddComponent(new UiTooltip("Show or hide the grid"));
        return button;
    }

    public static bool IsVisible(EntityStore store)
    {
        foreach (Entity toggle in Toggles(store))
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }

    /// <summary>Sets the switch, for a scene rebuilt with its button back at the default.</summary>
    public static void SetVisible(EntityStore store, bool visible)
    {
        foreach (Entity toggle in Toggles(store))
            toggle.GetComponent<Toggle>().Value = visible;
    }

    private static QueryEntities Toggles(EntityStore store) =>
        store.Query<Toggle>().AllTags(Tags.Get<ViewportGridToggle>()).Entities;
}
