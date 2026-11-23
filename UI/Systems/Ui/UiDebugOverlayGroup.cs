using Friflo.Engine.ECS.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Creates and wires the F4 UI bounds and tree overlays.</summary>
public sealed class UiDebugOverlayGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _overlay;

    /// <summary>Boots the F4 overlay on any ECS UI host.</summary>
    public static UiDebugOverlayGroup Attach(
        SystemGroup root,
        IRectCanvas canvas,
        Handle parentCanvas,
        IOverlayToggleSource? toggle = null,
        int zIndex = 1001,
        IPointerInputSource? pointer = null)
    {
        var group = new UiDebugOverlayGroup(
            canvas,
            parentCanvas,
            toggle,
            zIndex,
            pointer);
        root.Add(group);
        return group;
    }

    public UiDebugOverlayGroup(
        IRectCanvas canvas,
        Handle parentCanvas,
        IOverlayToggleSource? toggle = null,
        int zIndex = 1001,
        IPointerInputSource? pointer = null) : base("UiDebugOverlay")
    {
        _overlay = new CanvasItemHandle(parentCanvas, zIndex, visible: false);
        toggle ??= new KeyOverlayToggleSource(DebugKeys.UiTree);

        Add(new DebugElementOverlaySystem(canvas, _overlay.Handle, toggle));
        Add(new UiTreeOverlaySystem(toggle, pointer: pointer));
    }

    public void Dispose() => _overlay.Dispose();
}
