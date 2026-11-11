using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Marks the grid menu button that turns placement collision on or off.</summary>
public struct PlacementCollisionToggle : ITag;

/// <summary>
/// Whether painting and pasting refuse to put a block on a block, or a spike on
/// a spike. The grid menu button's <see cref="Toggle"/> is the state. Off by
/// default, and with no button, so blocks can be stacked and a copy pastes whole.
/// </summary>
public static class PlacementCollision
{
    public const string Label = "COLLISION";

    public static Entity Attach(Entity button, bool on = false)
    {
        button.AddComponent(new Toggle(Label, Label, on));
        button.AddTag<PlacementCollisionToggle>();
        button.AddComponent(new UiTooltip(
            "Stop blocks landing on blocks and spikes on spikes. Off by default: items can overlap"));
        return button;
    }

    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<PlacementCollisionToggle>()).WithDisabled().Entities)
            return toggle.GetComponent<Toggle>().Value;

        return false;
    }

    /// <summary>Whether a block may go in <paramref name="box"/>: always, unless collision is on and one is there.</summary>
    public static bool BlockFits(EntityStore store, Rect2 box) =>
        !IsOn(store) || GridPlacement.IsBlockSpaceFree(store, box);

    /// <inheritdoc cref="BlockFits"/>
    public static bool SpikeFits(EntityStore store, Rect2 box) =>
        !IsOn(store) || GridPlacement.IsSpikeSpaceFree(store, box);
}
