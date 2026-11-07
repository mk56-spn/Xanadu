using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Which tools use which part of the left panel.</summary>
public static class EditorToolSections
{
    public static readonly EditorPlacementMode[] MoveStrength =
    {
        EditorPlacementMode.Dash,
        EditorPlacementMode.Momentum,
    };

    public static readonly EditorPlacementMode[] BlockSize =
    {
        EditorPlacementMode.Blocks,
    };

    public static readonly EditorPlacementMode[] ContactCost =
    {
        EditorPlacementMode.Contact,
    };

    public static readonly EditorPlacementMode[] Grass =
    {
        EditorPlacementMode.Grass,
    };

    public static readonly EditorPlacementMode[] Flare =
    {
        EditorPlacementMode.Flare,
    };

    public static readonly EditorPlacementMode[] FormShape =
    {
        EditorPlacementMode.Blocks,
        EditorPlacementMode.FormStep,
    };
}

/// <summary>
/// Shows only the left panel sections the current tool uses, and hides the panel
/// itself when a tool uses none of them.
/// </summary>
public sealed class EditorToolSectionSystem : QuerySystem<EditorToolSection>
{
    private readonly EntityStore _entities;

    public EditorToolSectionSystem(EntityStore entities)
    {
        _entities = entities;

        // A hidden section still has to be found to be shown again.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement, EditorLeftMenu>(_entities);

        if (editor.IsNull)
            return;

        EditorPlacementMode mode = editor.GetComponent<EditorPlacement>().Mode;
        CommandBuffer buffer = CommandBuffer;
        bool any = false;
        Query.ForEachEntity((ref section, entity) =>
        {
            bool shown = Array.IndexOf(section.Modes, mode) >= 0;
            any |= shown;
            UiLayout.SetHidden(entity, buffer, !shown);
        });

        // The open group panel sits over where this panel floats.
        UiLayout.SetHidden(editor.GetComponent<EditorLeftMenu>().Panel, buffer, !any || EditorGroupState.IsOpen(editor));
    }
}
