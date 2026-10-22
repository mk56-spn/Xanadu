using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Layout;

/// <summary>
/// Resolves every native <see cref="UiContainer"/> each frame. Auto-sized
/// containers are measured bottom-up so nested containers settle in one pass,
/// then all containers stack their children using padding, spacing, and
/// cross-axis alignment. Children tagged <see cref="LayoutIgnore"/> are skipped.
/// </summary>
public sealed class ContainerLayoutSystem : QuerySystem<Rectangle, UiContainer>
{
    protected override void OnUpdate()
    {
        List<Entity> containers = Query.ToEntityList().ToList();
        if (containers.Count == 0)
            return;

        // Children must be measured before their auto-sized parents.
        containers.Sort(static (left, right) =>
            Depth(right).CompareTo(Depth(left)));

        CommandBuffer buffer = CommandBuffer;

        foreach (Entity entity in containers)
            Resize(entity, buffer);

        foreach (Entity entity in containers)
            Layout(entity, buffer);
    }

    private static void Resize(Entity entity, CommandBuffer buffer)
    {
        UiContainer container = entity.GetComponent<UiContainer>();
        if (!container.AutoWidth && !container.AutoHeight)
            return;

        if (!Measure(entity, container, out Vector2 contentSize))
            return;

        ref Rectangle rectangle = ref entity.GetComponent<Rectangle>();
        Vector2 currentSize = rectangle.Bounds.Size;
        Vector2 size = new(
            container.AutoWidth ? contentSize.X : currentSize.X,
            container.AutoHeight ? contentSize.Y : currentSize.Y);

        if (size == currentSize)
            return;

        float top = 0;
        bool hasPosition = entity.HasComponent<Position>();
        if (hasPosition)
            top = entity.GetComponent<Position>().Value.Y +
                  rectangle.Bounds.Position.Y;

        rectangle.Bounds = new Rect2(-size / 2, size);

        if (hasPosition && container.AnchorTop)
        {
            ref Position position = ref entity.GetComponent<Position>();
            position.Value.Y = top - rectangle.Bounds.Position.Y;
            buffer.AddTag<Moved>(entity.Id);
        }

        buffer.AddTag<Dirty>(entity.Id);
        buffer.AddTag<LayoutDirty>(entity.Id);

        if (!entity.Parent.IsNull &&
            entity.Parent.HasComponent<UiContainer>())
        {
            buffer.AddTag<LayoutDirty>(entity.Parent.Id);
        }
    }

    private static bool Measure(
        Entity entity,
        UiContainer container,
        out Vector2 size)
    {
        float main = 0;
        float cross = 0;
        int count = 0;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 childSize = child.GetComponent<Rectangle>().Bounds.Size;
            main += container.Direction == StackDirection.Horizontal
                ? childSize.X
                : childSize.Y;
            cross = Mathf.Max(
                cross,
                container.Direction == StackDirection.Horizontal
                    ? childSize.Y
                    : childSize.X);
            count++;
        }

        if (count > 1)
            main += container.Spacing * (count - 1);

        main += container.Padding * 2;
        cross += container.Padding * 2;

        size = container.Direction == StackDirection.Horizontal
            ? new Vector2(main, cross)
            : new Vector2(cross, main);
        return true;
    }

    private static void Layout(Entity entity, CommandBuffer buffer)
    {
        UiContainer container = entity.GetComponent<UiContainer>();
        Rectangle rectangle = entity.GetComponent<Rectangle>();

        float total = 0;
        int count = 0;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 childSize = child.GetComponent<Rectangle>().Bounds.Size;
            total += container.Direction == StackDirection.Horizontal
                ? childSize.X
                : childSize.Y;
            count++;
        }

        if (count > 1)
            total += container.Spacing * (count - 1);

        float cursor = -total / 2;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            ref Position position = ref child.GetComponent<Position>();
            Vector2 childSize = child.GetComponent<Rectangle>().Bounds.Size;
            Vector2 target;

            if (container.Direction == StackDirection.Horizontal)
            {
                target = new Vector2(
                    cursor + childSize.X / 2,
                    CrossOffset(container, rectangle, childSize.Y));
                cursor += childSize.X + container.Spacing;
            }
            else
            {
                target = new Vector2(
                    CrossOffset(container, rectangle, childSize.X),
                    cursor + childSize.Y / 2);
                cursor += childSize.Y + container.Spacing;
            }

            if (position.Value == target)
                continue;

            position.Value = target;
            buffer.AddTag<Moved>(child.Id);
        }

        buffer.RemoveTag<LayoutDirty>(entity.Id);
    }

    private static float CrossOffset(
        UiContainer container,
        Rectangle containerRect,
        float childCrossSize)
    {
        float available =
            (container.Direction == StackDirection.Horizontal
                ? containerRect.Bounds.Size.Y
                : containerRect.Bounds.Size.X) -
            container.Padding * 2;

        return container.Cross switch
        {
            StackCrossAlignment.Start => -available / 2 + childCrossSize / 2,
            StackCrossAlignment.End => available / 2 - childCrossSize / 2,
            _ => 0,
        };
    }

    private static bool IsLayoutChild(Entity child) =>
        !child.Tags.Has<LayoutIgnore>() &&
        child.HasComponent<Rectangle>() &&
        child.HasComponent<Position>();

    private static int Depth(Entity entity)
    {
        int depth = 0;
        Entity parent = entity.Parent;

        while (!parent.IsNull)
        {
            depth++;
            parent = parent.Parent;
        }

        return depth;
    }
}
