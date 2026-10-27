using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Input;

/// <summary>Animates button hover fades and short click flashes.</summary>
public sealed class ButtonVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    private const float HoverSpeed = 12f;
    private const float FlashSeconds = 0.12f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        float delta = Tick.deltaTime;

        Query.ForEachEntity((ref visual, ref fill, entity) =>
        {
            bool hovered = entity.Tags.Has<Hovered>();
            bool pressed = entity.Tags.Has<Pressed>();

            if (pressed && !visual.WasPressed)
                visual.FlashAmount = 1f;

            visual.WasPressed = pressed;

            float hoverTarget = hovered ? 1f : 0f;
            float hoverStep = delta * HoverSpeed;
            visual.HoverAmount = hoverTarget > visual.HoverAmount
                ? Mathf.Min(hoverTarget, visual.HoverAmount + hoverStep)
                : Mathf.Max(hoverTarget, visual.HoverAmount - hoverStep);

            visual.FlashAmount = Mathf.Max(
                0f,
                visual.FlashAmount - delta / FlashSeconds);

            Color color = visual.Normal.Lerp(
                visual.Hover,
                visual.HoverAmount);

            if (visual.FlashAmount > 0f)
                color = color.Lerp(visual.Flash, visual.FlashAmount);

            if (fill.Value == color)
                return;

            fill.Value = color;
            buffer.AddTag<Dirty>(entity.Id);
        });
    }
}
