using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;

namespace Xanadu.Systems.Groups;

/// <summary>Draws the F3 pressed-entity overlay and ECS/system metrics.</summary>
public sealed class DebugSystemGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _overlay;
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
        _overlay = new CanvasItemHandle(parentCanvas, 1000, visible: false);

        Add(new DebugPressedOverlaySystem(
            canvas,
            _overlay.Rid,
            toggle,
            store,
            systems,
            fps,
            paging));
        _uiOverlay = Xanadu.Systems.Ui.UiDebugOverlayGroup.Attach(
            this,
            canvas,
            parentCanvas);
    }

    public void Dispose()
    {
        _uiOverlay.Dispose();
        _overlay.Dispose();
    }
}
