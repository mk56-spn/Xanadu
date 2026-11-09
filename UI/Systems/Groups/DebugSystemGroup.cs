using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;

namespace Xanadu.Systems.Groups;

/// <summary>Runs the F1 frame rate, the F3 ECS and system metrics and the F4 UI overlays.</summary>
public sealed class DebugSystemGroup : SystemGroup, IDisposable
{
    private readonly UiDebugOverlayGroup _uiOverlay;

    public DebugSystemGroup(
        IRectCanvas canvas,
        Rid parentCanvas,
        IOverlayToggleSource toggle,
        EntityStore store,
        SystemRoot systems,
        IFpsSource fps,
        IOverlayPageSource? paging = null) : base("Debug")
    {
        Add(new DebugPressedOverlaySystem(
            toggle,
            store,
            systems,
            fps,
            paging));
        Add(new FpsCounterSystem(new KeyOverlayToggleSource(DebugKeys.FrameRate), store, fps));
        _uiOverlay = Xanadu.Systems.Ui.UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas);
    }

    public void Dispose()
    {
        _uiOverlay.Dispose();
    }
}
