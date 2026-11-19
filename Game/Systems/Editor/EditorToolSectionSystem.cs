using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>Which tools use which part of the left panel.</summary>
public static class EditorToolSections
{
    /// <summary>The 1×/2×/3× speed buttons: the speed new curve points get.</summary>
    public static readonly EditorPlacementMode[] MoveStrength =
    {
        EditorPlacementMode.Curve,
    };

    /// <summary>The hold length stepper: how many beats the selected holds last.</summary>
    public static readonly EditorPlacementMode[] HoldLength =
    {
        EditorPlacementMode.Hold,
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

        UiLayout.SetHidden(editor.GetComponent<EditorLeftMenu>().Panel, buffer, !any);
    }
}
