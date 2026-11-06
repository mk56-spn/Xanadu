using System;
using System.Collections.Generic;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Debug;

/// <summary>
/// The F3 panels: ECS and system metrics beside the archetype table. They are
/// ordinary ECS UI, tagged <see cref="DebugUi"/> so that what they report leaves them out.
/// </summary>
public sealed partial class DebugPressedOverlaySystem : QuerySystem<Rectangle, Position>
{
    /// <summary>Above the F4 boxes, so they do not tint the panels.</summary>
    private const int FontSize = UiType.Body;
    private const int NameLength = 32;

    private static readonly Vector2 TopLeft = new(UiMetrics.Space.Lg, UiMetrics.Space.Lg);

    private readonly EntityStore _store;
    private readonly SystemRoot? _systems;
    private readonly IFpsSource? _fpsSource;
    private readonly IOverlayPageSource? _paging;
    private readonly IVisualRidFactory? _visuals;
    private readonly OverlayLatch _latch;
    private readonly Dictionary<BaseSystem, string> _names = new();
    private readonly ArchetypeQuery _debug;
    private readonly ArchetypeQuery _dirty;
    private readonly ArchetypeQuery _moved;
    private readonly ArchetypeQuery _input;
    private readonly ArchetypeQuery _pressed;
    private readonly ArchetypeQuery _containers;
    private readonly ArchetypeQuery _layoutDirty;

    private readonly ArchetypeTable _archetypes = new();
    private readonly List<int> _sampleIds = new();
    private long _elapsedMs;
    private bool _pageUpHeld;
    private bool _pageDownHeld;
    private bool _selectUpHeld;
    private bool _selectDownHeld;
    private bool _sortHeld;

    private Entity _layer;

    /// <summary>A closed layer, hidden until the next update deletes it.</summary>
    private Entity _closing;
    private DebugTable _archetypeTable = null!;
    private Entity _archetypeTitle;
    private Entity _pageLine;
    private UiNode _detail;
    private ArchetypeRow? _detailFor;

    public DebugPressedOverlaySystem(
        IOverlayToggleSource toggle,
        EntityStore store,
        SystemRoot? systems = null,
        IFpsSource? fpsSource = null,
        IOverlayPageSource? paging = null,
        IVisualRidFactory? visuals = null)
    {
        _latch = new OverlayLatch(toggle);
        _store = store;
        _systems = systems;
        _fpsSource = fpsSource;
        _paging = paging;
        _visuals = visuals;

        Tags debug = Tags.Get<DebugUi>();
        _debug = store.Query().WithDisabled().AllTags(debug);
        _dirty = store.Query<Rectangle>().AllTags(Tags.Get<Dirty>()).WithoutAnyTags(debug);
        _moved = store.Query<VisualRid>().AllTags(Tags.Get<Moved>()).WithoutAnyTags(debug);
        _input = store.Query<Rectangle>().AllTags(Tags.Get<TakesInput>()).WithoutAnyTags(debug);
        _pressed = store.Query<Rectangle>().AllTags(Tags.Get<Pressed>()).WithoutAnyTags(debug);
        _containers = store.Query<UiContainer>().WithoutAnyTags(debug);
        _layoutDirty = store.Query<UiContainer>().AllTags(Tags.Get<LayoutDirty>()).WithoutAnyTags(debug);
    }

    protected override void OnUpdate()
    {
        DeleteClosed();
        _latch.Update();
        if (!_latch.Visible)
        {
            Close();
            return;
        }

        if (_layer.IsNull)
            Open();

        _elapsedMs += (long)(Tick.deltaTime * 1000);
        _archetypes.Refresh(_store, _elapsedMs);
        RefreshMetrics(Tick.deltaTime);
        RefreshArchetypes();
    }

    private void Open()
    {
        UiNode layer = UiNode.Layer(_store, default, UiLayers.DebugPressed, _visuals, UiStage.Full).Tag<DebugUi>();
        _layer = layer.Entity;

        // Pinned by its top left corner, so it grows right and down with its contents.
        UiNode panels = layer.Row()
            .At(TopLeft)
            .AnchorTop()
            .AnchorLeft()
            .Gap(UiMetrics.Space.Md)
            .Cross(StackCrossAlignment.Start);

        UiNode metrics = Panel(panels);
        metrics.Text("F3 ECS DEBUG", FontSize, DebugPalette.Title);
        BuildSummary(metrics);

        BuildSystemTree(metrics);
        if (_systems is not null)
            MonitorNested(_systems);

        BuildArchetypePanel(Panel(panels));
    }

    /// <summary>
    /// Hides the panels now and deletes them next update. Systems earlier this
    /// frame, such as layout, may have queued changes to them; hidden, they are
    /// out of every query, so by the next update nothing is queued for them.
    /// </summary>
    private void Close()
    {
        if (_layer.IsNull)
            return;

        UiLayout.SetHidden(_layer, true);
        _closing = _layer;
        _layer = default;
        _detailFor = null;
    }

    private void DeleteClosed()
    {
        if (_closing.IsNull)
            return;

        UiLayout.DeleteSubtree(_closing);
        _closing = default;
    }

    private static UiNode Panel(UiNode parent) => parent.Column()
        .Fill(DebugPalette.Panel)
        .Border(DebugPalette.PanelBorder)
        .Pad(UiMetrics.Space.Sm)
        .Gap(UiMetrics.Space.Sm)
        .Cross(StackCrossAlignment.Start);

    private void RefreshMetrics(float delta)
    {
        double fps = _fpsSource?.GetFps() ?? 60;
        float systemsMs = _systems is null ? 0 : RefreshSystems(_systems, delta);
        RefreshSummary(delta, fps, systemsMs);
    }

    /// <summary>Sets a line of text, widening it if it has outgrown its box.</summary>
    private static void SetLine(Entity line, string text)
    {
        if (line.GetComponent<Text>().Value == text)
            return;

        UiText.Set(line, text);
        Vector2 size = UiNode.Measure(text, FontSize);
        if (size.X <= line.GetComponent<Rectangle>().Bounds.Size.X)
            return;

        line.GetComponent<Rectangle>().Bounds = new Rect2(-size / 2, size);
        line.Parent.AddTag<LayoutDirty>();
    }

    private string Name(BaseSystem system)
    {
        if (_names.TryGetValue(system, out string? name))
            return name;

        name = system.Name.EndsWith("System", StringComparison.Ordinal)
            ? system.Name[..^6]
            : system.Name;
        if (name.Length > NameLength)
            name = name[..NameLength];
        _names[system] = name;
        return name;
    }
}
