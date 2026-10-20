using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Layout;

/// <summary>Lays out dirty container children as a horizontal or vertical stack.</summary>
public sealed class ContainerLayoutSystem : QuerySystem<Rectangle, UiContainer>
{
    public ContainerLayoutSystem()
    {
        Filter.AllTags(Tags.Get<LayoutDirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref _, ref container, containerEntity) =>
        {
            float total = 0;
            int count = 0;

            foreach (Entity child in containerEntity.ChildEntities)
            {
                if (!child.TryGetComponent<Rectangle>(out var childRect))
                    continue;

                total += container.Direction == StackDirection.Horizontal
                    ? childRect.Bounds.Size.X
                    : childRect.Bounds.Size.Y;
                count++;
            }

            if (count == 0)
            {
                buffer.RemoveTag<LayoutDirty>(containerEntity.Id);
                return;
            }

            total += container.Spacing * (count - 1);
            float cursor = -total / 2;

            foreach (Entity child in containerEntity.ChildEntities)
            {
                if (!child.TryGetComponent<Rectangle>(out var childRect) ||
                    !child.HasComponent<Position>())
                {
                    continue;
                }

                ref Position position = ref child.GetComponent<Position>();

                if (container.Direction == StackDirection.Horizontal)
                {
                    position.Value = new(cursor + childRect.Bounds.Size.X / 2, 0);
                    cursor += childRect.Bounds.Size.X + container.Spacing;
                }
                else
                {
                    position.Value = new(0, cursor + childRect.Bounds.Size.Y / 2);
                    cursor += childRect.Bounds.Size.Y + container.Spacing;
                }

                buffer.AddTag<Moved>(child.Id);
            }

            buffer.RemoveTag<LayoutDirty>(containerEntity.Id);
        });
    }
}
