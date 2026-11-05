using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Marks the header button that turns an editor's grid snapping on or off.</summary>
public struct ViewportSnapToggle : ITag;

/// <summary>
/// The snap-to-grid every editor shares, beside <see cref="ViewportGrid"/>: the
/// switch and the sum. The header button's <see cref="Toggle"/> is the state, so
/// anything that moves asks here, even while its button sits in a closed menu.
/// With no button, things snap.
/// </summary>
public static class ViewportSnap
{
    public const string Label = "SNAP";
    public const float ToggleWidth = 64f;

    /// <summary>Turns a header button into the snap switch; what it snaps is the tool's to say.</summary>
    public static Entity Attach(Entity button, string tooltip, bool on = true)
    {
        button.AddComponent(new Toggle(Label, Label, on));
        button.AddTag<ViewportSnapToggle>();
        button.AddComponent(new UiTooltip(tooltip));
        return button;
    }

    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<ViewportSnapToggle>()).WithDisabled().Entities)
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }

    /// <summary>Nearest grid line, rounding halves up so snapping is predictable.</summary>
    public static float Snap(float value, float cell) =>
        cell <= 0 ? value : Mathf.Floor(value / cell + 0.5f) * cell;

    public static Vector2 Snap(Vector2 value, Vector2 cell) => new(
        Snap(value.X, cell.X),
        Snap(value.Y, cell.Y));
}
