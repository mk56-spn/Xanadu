using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Folds and unfolds collapsible sections: a header press flips the section,
/// a folded body is hidden, and the header's arrow follows the state.
/// </summary>
public sealed class UiCollapsibleSystem : QuerySystem<UiCollapsible>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity section in Query.ToEntityList())
        {
            ref UiCollapsible state = ref section.GetComponent<UiCollapsible>();
            Entity header = UiCollapsibleControl.HeaderOf(section);
            bool pressed = header.Tags.Has<Pressed>();
            if (pressed && !state.Held)
                state.Expanded = !state.Expanded;
            state.Held = pressed;

            UiLayout.SetHidden(UiCollapsibleControl.BodyOf(section), buffer, !state.Expanded);
            SetHeader(header, UiCollapsibleControl.Header(state.Title, state.Expanded), buffer);
        }
    }

    private static void SetHeader(Entity header, string label, CommandBuffer buffer)
    {
        if (!header.HasComponent<Text>())
            return;

        ref Text text = ref header.GetComponent<Text>();
        if (text.Value == label)
            return;

        text.Value = label;
        buffer.AddTag<Dirty>(header.Id);
    }
}
