using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Layout;

/// <summary>Grid containers: columns as wide as their widest child, rows as tall as their tallest.</summary>
public sealed partial class ContainerLayoutSystem
{
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
}
