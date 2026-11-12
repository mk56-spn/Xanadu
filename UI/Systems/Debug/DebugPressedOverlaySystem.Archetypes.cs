using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Debug;

/// <summary>The archetype table: live counts, sampled deltas, reserve, and selection.</summary>
public sealed partial class DebugPressedOverlaySystem
{
    private const int ArchetypeSampleSize = 4;
    private const float ComponentsWidth = 208;
    private const float TagsWidth = 166;
    private const float DetailWidth = 620;

    /// <summary>Where END writes the archetype list: beside the project, so it is easy to open.</summary>
    private const string DumpPath = "archetypes.txt";

    private static readonly Color Unselected = new(0, 0, 0, 0);

    private bool _dumpHeld;

    private void BuildArchetypePanel(UiNode panel)
    {
        _archetypeTitle = panel.Text("", FontSize, DebugPalette.Title).Entity;
        _archetypeTable = new DebugTable(
            panel,
            new DebugColumn("#", TextAlign.Right),
            new DebugColumn("n", TextAlign.Right),
            new DebugColumn("Δ", TextAlign.Right),
            new DebugColumn("cap", TextAlign.Right),
            new DebugColumn("use", TextAlign.Right),
            new DebugColumn("KB", TextAlign.Right),
            new DebugColumn("components", TextAlign.Left, ComponentsWidth),
            new DebugColumn("tags", TextAlign.Left, TagsWidth));
        _pageLine = panel.Text("", FontSize, DebugPalette.Header).Entity;
        _detail = panel.Column().Cross(StackCrossAlignment.Start);
    }

    private void RefreshArchetypes()
    {
        HandleArchetypeInput();

        IReadOnlyList<ArchetypeRow> rows = _archetypes.Rows;
        ArchetypeRow? selected = _archetypes.Selected;
        int pageCount = _archetypes.PageCount;
        int start = _archetypes.Page * _archetypes.PageSize;
        int visible = Math.Clamp(rows.Count - start, 0, _archetypes.PageSize);

        SetLine(
            _archetypeTitle,
            $"ARCHETYPES {_archetypes.Count}   ACTIVE {_archetypes.ActiveCount}   INACTIVE {_archetypes.InactiveCount}");
        _archetypeTable.SetRowCount(visible);
        for (int i = 0; i < visible; i++)
        {
            ArchetypeRow row = rows[start + i];
            _archetypeTable.SetRowFill(i, ReferenceEquals(row, selected) ? DebugPalette.Selection : Unselected);
            _archetypeTable.Set(i, 0, row.Order.ToString(CultureInfo.InvariantCulture));
            _archetypeTable.Set(i, 1, row.Count.ToString(CultureInfo.InvariantCulture));
            _archetypeTable.Set(i, 2, FormatDelta(row.Delta));
            _archetypeTable.SetColor(i, 2, row.Growth >= 3 ? DebugPalette.Warning : DebugPalette.Text);
            _archetypeTable.Set(i, 3, row.Capacity.ToString(CultureInfo.InvariantCulture));
            _archetypeTable.Set(i, 4, $"{row.Used * 100:0}%");
            _archetypeTable.Set(i, 5, (row.ReservedBytes / 1024f).ToString("0.0", CultureInfo.InvariantCulture));
            _archetypeTable.Set(i, 6, row.Components);
            _archetypeTable.Set(i, 7, row.Tags);
        }

        string hint = $"HOME SORT   END DUMP → {DumpPath}";
        SetLine(_pageLine, pageCount > 1
            ? $"PAGE {_archetypes.Page + 1}/{pageCount}   PGUP/PGDN  UP/DOWN  {hint}"
            : hint);

        RefreshDetail(selected);
    }

    /// <summary>
    /// The selected row's signature in full: the table columns cut it short, so it
    /// wraps across as many lines as it needs. The last line samples its entities.
    /// </summary>
    private void RefreshDetail(ArchetypeRow? selected)
    {
        UiLayout.SetHidden(_detail, selected is null);
        if (selected is null)
        {
            _detailFor = null;
            return;
        }

        if (!ReferenceEquals(selected, _detailFor))
        {
            _detailFor = selected;
            UiLayout.DeleteChildren(_detail);
            AddWrapped("COMPONENTS " + selected.Components);
            AddWrapped("TAGS " + selected.Tags);
            _detail.Text("", FontSize, DebugPalette.Text);
        }

        _archetypes.FillEntitySample(_sampleIds, ArchetypeSampleSize);
        SetLine(
            _detail.Entity.ChildEntities[_detail.Entity.ChildCount - 1],
            $"ENTITIES {string.Join(", ", _sampleIds)}");
    }

    private void AddWrapped(string text)
    {
        foreach (string line in UiTextFit.Wrap(text, FontSize, DetailWidth))
            _detail.Text(line, FontSize, DebugPalette.Text);
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
        bool dump = _paging.DumpPressed();

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
        if (dump && !_dumpHeld)
            WriteDump();

        _pageUpHeld = pageUp;
        _pageDownHeld = pageDown;
        _selectUpHeld = selectUp;
        _selectDownHeld = selectDown;
        _sortHeld = sort;
        _dumpHeld = dump;
    }

    /// <summary>Writes every archetype, active or not, to <see cref="DumpPath"/> for reading outside the overlay.</summary>
    private void WriteDump()
    {
        string path = Xanadu.Storage.GamePaths.Full(DumpPath);
        try
        {
            _archetypes.Dump(path);
            Xanadu.Diagnostics.GameLog.Info($"Archetypes written to {path}");
        }
        catch (Exception exception)
        {
            Xanadu.Diagnostics.GameLog.Warning($"Could not write {path}: {exception.Message}");
        }
    }

    private static string FormatDelta(int delta) => delta switch
    {
        > 0 => "+" + delta.ToString(CultureInfo.InvariantCulture),
        < 0 => delta.ToString(CultureInfo.InvariantCulture),
        _ => "0",
    };
}
