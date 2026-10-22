using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;

namespace Writer.Systems.Ui;

/// <summary>Handles click-to-toggle ECS buttons and keeps their labels in sync.</summary>
public sealed class ToggleSystem : QuerySystem<Toggle, Text>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref toggle, ref text, entity) =>
        {
            bool pressed = entity.Tags.Has<Pressed>();
            if (pressed && !toggle.Held)
            {
                toggle.Value = !toggle.Value;
                buffer.AddTag<ToggleChanged>(entity.Id);
                buffer.AddTag<Dirty>(entity.Id);
            }
            toggle.Held = pressed;

            string expected = toggle.Value ? toggle.OnText : toggle.OffText;
            if (text.Value != expected)
            {
                text.Value = expected;
                buffer.AddTag<Dirty>(entity.Id);
            }
        });
    }
}
