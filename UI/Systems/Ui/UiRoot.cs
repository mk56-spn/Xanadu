using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// The UI's own root, owned by the host, not by the app. Each frame:
/// <list type="number">
/// <item>Input: pointer, focus, buttons, fields and text editors, hit-tested against the layout on screen.</item>
/// <item>App: the app's own root (<see cref="Run"/>), reacting to this frame's clicks.</item>
/// <item>Layout: what the UI and the app made or moved this frame is laid out.</item>
/// <item>Draw.</item>
/// <item>Overlays: F1 frame rate, F3 ECS and systems, F4 UI tree, last so they see everything.</item>
/// </list>
/// The app's root runs on the same store; its systems are its own business.
/// </summary>
public sealed class UiRoot : SystemRoot, IDisposable
{
    private readonly AppStep _app = new();
    private readonly DebugSystemGroup? _overlays;
    private readonly VisualRidRelease _visuals;

    public UiRoot(
        EntityStore entities,
        Handle parentCanvas,
        IPointerInputSource pointer,
        Vector2 screen,
        IKeyboardState? keyboard = null,
        IWheelSource? wheel = null,
        IVisualRidFactory? visuals = null,
        bool tooltips = true,
        bool overlays = true) : base(entities, "Ui")
    {
        // Deleted UI (F1 and F4 rebuild theirs each time they open) has to take its canvas items with it.
        _visuals = new VisualRidRelease(entities);
        Canvas = HostPlatform.NewCanvas();

        var input = new SystemGroup("UiInput");
        input.Add(new UiInteractionSystemGroup(
            entities,
            pointer,
            new ScrollSystem(pointer, wheel),
            keyboard,
            visuals,
            screen,
            tooltips: tooltips));
        input.Add(new TextEditorInputSystem(pointer, keyboard ?? HostPlatform.Keyboard, wheel));
        Add(input);

        Add(_app);
        Add(new InitializationSystemGroup(parentCanvas));

        var draw = new SystemGroup("UiDraw");
        draw.Add(new PresentationSystemGroup(Canvas));
        draw.Add(new TextEditorDrawSystem(Canvas));
        Add(draw);

        if (overlays)
        {
            _overlays = new DebugSystemGroup(
                HostPlatform.NewCanvas(),
                parentCanvas,
                new KeyOverlayToggleSource(DebugKeys.Ecs),
                entities,
                this,
                HostPlatform.Fps,
                new KeyOverlayPageSource());
            Add(_overlays);
        }
    }

    /// <summary>The canvas the UI draws with, for app systems that draw alongside it.</summary>
    public IRectCanvas Canvas { get; }

    /// <summary>The app's root, stepped between UI input and layout; null runs the UI alone.</summary>
    public SystemRoot? App => _app.Root;

    /// <summary>Hands the app's root to the UI's frame. The app keeps owning it.</summary>
    public void Run(SystemRoot? app) => _app.Root = app;

    public void Dispose()
    {
        _overlays?.Dispose();
        _visuals.Dispose();
    }
}

/// <summary>Steps an app's own root from inside the UI's frame, so the two run in one order.</summary>
public sealed class AppStep : BaseSystem
{
    public SystemRoot? Root { get; set; }

    public override string Name => Root?.Name ?? "App";

    protected override void OnUpdateGroup() => Root?.Update(Tick);

    /// <summary>A system's children for tools that walk the tree (F3, the autopsy): a group's, or the root an <see cref="AppStep"/> runs.</summary>
    public static System.Collections.Generic.IEnumerable<BaseSystem> Children(BaseSystem system) => system switch
    {
        AppStep { Root: { } root } => root.ChildSystems,
        SystemGroup group => group.ChildSystems,
        _ => [],
    };

    /// <summary>Turns per-system timing on for a group, everything under it, and any app root it steps.</summary>
    public static void MonitorAll(BaseSystem system)
    {
        if (system is SystemGroup group)
            group.SetMonitorPerf(true);
        if (system is AppStep { Root: { } root })
            root.SetMonitorPerf(true);
        foreach (BaseSystem child in Children(system))
            MonitorAll(child);
    }
}
