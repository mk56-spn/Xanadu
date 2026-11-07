using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Debug;

/// <summary>
/// The F3 systems as a tree control, the same one the F4 UI tree uses: every
/// group opens with its arrow, down to the exact system inside it. A group's
/// time includes everything in it. Times are averaged over a window, not read
/// per frame, so they hold still long enough to read: the live column over the
/// last 0.1 s, the average over the last second.
/// </summary>
public sealed partial class DebugPressedOverlaySystem
{
    public const float LiveWindow = 0.1f;
    public const float AverageWindow = 1f;

    /// <summary>An average at or above this, in milliseconds, is coloured as a warning.</summary>
    private const float SlowMs = 0.5f;

    private const float TreeWidth = 560;
    private const float TreeHeight = 560;
    private const float TreeRowHeight = DebugTable.RowHeight + UiMetrics.Space.Xxs;
    private static readonly float[] CellWidths = [80, 80, 70];
    private static readonly string[] CellTitles = ["live ms", "avg ms", "upd"];

    /// <summary>A system's totals at the start of each window, and what the window last showed.</summary>
    private sealed class SystemWindow
    {
        public double LiveSum;
        public int LiveCount;
        public double AverageSum;
        public int AverageCount;
        public float Live = float.NaN;
        public float Average = float.NaN;
        public readonly string[] Cells = ["-", "-", "0"];
    }

    private readonly Dictionary<BaseSystem, SystemWindow> _windows = new();
    private readonly Dictionary<string, BaseSystem> _byId = new();
    private readonly HashSet<string> _known = new();
    private readonly List<UiTreeItem> _systemItems = new();
    private Entity _systemTree;
    private float _liveClock;
    private float _averageClock;

    /// <summary>The column titles over the tree, then the tree in a scrolling viewport.</summary>
    private void BuildSystemTree(UiNode panel)
    {
        float labelWidth = TreeWidth - UiTheme.TreeIndent - TotalCellWidth();
        UiNode header = panel.Row().Main(StackMainAlignment.Start);
        header.Text("System", FontSize, DebugPalette.Header, TextAlign.Left).Size(labelWidth + UiTheme.TreeIndent, DebugTable.RowHeight);
        for (int i = 0; i < CellTitles.Length; i++)
            header.Text(CellTitles[i], FontSize, DebugPalette.Header, TextAlign.Right).Size(CellWidths[i], DebugTable.RowHeight);

        UiBuilder ui = UiBuilder.Begin(_store, _visuals).Use(panel.Entity);
        ui.Container(
            Vector2.Zero,
            new Vector2(TreeWidth, TreeHeight),
            StackDirection.Vertical,
            clip: true,
            color: new Color(0, 0, 0, 0),
            takesInput: true);
        Entity viewport = ui.Current;
        ui.Container(
            Vector2.Zero,
            new Vector2(TreeWidth, 4),
            StackDirection.Vertical,
            color: new Color(0, 0, 0, 0),
            cross: StackCrossAlignment.Start,
            autoHeight: true,
            main: StackMainAlignment.Start);
        _systemTree = ui.Tree(TreeWidth, TreeRowHeight, cellWidths: CellWidths);
        ui.End();
        viewport.AddComponent(new ScrollView());
        ui.End();

        _known.Clear();
        _windows.Clear();
        _liveClock = LiveWindow;
        _averageClock = AverageWindow;
    }

    private static float TotalCellWidth()
    {
        float total = 0;
        foreach (float width in CellWidths)
            total += width;
        return total;
    }

    /// <summary>Times every system in every group, not only the root's children, while F3 is open.</summary>
    private static void MonitorNested(SystemGroup group)
    {
        group.SetMonitorPerf(true);
        foreach (BaseSystem child in group.ChildSystems)
        {
            if (child is SystemGroup nested)
                MonitorNested(nested);
        }
    }

    /// <summary>
    /// Closes each window that has run its time and lists the systems again.
    /// Returns the root's live total, the sum of its top-level systems.
    /// </summary>
    private float RefreshSystems(SystemRoot root, float delta)
    {
        _liveClock += delta;
        _averageClock += delta;
        bool live = _liveClock >= LiveWindow;
        bool average = _averageClock >= AverageWindow;
        if (live)
            _liveClock = 0;
        if (average)
            _averageClock = 0;

        if (live || average || _systemItems.Count == 0)
        {
            _systemItems.Clear();
            _byId.Clear();
            Collect(root, null, live, average);
            UiBuilder.Begin(_store, _visuals).SetTreeItems(_systemTree, _systemItems);
            CloseNewGroups();
            Colour();
        }

        float total = 0;
        foreach (BaseSystem system in root.ChildSystems)
        {
            if (_windows.TryGetValue(system, out SystemWindow? window) && !float.IsNaN(window.Live))
                total += window.Live;
        }
        return total;
    }

    private void Collect(SystemGroup group, string? parent, bool live, bool average)
    {
        int index = 0;
        foreach (BaseSystem system in group.ChildSystems)
        {
            string id = parent is null ? index.ToString(CultureInfo.InvariantCulture) : $"{parent}.{index}";
            index++;
            SystemWindow window = Sample(system, live, average);
            _byId[id] = system;
            _systemItems.Add(new UiTreeItem(id, parent, Name(system), window.Cells));
            if (system is SystemGroup nested)
                Collect(nested, id, live, average);
        }
    }

    /// <summary>The mean time per run over each window that just closed; a window with no runs shows a dash.</summary>
    private SystemWindow Sample(BaseSystem system, bool live, bool average)
    {
        SystemPerf perf = system.Perf;
        if (!_windows.TryGetValue(system, out SystemWindow? window))
        {
            window = new SystemWindow
            {
                LiveSum = perf.SumMs, LiveCount = perf.UpdateCount,
                AverageSum = perf.SumMs, AverageCount = perf.UpdateCount,
            };
            _windows[system] = window;
            return window;
        }

        if (live)
        {
            window.Live = Mean(perf.SumMs - window.LiveSum, perf.UpdateCount - window.LiveCount);
            (window.LiveSum, window.LiveCount) = (perf.SumMs, perf.UpdateCount);
            window.Cells[0] = Format(window.Live);
            window.Cells[2] = perf.UpdateCount.ToString(CultureInfo.InvariantCulture);
        }

        if (average)
        {
            window.Average = Mean(perf.SumMs - window.AverageSum, perf.UpdateCount - window.AverageCount);
            (window.AverageSum, window.AverageCount) = (perf.SumMs, perf.UpdateCount);
            window.Cells[1] = Format(window.Average);
        }

        return window;
    }

    private static float Mean(double sum, int runs) => runs > 0 ? (float)(sum / runs) : float.NaN;

    private static string Format(float ms) =>
        float.IsNaN(ms) ? "-" : ms.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Groups start closed, so the tree opens on the top level; one the user opened stays open.</summary>
    private void CloseNewGroups()
    {
        foreach ((string id, BaseSystem system) in _byId)
        {
            if (_known.Add(id) && system is SystemGroup group && group.ChildSystems.Count > 0)
                UiTreeControl.SetExpanded(_systemTree, id, false);
        }
    }

    /// <summary>Slow systems in the warning colour, groups in the header colour, the rest plain.</summary>
    private void Colour()
    {
        foreach (Entity node in UiTreeControl.Nodes(_systemTree))
        {
            if (!_byId.TryGetValue(node.GetComponent<UiTreeNode>().Id, out BaseSystem? system))
                continue;

            float average = _windows.TryGetValue(system, out SystemWindow? window) ? window.Average : float.NaN;
            Color color = average >= SlowMs
                ? DebugPalette.Warning
                : system is SystemGroup ? DebugPalette.Header : DebugPalette.Text;
            UiText.SetColor(UiTreeControl.Label(node), color);
            foreach (Entity cell in UiTreeControl.Cells(node))
                UiText.SetColor(cell, color);
        }
    }
}
