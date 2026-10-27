using Godot;
using Friflo.Engine.ECS.Systems;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>Creates and wires the F4 UI bounds overlay.</summary>
public sealed class UiDebugOverlayGroup : SystemGroup
{
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
        var overlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(overlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(overlay, zIndex);
        canvas.SetVisible(overlay, false);

        Add(new DebugElementOverlaySystem(
            canvas,
            overlay,
            toggle ?? new GodotOverlayToggleSource(Key.F4)));
    }
}
