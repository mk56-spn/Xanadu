using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Edits the left/right charge newly placed contact barriers ask for. The four
/// buttons are L−, L+, R− and R+; the readout shows the current pair.
/// </summary>
public sealed class EditorContactCostSystem : QuerySystem<EditorContactCostButton>
{
    private const int MaxCost = 3;

    private readonly EntityStore _entities;
    private readonly HashSet<int> _held = new();

    public EditorContactCostSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorContactCostState, EditorContactCostReadout>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        EditorContactCostState state =
            editor.GetComponent<EditorContactCostState>();

        foreach (Entity entity in Query.ToEntityList())
        {
            EditorContactCostButton button =
                entity.GetComponent<EditorContactCostButton>();
            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _held.Contains(entity.Id);
            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (!pressed || wasPressed)
                continue;

            if (button.Left)
                state.Left = Math.Clamp(state.Left + button.Delta, 0, MaxCost);
            else
                state.Right = Math.Clamp(state.Right + button.Delta, 0, MaxCost);
        }

        editor.GetComponent<EditorContactCostState>() = state;

        Entity readout = editor.GetComponent<EditorContactCostReadout>().Text;
        if (readout.IsNull || !readout.HasComponent<Text>())
            return;

        string value = $"L{state.Left}  R{state.Right}";
        ref Text text = ref readout.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        CommandBuffer buffer = CommandBuffer;
        buffer.AddTag<Dirty>(readout.Id);
    }
}
