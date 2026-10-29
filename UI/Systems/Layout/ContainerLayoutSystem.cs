using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Layout;

/// <summary>
/// Resolves every native <see cref="UiContainer"/> each frame. Auto-sized
/// containers are measured bottom-up so nested containers settle in one pass,
/// then all containers stack their children using padding, spacing, and
/// cross-axis alignment. Children tagged <see cref="LayoutIgnore"/> are skipped.
/// </summary>
public sealed class ContainerLayoutSystem : QuerySystem<Rectangle, UiContainer>
{
    public ContainerLayoutSystem()
    {
        Filter.AllTags(Tags.Get<LayoutDirty>());
    }

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
        if (container.IsGrid)
        {
            Grid grid = Grid.Of(entity, container);
            size = grid.Size + new Vector2(container.Padding, container.Padding) * 2;
            return true;
        }

        float main = 0;
        float cross = 0;
        int count = 0;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 childSize = SlotSize(child, container.Direction);
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

        if (container.IsGrid)
        {
            LayoutGrid(entity, container, rectangle, buffer);
            return;
        }

        float total = 0;
        int count = 0;

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 childSize = SlotSize(child, container.Direction);
            total += container.Direction == StackDirection.Horizontal
                ? childSize.X
                : childSize.Y;
            count++;
        }

        if (count > 1)
            total += container.Spacing * (count - 1);

        float cursor = StartOffset(container, rectangle, total);

        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            ref Position position = ref child.GetComponent<Position>();
            Vector2 childSize = child.GetComponent<Rectangle>().Bounds.Size;
            Vector2 slotSize = SlotSize(child, container.Direction);
            Vector2 target;

            if (container.Direction == StackDirection.Horizontal)
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
            if (container.Skew != 0 && container.Direction == StackDirection.Vertical)
                target.X -= Mathf.Tan(container.Skew) * target.Y;

            if (position.Value == target)
                continue;

            position.Value = target;
            buffer.AddTag<Moved>(child.Id);
        }

        buffer.RemoveTag<LayoutDirty>(entity.Id);
    }

    private static void LayoutGrid(
        Entity entity,
        UiContainer container,
        Rectangle rectangle,
        CommandBuffer buffer)
    {
        Grid grid = Grid.Of(entity, container);
        Vector2 size = rectangle.Bounds.Size;
        float left = container.Main switch
        {
            StackMainAlignment.Start => -size.X / 2 + container.Padding,
            StackMainAlignment.End => size.X / 2 - container.Padding - grid.Size.X,
            _ => -grid.Size.X / 2,
        };
        float top = container.Cross switch
        {
            StackCrossAlignment.Start => -size.Y / 2 + container.Padding,
            StackCrossAlignment.End => size.Y / 2 - container.Padding - grid.Size.Y,
            _ => -grid.Size.Y / 2,
        };

        int index = 0;
        foreach (Entity child in entity.ChildEntities)
        {
            if (!IsLayoutChild(child))
                continue;

            Vector2 cell = grid.CellOrigin(index, container, left, top);
            Vector2 cellSize = grid.CellSize(index);
            Vector2 target = cell + cellSize / 2;
            index++;

            ref Position position = ref child.GetComponent<Position>();
            if (position.Value == target)
                continue;

            position.Value = target;
            buffer.AddTag<Moved>(child.Id);
        }

        buffer.RemoveTag<LayoutDirty>(entity.Id);
    }

    /// <summary>A grid's column widths and row heights, and the size they add up to.</summary>
    private readonly struct Grid
    {
        private readonly float[] _columns;
        private readonly float[] _rows;
        private readonly int _columnCount;

        private Grid(float[] columns, float[] rows, int columnCount, Vector2 size)
        {
            _columns = columns;
            _rows = rows;
            _columnCount = columnCount;
            Size = size;
        }

        public Vector2 Size { get; }

        public static Grid Of(Entity entity, UiContainer container)
        {
            var sizes = new List<Vector2>();
            foreach (Entity child in entity.ChildEntities)
            {
                if (IsLayoutChild(child))
                    sizes.Add(child.GetComponent<Rectangle>().Bounds.Size);
            }

            int columnCount = container.Columns;
            int rowCount = (sizes.Count + columnCount - 1) / columnCount;
            var columns = new float[Mathf.Min(columnCount, sizes.Count)];
            var rows = new float[rowCount];
            for (int i = 0; i < sizes.Count; i++)
            {
                columns[i % columnCount] = Mathf.Max(columns[i % columnCount], sizes[i].X);
                rows[i / columnCount] = Mathf.Max(rows[i / columnCount], sizes[i].Y);
            }

            float width = 0;
            foreach (float column in columns)
                width += column;
            float height = 0;
            foreach (float row in rows)
                height += row;
            if (columns.Length > 1)
                width += container.Spacing * (columns.Length - 1);
            if (rows.Length > 1)
                height += container.RowSpacing * (rows.Length - 1);

            return new Grid(columns, rows, columnCount, new Vector2(width, height));
        }

        public Vector2 CellSize(int index) =>
            new(_columns[index % _columnCount], _rows[index / _columnCount]);

        public Vector2 CellOrigin(int index, UiContainer container, float left, float top)
        {
            int column = index % _columnCount;
            int row = index / _columnCount;
            float x = left + column * container.Spacing;
            for (int i = 0; i < column; i++)
                x += _columns[i];
            float y = top + row * container.RowSpacing;
            for (int i = 0; i < row; i++)
                y += _rows[i];
            return new Vector2(x, y);
        }
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

    private static Vector2 SlotSize(Entity child, StackDirection direction)
    {
        Vector2 size = child.GetComponent<Rectangle>().Bounds.Size;
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
