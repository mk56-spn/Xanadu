using System;
using Godot;
using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>Creates and wires the F4 UI bounds and tree overlays.</summary>
public sealed class UiDebugOverlayGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _overlay;

    /// <summary>Boots the F4 overlay on any ECS UI host.</summary>
    public static UiDebugOverlayGroup Attach(
        SystemGroup root,
        IRectCanvas canvas,
        Rid parentCanvas,
        IOverlayToggleSource? toggle = null,
        int zIndex = 1001)
    {
        var group = new UiDebugOverlayGroup(
            canvas,
            parentCanvas,
            toggle,
            zIndex);
        root.Add(group);
        return group;
    }

    public UiDebugOverlayGroup(
        IRectCanvas canvas,
        Rid parentCanvas,
        IOverlayToggleSource? toggle = null,
        int zIndex = 1001) : base("UiDebugOverlay")
    {
        _overlay = new CanvasItemHandle(parentCanvas, zIndex, visible: false);
        toggle ??= new GodotOverlayToggleSource(DebugKeys.UiTree);

        Add(new DebugElementOverlaySystem(canvas, _overlay.Rid, toggle));
        Add(new UiTreeOverlaySystem(toggle));
    }

    public void Dispose() => _overlay.Dispose();
}
