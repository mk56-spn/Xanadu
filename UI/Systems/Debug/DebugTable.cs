using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Debug;

/// <summary>What a cell was asked to show, which its text may be a shortened form of.</summary>
public struct DebugCell : IComponent
{
    public string Source;
}

/// <summary>One column of a <see cref="DebugTable"/>; a text past <see cref="MaxWidth"/> is cut short.</summary>
public readonly record struct DebugColumn(string Title, TextAlign Align, float MaxWidth = float.MaxValue);

/// <summary>
/// A table of text cells whose columns grow to fit the widest cell, so the panel
/// holding it always fits. Each row is a child of the table and each cell a child of
/// its row. Columns do not shrink, so they stay put while the numbers change.
/// </summary>
public sealed class DebugTable
{
    public const float RowHeight = UiType.Body + UiMetrics.Space.Xxs;

    private const int FontSize = UiType.Body;

    /// <summary>The estimate runs short on narrow text, and a box one glyph short drops that glyph.</summary>
    private const float CellSlack = UiMetrics.Space.Sm;
    private static readonly Color Transparent = new(0, 0, 0, 0);

    private readonly DebugColumn[] _columns;
    private readonly float[] _widths;
    private readonly UiNode _table;

    public DebugTable(UiNode parent, params DebugColumn[] columns)
    {
        _columns = columns;
        _widths = new float[columns.Length];
        _table = parent.Column().Cross(StackCrossAlignment.Start);

        for (int i = 0; i < columns.Length; i++)
            _widths[i] = CellWidth(columns[i].Title);

        Entity header = AddRow();
        for (int i = 0; i < columns.Length; i++)
        {
            UiText.Set(header.ChildEntities[i], columns[i].Title);
            SetColor(header.ChildEntities[i], DebugPalette.Header);
        }
    }

    /// <summary>The rows under the header.</summary>
    public int RowCount => _table.Entity.ChildCount - 1;

    public void SetRowCount(int count)
    {
        while (RowCount < count)
            AddRow();
        while (RowCount > count)
            UiLayout.DeleteSubtree(_table.Entity.ChildEntities[_table.Entity.ChildCount - 1]);
    }

    public void Set(int row, int column, string text)
    {
        Entity cell = Row(row).ChildEntities[column];
        ref DebugCell shown = ref cell.GetComponent<DebugCell>();
        if (shown.Source == text)
            return;

        shown.Source = text;
        text = UiTextFit.Cut(text, FontSize, _columns[column].MaxWidth);
        UiText.Set(cell, text);

        float width = CellWidth(text);
        if (width > _widths[column])
            Widen(column, width);
    }

    public void SetColor(int row, int column, Color color) =>
        SetColor(Row(row).ChildEntities[column], color);

    /// <summary>Tints a whole row, for the selected one.</summary>
    public void SetRowFill(int row, Color color)
    {
        Entity entity = Row(row);
        ref FillColor fill = ref entity.GetComponent<FillColor>();
        if (fill.Value == color)
            return;

        fill.Value = color;
        entity.AddTag<Dirty>();
    }

    private static float CellWidth(string text) => UiNode.Measure(text, FontSize).X + CellSlack;

    private Entity Row(int row) => _table.Entity.ChildEntities[row + 1];

    private Entity AddRow()
    {
        UiNode row = _table.Row().Fill(Transparent).Main(StackMainAlignment.Start);
        for (int i = 0; i < _columns.Length; i++)
        {
            row.Text("", FontSize, DebugPalette.Text, _columns[i].Align)
                .Size(_widths[i], RowHeight)
                .With(new DebugCell { Source = "" });
        }
        return row.Entity;
    }

    private void Widen(int column, float width)
    {
        _widths[column] = width;
        foreach (Entity row in _table.Entity.ChildEntities)
        {
            Entity cell = row.ChildEntities[column];
            cell.GetComponent<Rectangle>().Bounds = new Rect2(
                -width / 2,
                -RowHeight / 2,
                width,
                RowHeight);
            cell.AddTag<Dirty>();
            row.AddTag<LayoutDirty>();
        }
    }

    private static void SetColor(Entity cell, Color color)
    {
        ref TextStyle style = ref cell.GetComponent<TextStyle>();
        if (style.Color == color)
            return;

        style.Color = color;
        cell.AddTag<Dirty>();
    }
}
