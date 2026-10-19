using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems;

namespace Writer.Ecs;

/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;

    public WriterSystemRoot(
        EntityStore entities,
        Rid parentCanvas,
        IPointerInputSource pointerInput) : base(entities, "Writer")
    {
        var canvas = new GodotRectCanvas();

        _debugOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_debugOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_debugOverlay, 1000);
        RenderingServer.CanvasItemSetVisible(_debugOverlay, false);

        Add(new LayerParentSystem(parentCanvas));
        Add(new DrawableInitialiseSystem());
        Add(new RectDrawSystem(canvas));
        Add(new TextDrawSystem(canvas));
        Add(new InputSystem(pointerInput));
        Add(new DebugPressedOverlaySystem(canvas, _debugOverlay, new GodotOverlayToggleSource()));
    }

    public void Dispose() => RenderingServer.FreeRid(_debugOverlay);
}
