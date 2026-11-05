using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Edits the left/right charge newly placed contact barriers ask for. The four
/// buttons are L−, L+, R− and R+; the readout shows the current pair.
/// </summary>
public sealed class EditorContactCostSystem : QuerySystem<EditorButton>
{
    private const int MaxCost = 3;

    private readonly EntityStore _entities;
    private readonly ButtonPress _presses = new();

    public EditorContactCostSystem(EntityStore entities)
    {
        _entities = entities;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement>(_entities);

        if (editor.IsNull)
            return;

        ref EditorPlacement state = ref editor.GetComponent<EditorPlacement>();

        foreach (Entity entity in Query.ToEntityList())
        {
            EditorButton button = entity.GetComponent<EditorButton>();
            if (button.Kind is not (EditorButtonKind.ContactLeft or EditorButtonKind.ContactRight))
                continue;

            if (!_presses.Started(entity))
                continue;

            if (button.Kind == EditorButtonKind.ContactLeft)
                state.ContactLeft = Math.Clamp(state.ContactLeft + button.Arg, 0, MaxCost);
            else
                state.ContactRight = Math.Clamp(state.ContactRight + button.Arg, 0, MaxCost);
        }

        EditorReadout.Show(
            _entities,
            EditorReadoutKind.ContactCost,
            $"L{state.ContactLeft}  R{state.ContactRight}",
            CommandBuffer);
    }
}
