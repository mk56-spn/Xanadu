using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Folds and unfolds collapsible sections: a header press flips the section,
/// a folded body is parked off screen and dropped from the stack, and the
/// header's arrow follows the state.
/// </summary>
public sealed class UiCollapsibleSystem : QuerySystem<UiCollapsible>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        foreach (Entity section in Query.ToEntityList())
        {
            ref UiCollapsible state = ref section.GetComponent<UiCollapsible>();
            bool pressed = !state.Header.IsNull && state.Header.Tags.Has<Pressed>();
            if (pressed && !state.Held)
                state.Expanded = !state.Expanded;
            state.Held = pressed;

            SetBody(section, state.Body, state.Expanded, buffer);
            SetHeader(state.Header, UiCollapsibleControl.Header(state.Title, state.Expanded), buffer);
        }
    }

    private static void SetBody(Entity section, Entity body, bool expanded, CommandBuffer buffer)
    {
        if (body.IsNull)
            return;

        bool ignored = body.Tags.Has<LayoutIgnore>();
        if (!expanded && !ignored)
        {
            UiLayout.Hide(body, buffer);
        }
        else if (expanded && ignored)
        {
            UiLayout.SetIgnored(body, buffer, false);
            buffer.AddTag<LayoutDirty>(section.Id);
        }
    }

    private static void SetHeader(Entity header, string label, CommandBuffer buffer)
    {
        if (header.IsNull || !header.HasComponent<Text>())
            return;

        ref Text text = ref header.GetComponent<Text>();
        if (text.Value == label)
            return;

        text.Value = label;
        buffer.AddTag<Dirty>(header.Id);
    }
}
