using Godot;
using Friflo.Engine.ECS.Systems;
using Writer.Systems.Canvas;
using Writer.Systems.Input;

namespace Writer.Systems.Ui;

/// <summary>Creates and wires the F4 UI bounds overlay.</summary>
public sealed class UiDebugOverlayGroup : SystemGroup
{
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
