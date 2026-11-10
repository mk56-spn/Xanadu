using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Debug;

/// <summary>
/// The figures above the system tree, as labelled tiles in three titled
/// sections rather than run-on lines. Tiles keep their width, so a number never
/// shoves its neighbours, and they refresh with the live column, every 0.1 s.
/// </summary>
public sealed partial class DebugPressedOverlaySystem
{
    private const float TileWidth = 104;
    private const float CaptionHeight = UiType.Caption + UiMetrics.Space.Xxs;

    private enum Stat
    {
        Fps,
        FrameMs,
        SystemsMs,
        AllocKb,
        Entities,
        Capacity,
        Archetypes,
        ArchetypesInUse,
        Containers,
        Dirty,
        Moved,
        Input,
        Pressed,
        LayoutDirty,
        GpuProcess,
        GpuBudget,
        GpuCard,
        Count,
    }

    private static readonly (string Title, (Stat Stat, string Label)[] Tiles)[] Sections =
    [
        ("FRAME", [(Stat.Fps, "fps"), (Stat.FrameMs, "frame ms"), (Stat.SystemsMs, "systems ms"), (Stat.AllocKb, "alloc KB / frame")]),
        ("ENTITIES", [(Stat.Entities, "entities"), (Stat.Capacity, "capacity"), (Stat.Archetypes, "archetypes"), (Stat.ArchetypesInUse, "in use"), (Stat.Containers, "containers")]),
        ("UI THIS FRAME", [(Stat.Dirty, "dirty"), (Stat.Moved, "moved"), (Stat.Input, "take input"), (Stat.Pressed, "pressed"), (Stat.LayoutDirty, "layout dirty")]),
        ("GPU MEMORY", [(Stat.GpuProcess, "this app MB"), (Stat.GpuBudget, "budget MB"), (Stat.GpuCard, "card MB")]),
    ];

    private readonly Entity[] _stats = new Entity[(int)Stat.Count];
    private float _summaryClock;
    private long _allocSum;
    private int _allocFrames;

    private void BuildSummary(UiNode panel)
    {
        foreach ((string title, (Stat Stat, string Label)[] tiles) in Sections)
        {
            UiNode section = panel.Column().Gap(UiMetrics.Space.Xxs).Cross(StackCrossAlignment.Start);
            section.Text(title, UiType.Caption, DebugPalette.Header, TextAlign.Left)
                .Size(TileWidth * tiles.Length, CaptionHeight);
            UiNode row = section.Row().Gap(UiMetrics.Space.Sm).Main(StackMainAlignment.Start);
            foreach ((Stat stat, string label) in tiles)
            {
                // At least the tile width, so numbers line up; wider when the label needs it.
                float width = Mathf.Max(TileWidth, UiNode.Measure(label, UiType.Caption).X + UiMetrics.Space.Sm);
                UiNode tile = row.Column().Cross(StackCrossAlignment.Start);
                tile.Text(label, UiType.Caption, DebugPalette.TextDim, TextAlign.Left)
                    .Size(width, CaptionHeight);
                _stats[(int)stat] = tile.Text("-", FontSize, DebugPalette.Text, TextAlign.Left)
                    .Size(width, DebugTable.RowHeight)
                    .Entity;
            }
        }

        _summaryClock = LiveWindow;
        _allocSum = 0;
        _allocFrames = 0;
    }

    /// <summary>Gathers this frame's allocations; the tiles show the mean per frame when the window closes.</summary>
    private void RefreshSummary(float delta, double fps, float systemsMs)
    {
        if (_systems is not null)
        {
            foreach (BaseSystem system in _systems.ChildSystems)
                _allocSum += system.Perf.LastMemory;
        }
        _allocFrames++;

        _summaryClock += delta;
        if (_summaryClock < LiveWindow)
            return;
        _summaryClock = 0;

        Set(Stat.Fps, fps.ToString("0", CultureInfo.InvariantCulture));
        Set(Stat.FrameMs, (1000f / Mathf.Max(1f, (float)fps)).ToString("0.00", CultureInfo.InvariantCulture));
        Set(Stat.SystemsMs, systemsMs.ToString("0.00", CultureInfo.InvariantCulture));
        Set(Stat.AllocKb, (_allocSum / 1024f / Mathf.Max(1, _allocFrames)).ToString("0.0", CultureInfo.InvariantCulture));
        _allocSum = 0;
        _allocFrames = 0;

        Set(Stat.Entities, Count(_store.Count - _debug.Count));
        Set(Stat.Capacity, Count(_store.Capacity));
        Set(Stat.Archetypes, Count(_archetypes.Count));
        Set(Stat.ArchetypesInUse, Count(_archetypes.ActiveCount));
        Set(Stat.Containers, Count(_containers.Count));
        Set(Stat.Dirty, Count(_dirty.Count));
        Set(Stat.Moved, Count(_moved.Count));
        Set(Stat.Input, Count(_input.Count));
        Set(Stat.Pressed, Count(_pressed.Count));
        Set(Stat.LayoutDirty, Count(_layoutDirty.Count));

        // From the driver: what this process holds in video memory, what it may hold, and the card's size.
        GpuMemoryReading? gpu = _gpuMemory?.Read();
        Set(Stat.GpuProcess, gpu is { } a ? Megabytes(a.ProcessBytes) : "n/a");
        Set(Stat.GpuBudget, gpu is { } b ? Megabytes(b.BudgetBytes) : "n/a");
        Set(Stat.GpuCard, gpu is { } c ? Megabytes(c.TotalBytes) : "n/a");
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0", CultureInfo.InvariantCulture);

    private void Set(Stat stat, string text) => SetLine(_stats[(int)stat], text);
}
