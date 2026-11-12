using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Layout;

/// <summary>
/// Resolves every native <see cref="UiContainer"/> each frame, in two passes.
/// Measuring goes bottom-up: auto-sized containers fit their content, deepest
/// first, so nested containers settle in one frame. Placing goes top-down: a
/// container sizes its <see cref="UiFlex"/> children (grow, stretch, min and
/// max) before they place their own, and a child it resized is placed again
/// the same frame. Children tagged <see cref="LayoutIgnore"/> are skipped.
/// </summary>
public sealed partial class ContainerLayoutSystem : QuerySystem<Rectangle, UiContainer>
{
    private readonly List<Entity> _children = new();
    private readonly List<Vector2> _sizes = new();
    private readonly List<Entity> _resized = new();

    public ContainerLayoutSystem()
    {
        Filter.AllTags(Tags.Get<LayoutDirty>());
    }

    protected override void OnUpdate()
    {
        List<Entity> containers = Query.ToEntityList().ToList();
        if (containers.Count == 0)
            return;

        CommandBuffer buffer = CommandBuffer;
        var queued = new HashSet<Entity>(containers);

        // Measure: children before their auto-sized parents.
        containers.Sort(static (left, right) => Depth(right).CompareTo(Depth(left)));

        // A resize reaches its auto-sized ancestors this frame, however deep the tree.
        for (int i = 0; i < containers.Count; i++)
        {
            Entity entity = containers[i];
            Entity parent = entity.Parent;
            if (Resize(entity, buffer) &&
                !parent.IsNull &&
                parent.HasComponent<UiContainer>() &&
                parent.HasComponent<Rectangle>() &&
                queued.Add(parent))
            {
                int depth = Depth(parent);
                int at = i + 1;
                while (at < containers.Count && Depth(containers[at]) >= depth)
                    at++;
                containers.Insert(at, parent);
            }
        }

        // Place: parents before children, so a child they resize is placed in its new size.
        containers.Reverse();
        for (int i = 0; i < containers.Count; i++)
        {
            _resized.Clear();
            Layout(containers[i], buffer);
            foreach (Entity child in _resized)
            {
                if (!queued.Add(child))
                    continue;

                int depth = Depth(child);
                int at = i + 1;
                while (at < containers.Count && Depth(containers[at]) <= depth)
                    at++;
                containers.Insert(at, child);
            }
        }
    }

    /// <summary>Fits an auto-sized container to its content; true when the size it asks for changed.</summary>
    private static bool Resize(Entity entity, CommandBuffer buffer)
    {
        UiContainer container = entity.GetComponent<UiContainer>();
        if (!container.AutoWidth && !container.AutoHeight)
            return false;

        if (!Measure(entity, container, out Vector2 contentSize))
            return false;

        ref Rectangle rectangle = ref entity.GetComponent<Rectangle>();
        bool hasFlex = entity.TryGetComponent(out UiFlex flex);

        // Inside a stack the parent sizes it; it only says what it would like.
        if (hasFlex && IsLayoutChild(entity) && entity.Parent.HasComponent<UiContainer>())
        {
            Vector2 natural = new(
                container.AutoWidth ? contentSize.X : flex.Natural.X,
                container.AutoHeight ? contentSize.Y : flex.Natural.Y);
            if (natural == flex.Natural)
                return false;

            entity.GetComponent<UiFlex>().Natural = natural;
            return true;
        }

        Vector2 currentSize = rectangle.Bounds.Size;
        Vector2 size = new(
            container.AutoWidth ? contentSize.X : currentSize.X,
            container.AutoHeight ? contentSize.Y : currentSize.Y);
        if (hasFlex)
            size = flex.Clamp(size);

        if (size == currentSize)
            return false;

        float top = 0;
        float left = 0;
        bool hasPosition = entity.HasComponent<Position>();
        if (hasPosition)
        {
            Vector2 at = entity.GetComponent<Position>().Value;
            top = at.Y + rectangle.Bounds.Position.Y;
            left = at.X + rectangle.Bounds.Position.X;
        }

        rectangle.Bounds = new Rect2(-size / 2, size);

        if (hasPosition && (container.AnchorTop || container.AnchorLeft))
        {
            ref Position position = ref entity.GetComponent<Position>();
            if (container.AnchorTop)
                position.Value.Y = top - rectangle.Bounds.Position.Y;

            if (container.AnchorLeft)
                position.Value.X = left - rectangle.Bounds.Position.X;

            buffer.AddTag<Moved>(entity.Id);
        }

        buffer.AddTag<Dirty>(entity.Id);
        buffer.AddTag<LayoutDirty>(entity.Id);
        return true;
    }

    private static bool Measure(
        Entity entity,
        UiContainer container,
        out Vector2 size)
    {
        if (container.IsGrid)
        {
            Grid grid = Grid.Of(entity, container);
            size = grid.Size + new Vector2(container.Padding, container.Padding) * 2;
            return true;
        }

        bool horizontal = container.Direction == StackDirection.Horizontal;
        float main = 0;
        float cross = 0;
        int count = 0;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 childSize = SlotSize(child, NaturalSize(child), container.Direction);
            main += horizontal ? childSize.X : childSize.Y;
            cross = Mathf.Max(cross, horizontal ? childSize.Y : childSize.X);
            count++;
        }

        if (count > 1)
            main += container.Spacing * (count - 1);

        main += container.Padding * 2;
        cross += container.Padding * 2;

        size = horizontal ? new Vector2(main, cross) : new Vector2(cross, main);
        return true;
    }

    private void Layout(Entity entity, CommandBuffer buffer)
    {
        UiContainer container = entity.GetComponent<UiContainer>();
        Rectangle rectangle = entity.GetComponent<Rectangle>();

        if (container.IsGrid)
        {
            LayoutGrid(entity, container, rectangle, buffer);
            return;
        }

        bool horizontal = container.Direction == StackDirection.Horizontal;
        SizeChildren(entity, container, rectangle, buffer);

        float total = 0;
        for (int i = 0; i < _children.Count; i++)
        {
            Vector2 slot = SlotSize(_children[i], _sizes[i], container.Direction);
            total += horizontal ? slot.X : slot.Y;
        }

        if (_children.Count > 1)
            total += container.Spacing * (_children.Count - 1);

        float cursor = StartOffset(container, rectangle, total);

        for (int i = 0; i < _children.Count; i++)
        {
            Entity child = _children[i];
            Vector2 childSize = _sizes[i];
            Resized(child, childSize, buffer);

            ref Position position = ref child.GetComponent<Position>();
            Vector2 slotSize = SlotSize(child, childSize, container.Direction);
            Vector2 target;

            if (horizontal)
            {
                target = new Vector2(
                    cursor + childSize.X / 2,
                    CrossOffset(container, rectangle, childSize.Y));
                cursor += slotSize.X + container.Spacing;
            }
            else
            {
                target = new Vector2(
                    CrossOffset(container, rectangle, childSize.X),
                    cursor + childSize.Y / 2);
                cursor += slotSize.Y + container.Spacing;
            }

            // A skewed vertical stack flows along its slanted sides: the higher a
            // child sits, the further right.
            if (container.Skew != 0 && !horizontal)
                target.X -= Mathf.Tan(container.Skew) * target.Y;

            if (position.Value == target)
                continue;

            position.Value = target;
            buffer.AddTag<Moved>(child.Id);
        }

        buffer.RemoveTag<LayoutDirty>(entity.Id);
    }

    /// <summary>Gives a child its laid-out size; a resized container is placed again after this one.</summary>
    private void Resized(Entity child, Vector2 size, CommandBuffer buffer)
    {
        ref Rectangle rect = ref child.GetComponent<Rectangle>();
        if (rect.Bounds.Size == size)
            return;

        rect.Bounds = new Rect2(-size / 2, size);
        buffer.AddTag<Dirty>(child.Id);
        if (child.HasComponent<UiContainer>())
            _resized.Add(child);
    }

    private static float StartOffset(
        UiContainer container,
        Rectangle rectangle,
        float total)
    {
        float size = container.Direction == StackDirection.Horizontal
            ? rectangle.Bounds.Size.X
            : rectangle.Bounds.Size.Y;

        return container.Main switch
        {
            StackMainAlignment.Start => -size / 2 + container.Padding,
            StackMainAlignment.End => size / 2 - container.Padding - total,
            _ => -total / 2,
        };
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

    /// <summary>The room a child takes in the stack: its size, plus an open dropdown's list.</summary>
    private static Vector2 SlotSize(Entity child, Vector2 size, StackDirection direction)
    {
        if (!child.HasComponent<Dropdown>())
            return size;

        Dropdown dropdown = child.GetComponent<Dropdown>();
        if (!dropdown.Open)
            return size;

        Vector2 listSize = dropdown.ListSize;
        if (direction == StackDirection.Vertical)
            size.Y += listSize.Y + 4;
        else
            size.X = Mathf.Max(size.X, listSize.X);
        return size;
    }

    private static bool IsLayoutChild(Entity child) =>
        child.Enabled &&
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
