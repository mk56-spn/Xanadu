using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Systems.Debug;

/// <summary>The archetype table: live counts, sampled deltas, reserve, and selection.</summary>
public sealed partial class DebugPressedOverlaySystem
{
    private void DrawArchetypePanel(EntityStore store)
    {
        _archetypes.Refresh(store, _elapsedMs);
        HandleArchetypeInput();

        IReadOnlyList<ArchetypeRow> rows = _archetypes.Rows;
        ArchetypeRow? selected = _archetypes.Selected;
        int pageCount = _archetypes.PageCount;
        int start = _archetypes.Page * _archetypes.PageSize;
        int visible = Math.Clamp(rows.Count - start, 0, _archetypes.PageSize);

        int detailLines = selected is null
            ? 0
            : WrappedLineCount("COMPONENTS", selected.Components, ArchetypeWidth - 20)
              + WrappedLineCount("TAGS", selected.Tags, ArchetypeWidth - 20)
              + 1;
        float height = 58 + visible * LineHeight
            + (pageCount > 1 ? LineHeight : 0)
            + detailLines * LineHeight;

        var panel = new Rect2(16 + PanelWidth + 12, 16, ArchetypeWidth, height);
        _canvas.DrawRect(_overlay, panel, PanelBackground);
        DrawBorder(panel, PanelBorder, 1);

        float x = panel.Position.X + 10;
        float y = panel.Position.Y + 8;
        float contentWidth = ArchetypeWidth - 20;

        DrawPanelLine($"ARCHETYPES {_archetypes.Count}", x, y, TitleColor, contentWidth);
        y += LineHeight;

        const float indexW = 26;
        const float countW = 44;
        const float deltaW = 40;
        const float capacityW = 46;
        const float usedW = 36;
        const float sizeW = 54;
        const float componentsW = 208;
        const float tagsW = 166;

        float indexX = x;
        float countX = indexX + indexW;
        float deltaX = countX + countW;
        float capacityX = deltaX + deltaW;
        float usedX = capacityX + capacityW;
        float sizeX = usedX + usedW;
        float componentsX = sizeX + sizeW;
        float tagsX = componentsX + componentsW;

        DrawPanelRight("#", indexX, y, indexW, HeaderColor);
        DrawPanelRight("n", countX, y, countW, HeaderColor);
        DrawPanelRight("Δ", deltaX, y, deltaW, HeaderColor);
        DrawPanelRight("cap", capacityX, y, capacityW, HeaderColor);
        DrawPanelRight("use", usedX, y, usedW, HeaderColor);
        DrawPanelRight("KB", sizeX, y, sizeW, HeaderColor);
        DrawPanelLeft("components", componentsX, y, componentsW, HeaderColor);
        DrawPanelLeft("tags", tagsX, y, tagsW, HeaderColor);
        y += LineHeight;

        for (int i = start; i < start + visible; i++)
        {
            ArchetypeRow row = rows[i];
            if (ReferenceEquals(row, selected))
            {
                _canvas.DrawRect(
                    _overlay,
                    new Rect2(x - 4, y - 1, contentWidth + 4, LineHeight),
                    SelectionColor);
            }

            DrawPanelRight(
                row.Order.ToString(CultureInfo.InvariantCulture),
                indexX, y, indexW, TextColor);
            DrawPanelRight(
                row.Count.ToString(CultureInfo.InvariantCulture),
                countX, y, countW, TextColor);
            DrawPanelRight(
                FormatDelta(row.Delta),
                deltaX, y, deltaW,
                row.Growth >= 3 ? GrowthColor : TextColor);
            DrawPanelRight(
                row.Capacity.ToString(CultureInfo.InvariantCulture),
                capacityX, y, capacityW, TextColor);
            DrawPanelRight(
                $"{row.Used * 100:0}%",
                usedX, y, usedW, TextColor);
            DrawPanelRight(
                (row.ReservedBytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture),
                sizeX, y, sizeW, TextColor);
            DrawPanelLeft(
                Fit(row.Components, componentsW),
                componentsX, y, componentsW, TextColor);
            DrawPanelLeft(
                Fit(row.Tags, tagsW),
                tagsX, y, tagsW, TextColor);
            y += LineHeight;
        }

        if (pageCount > 1)
        {
            DrawPanelLine(
                $"PAGE {_archetypes.Page + 1}/{pageCount}   PGUP/PGDN  UP/DOWN  HOME SORT",
                x, y, HeaderColor, contentWidth);
            y += LineHeight;
        }

        if (selected is null)
            return;

        // The selected row's signature in full: the table columns truncate it, so
        // wrap it across as many lines as the panel width needs.
        y = DrawWrapped("COMPONENTS", selected.Components, x, y, contentWidth);
        y = DrawWrapped("TAGS", selected.Tags, x, y, contentWidth);

        _archetypes.FillEntitySample(_sampleIds, ArchetypeSampleSize);
        DrawPanelLine(
            $"ENTITIES {string.Join(", ", _sampleIds)}",
            x, y, TextColor, contentWidth);
    }

    private void HandleArchetypeInput()
    {
        if (_paging is null)
            return;

        bool pageUp = _paging.PreviousPressed();
        bool pageDown = _paging.NextPressed();
        bool selectUp = _paging.SelectionUpPressed();
        bool selectDown = _paging.SelectionDownPressed();
        bool sort = _paging.SortTogglePressed();

        if (pageUp && !_pageUpHeld)
            _archetypes.MovePage(-1);
        if (pageDown && !_pageDownHeld)
            _archetypes.MovePage(1);
        if (selectUp && !_selectUpHeld)
            _archetypes.MoveSelection(-1);
        if (selectDown && !_selectDownHeld)
            _archetypes.MoveSelection(1);
        if (sort && !_sortHeld)
            _archetypes.CycleSort();

        _pageUpHeld = pageUp;
        _pageDownHeld = pageDown;
        _selectUpHeld = selectUp;
        _selectDownHeld = selectDown;
        _sortHeld = sort;
    }

    private static int CharsPerLine(float width) =>
        Math.Max(1, (int)(width / GlyphWidth));

    private static int WrappedLineCount(string label, string text, float width)
    {
        int length = label.Length + 1 + text.Length;
        int perLine = CharsPerLine(width);
        return Math.Max(1, (length + perLine - 1) / perLine);
    }

    /// <summary>Draws <paramref name="label"/> and <paramref name="text"/> across as many lines as fit.</summary>
    private float DrawWrapped(
        string label,
        string text,
        float x,
        float y,
        float width)
    {
        string line = label + " " + text;
        int perLine = CharsPerLine(width);
        for (int offset = 0; offset < line.Length; offset += perLine)
        {
            DrawPanelLeft(
                line.Substring(offset, Math.Min(perLine, line.Length - offset)),
                x, y, width, TextColor);
            y += LineHeight;
        }

        return y;
    }

    /// <summary>Truncates to the characters that fit <paramref name="width"/>, not an arbitrary count.</summary>
    private static string Fit(string value, float width) =>
        Truncate(value, CharsPerLine(width));

    private static string FormatDelta(int delta) => delta switch
    {
        > 0 => "+" + delta.ToString(CultureInfo.InvariantCulture),
        < 0 => delta.ToString(CultureInfo.InvariantCulture),
        _ => "0",
    };

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;

        return max <= 3 ? value[..max] : value[..(max - 3)] + "...";
    }
}
