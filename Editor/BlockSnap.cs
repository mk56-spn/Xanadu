using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Editor;

/// <summary>Marks the header button that turns block grid snapping on or off.</summary>
public struct BlockSnapToggle : ITag;

/// <summary>
/// Whether blocks snap to the grid. The header button's <see cref="Toggle"/> is
/// the state, so placing, moving and resizing all ask here. With no button,
/// blocks snap.
/// </summary>
public static class BlockSnap
{
    public const string Label = "SNAP";
    public const float ToggleWidth = 64f;

    /// <summary>Turns a header button into the snap switch, starting on.</summary>
    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, true));
        button.AddTag<BlockSnapToggle>();
        button.AddComponent(new UiTooltip("Snap blocks to the grid"));
        return button;
    }

    public static bool IsOn(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<BlockSnapToggle>()).Entities)
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }
}
