using Xanadu.Core;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Input;

/// <summary>Animates button hover fades and short click flashes.</summary>
public sealed class ButtonVisualSystem : QuerySystem<ButtonVisual, FillColor>
{
    private const float HoverSpeed = 12f;
    private const float FlashSeconds = 0.12f;

    public ButtonVisualSystem() => Filter.WithoutAnyTags(Tags.Get<UiDisabled>());

    protected override void OnUpdate()
    {
        Query.EachEntity(new AnimateButtons(Query.Store, CommandBuffer, Tick.deltaTime));
    }

    private readonly record struct AnimateButtons(EntityStore Store, CommandBuffer Buffer, float Delta) : IEachEntity<ButtonVisual, FillColor>
    {
        public void Execute(ref ButtonVisual visual, ref FillColor fill, int id)
        {
            Entity entity = Store.GetEntityById(id);

            bool hovered = entity.Tags.Has<Hovered>();
            bool pressed = entity.Tags.Has<Pressed>();

            if (pressed && !visual.WasPressed)
                visual.FlashAmount = 1f;

            visual.WasPressed = pressed;

            float hoverTarget = hovered ? 1f : 0f;
            float hoverStep = Delta * HoverSpeed;
            visual.HoverAmount = hoverTarget > visual.HoverAmount
                ? Math.Min(hoverTarget, visual.HoverAmount + hoverStep)
                : Math.Max(hoverTarget, visual.HoverAmount - hoverStep);

            visual.FlashAmount = Math.Max(
                0f,
                visual.FlashAmount - Delta / FlashSeconds);

            Color color = visual.Normal.Lerp(
                visual.Hover,
                visual.HoverAmount);

            if (visual.FlashAmount > 0f)
                color = color.Lerp(visual.Flash, visual.FlashAmount);

            if (fill.Value == color)
                return;

            fill.Value = color;
            Buffer.AddTag<Dirty>(id);
        }
    }
}
