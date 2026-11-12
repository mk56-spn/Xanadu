using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Edits the brush for new contact barriers and directional notes: each bar's
/// cost steps with its − and + buttons, inside the barrier limits
/// (<see cref="BarCosts.With"/>), and the rhombus button turns diagonal on and off.
/// </summary>
public sealed class EditorContactCostSystem : QuerySystem<UiCommand>
{
    private static readonly EditorReadoutKind[] BarReadouts =
    {
        EditorReadoutKind.ContactFirst,
        EditorReadoutKind.ContactSecond,
        EditorReadoutKind.ContactThird,
    };

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
            UiCommand button = entity.GetComponent<UiCommand>();
            if (button.As<EditorButtonKind>() is not (EditorButtonKind.ContactCost or EditorButtonKind.Diagonal))
                continue;

            if (!_presses.Started(entity))
                continue;

            if (button.Is(EditorButtonKind.Diagonal))
            {
                state.Diagonal = !state.Diagonal;
                continue;
            }

            int bar = System.Math.Abs(button.Arg) - 1;
            int step = System.Math.Sign(button.Arg);
            state.Costs = state.Costs.With(bar, state.Costs[bar] + step);
        }

        for (int bar = 0; bar < BarReadouts.Length; bar++)
            EditorReadout.Show(_entities, BarReadouts[bar], $"BAR {bar + 1}: {state.Costs[bar]}", CommandBuffer);
        EditorReadout.Show(_entities, EditorReadoutKind.Diagonal, state.Diagonal ? "RHOMBUS: ON" : "RHOMBUS: OFF", CommandBuffer);
    }
}
