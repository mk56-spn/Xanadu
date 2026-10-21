using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Input;
using Writer.Systems.Layout;
using Writer.Systems.Rendering;

namespace Writer.Ecs;

/// <summary>
/// Central system registration point. Main owns only Godot lifecycle;
/// systems are added here instead of polluting Main.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;
    private readonly Rid _timeline;
    private readonly MiniaudioTrackAudio _trackAudio = new();

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

        _timeline = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_timeline, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_timeline, 900);

        Add(new LayerParentSystem(parentCanvas));
        Add(new ContainerLayoutSystem());
        Add(new DrawableInitialiseSystem());
        Add(new TransformSystem(canvas));
        Add(new RectDrawSystem(canvas));
        Add(new TextDrawSystem(canvas));
        Add(new TrackPlaybackSystem(_trackAudio, new GodotTrackClock()));
        Add(new TimelineDrawSystem(canvas, _timeline));
        Add(new InputSystem(pointerInput));
        Add(new DebugPressedOverlaySystem(
            canvas,
            _debugOverlay,
            new GodotOverlayToggleSource(),
            entities,
            this,
            new GodotFpsSource()));

        SetMonitorPerf(true);
    }

    public void Dispose()
    {
        _trackAudio.Dispose();
        RenderingServer.FreeRid(_timeline);
        RenderingServer.FreeRid(_debugOverlay);
    }
}
