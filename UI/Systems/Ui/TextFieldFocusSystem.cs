using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>Keeps editor text-field focus in sync with pointer presses.</summary>
public sealed class TextFieldFocusSystem : QuerySystem<Rectangle>
{
    protected override void OnUpdate()
    {
        Entity focused = default;
        foreach (Entity entity in Query.Store.Entities.ToEntityList())
        {
            if (entity.Tags.Has<TextFieldFocused>())
            {
                focused = entity;
                break;
            }
        }

        foreach (Entity pressed in Query.ToEntityList())
        {
            if (!pressed.Tags.Has<Pressed>())
                continue;

            CommandBuffer buffer = CommandBuffer;
            if (pressed.HasComponent<TextField>())
            {
                if (!focused.IsNull && focused.Id != pressed.Id)
                    buffer.RemoveTag<TextFieldFocused>(focused.Id);
                buffer.AddTag<TextFieldFocused>(pressed.Id);
                return;
            }

            if (!focused.IsNull)
                buffer.RemoveTag<TextFieldFocused>(focused.Id);
            return;
        }
    }
}
