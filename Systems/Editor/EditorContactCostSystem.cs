using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Edits the left/right charge newly placed contact barriers ask for. The four
/// buttons are L−, L+, R− and R+; the readout shows the current pair.
/// </summary>
public sealed class EditorContactCostSystem : QuerySystem<EditorButton>
{
    private const int MaxCost = 3;

    private readonly EntityStore _entities;
    private readonly HashSet<int> _held = new();

    public EditorContactCostSystem(EntityStore entities)
    {
        _entities = entities;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorPlacement>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorPlacement state = ref editor.GetComponent<EditorPlacement>();

        foreach (Entity entity in Query.ToEntityList())
        {
            EditorButton button = entity.GetComponent<EditorButton>();
            if (button.Kind is not (EditorButtonKind.ContactLeft or EditorButtonKind.ContactRight))
                continue;

            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (!pressed || wasPressed)
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
