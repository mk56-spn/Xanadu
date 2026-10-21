using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Editor;
using Writer.Editor.Commands;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Groups;
using Writer.Systems.Input;
using Writer.Systems.Physics;
using Writer.Settings;
using Writer.Systems.Scenes;

namespace Writer.Ecs;

/// <summary>
/// Owns Godot resources and composes the application's phase groups.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;
    private readonly SceneDirector _sceneDirector;
    private EditorSystemGroup _editorSystems = null!;
    private readonly Rid _timeline;
    private readonly Rid _playfield;
    private readonly EntityStore _entities;
    private readonly MiniaudioTrackAudio _trackAudio = new();
    private readonly MiniaudioTrackAudio _playfieldAudio = new();
    private readonly MiniaudioTrackAudio _metronomeAudio = new();

    public Box2DPhysicsWorld Physics { get; } = new();

    public EditorHistory EditorHistory { get; }

    public EditorCommandBus Commands { get; }

    public SceneSystem Scenes { get; }

    public KeybindSettings Keybinds { get; }

    public WriterSystemRoot(
        EntityStore entities,
        Rid parentCanvas,
        IPointerInputSource pointerInput,
        SceneDirector sceneDirector,
        Vector2 viewportSize,
        KeybindSettings? keybinds = null,
        bool transitions = true) : base(entities, "Writer")
    {
        var canvas = new GodotRectCanvas();
        _entities = entities;
        entities.OnEntityDelete += OnEntityDelete;
        EditorHistory = new EditorHistory(entities);
        Commands = new EditorCommandBus();
        Keybinds = keybinds ?? new KeybindSettings();

        // Register the keybind component archetype before the first runtime
        // Settings build; first-time component registration during scene
        // construction leaves the new layer's structural tags stuck.
        Entity keybindWarmup = entities.CreateEntity(
            new KeybindButton(KeybindAction.L1));
        keybindWarmup.DeleteEntity();

        Scenes = new SceneSystem(
            entities,
            sceneDirector,
            viewportSize,
            keybinds: Keybinds,
            transitions: transitions);
        _sceneDirector = sceneDirector;
        sceneDirector.Changed += OnSceneChanged;

        _debugOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_debugOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_debugOverlay, 1000);
        RenderingServer.CanvasItemSetVisible(_debugOverlay, false);

        _timeline = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_timeline, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_timeline, 900);

        _playfield = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_playfield, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_playfield, 800);

        Add(new InitializationSystemGroup(parentCanvas));
        Add(new PhysicsSystemGroup(Physics));
        Add(new PresentationSystemGroup(canvas));
        Add(new AudioSystemGroup(_trackAudio, new GodotTrackClock(), canvas, _timeline));
        Add(new PlayfieldSystemGroup(
            entities,
            canvas,
            _playfield,
            _playfieldAudio,
            pointerInput,
            Keybinds,
            new GodotKeyboardState()));
        _editorSystems = new EditorSystemGroup(
            entities,
            Commands,
            pointerInput,
            canvas,
            _playfield,
            _metronomeAudio,
            EditorHistory);
        _editorSystems.Enabled = false;
        Add(_editorSystems);
        Add(new InputSystemGroup(entities, pointerInput, Scenes, Keybinds));
        Add(new DebugSystemGroup(
            canvas,
            _debugOverlay,
            new GodotOverlayToggleSource(),
            entities,
            this,
            new GodotFpsSource()));

        SetMonitorPerf(true);
    }

    private void OnSceneChanged(SceneKind scene) =>
        _editorSystems.Enabled = scene == SceneKind.Editor;

    private void OnEntityDelete(EntityDelete entityDelete)
    {
        if (entityDelete.Entity.TryGetComponent<VisualRid>(out var visual))
            RenderingServer.FreeRid(visual.Value);

        if (entityDelete.Entity.TryGetComponent<PhysicsBody>(out var body))
            Physics.DestroyBody(body.Body);
    }

    public void Dispose()
    {
        _sceneDirector.Changed -= OnSceneChanged;
        _entities.OnEntityDelete -= OnEntityDelete;
        _trackAudio.Dispose();
        _playfieldAudio.Dispose();
        _metronomeAudio.Dispose();
        Physics.Dispose();
        RenderingServer.FreeRid(_playfield);
        RenderingServer.FreeRid(_timeline);
        RenderingServer.FreeRid(_debugOverlay);
    }
}
