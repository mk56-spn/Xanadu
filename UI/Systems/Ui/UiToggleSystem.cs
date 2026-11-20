using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Handles click-to-toggle ECS buttons and keeps their labels in sync. A flip
/// is announced with the entity's native <c>OnComponentChanged</c> (an Update
/// of <see cref="Toggle"/>); owners subscribe on the toggle they built (see
/// <see cref="UiToggles"/>) and keep the value in their own model.
/// </summary>
public sealed class ToggleSystem : QuerySystem<Toggle, Text>
{
    protected override void OnUpdate()
    {
        Query.EachEntity(new FlipToggles(Query.Store, CommandBuffer));
    }

    private readonly record struct FlipToggles(EntityStore Store, CommandBuffer Buffer) : IEachEntity<Toggle, Text>
    {
        public void Execute(ref Toggle toggle, ref Text text, int id)
        {
            Entity entity = Store.GetEntityById(id);

            bool pressed = entity.Tags.Has<Pressed>();
            bool flipped = pressed && !toggle.Held;
            if (flipped)
            {
                toggle.Value = !toggle.Value;
                Buffer.AddTag<Dirty>(id);
            }
            toggle.Held = pressed;

            // Written back through the store so its owner hears it: the native
            // ComponentChanged event (Update) on the entity, at playback.
            if (flipped)
                Buffer.AddComponent(id, toggle);

            string expected = toggle.Value ? toggle.OnText : toggle.OffText;
            if (text.Value != expected)
            {
                text.Value = expected;
                Buffer.AddTag<Dirty>(id);
            }

            if (entity.HasComponent<ButtonVisual>())
            {
                ref ButtonVisual visual = ref entity.GetComponent<ButtonVisual>();
                Color normal = toggle.Value ? toggle.OnColor : toggle.OffColor;
                if (visual.Normal != normal)
                {
                    visual.Normal = normal;
                    visual.Hover = normal.Lerp(Colors.White, 0.16f);
                    visual.Flash = Colors.White;
                    Buffer.AddTag<Dirty>(id);
                }
            }
        }
    }
}
