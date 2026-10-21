using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Editor;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Groups;
using Writer.Systems.Input;
using Writer.Systems.Physics;

namespace Writer.Ecs;

/// <summary>
/// Owns Godot resources and composes the application's phase groups.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;
    private readonly Rid _timeline;
    private readonly Rid _editor;
    private readonly MiniaudioTrackAudio _trackAudio = new();
    private readonly MiniaudioTrackAudio _editorAudio = new();

    public Box2DPhysicsWorld Physics { get; } = new();

    public EditorHistory EditorHistory { get; }

    public WriterSystemRoot(
        EntityStore entities,
        Rid parentCanvas,
        IPointerInputSource pointerInput) : base(entities, "Writer")
    {
        var canvas = new GodotRectCanvas();
        EditorHistory = new EditorHistory(entities);

        _debugOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_debugOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_debugOverlay, 1000);
        RenderingServer.CanvasItemSetVisible(_debugOverlay, false);

        _timeline = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_timeline, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_timeline, 900);

        _editor = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_editor, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_editor, 800);

        Add(new InitializationSystemGroup(parentCanvas));
        Add(new PhysicsSystemGroup(Physics));
        Add(new PresentationSystemGroup(canvas));
        Add(new AudioSystemGroup(_trackAudio, new GodotTrackClock(), canvas, _timeline));
        Add(new EditorSystemGroup(
            entities,
            pointerInput,
            canvas,
            _editor,
            _editorAudio,
            EditorHistory));
        Add(new InputSystemGroup(pointerInput));
        Add(new DebugSystemGroup(
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
        _editorAudio.Dispose();
        Physics.Dispose();
        RenderingServer.FreeRid(_editor);
        RenderingServer.FreeRid(_timeline);
        RenderingServer.FreeRid(_debugOverlay);
    }
}
