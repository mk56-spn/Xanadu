using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Editor.Commands;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Debug;
using Writer.Systems.Groups;
using Writer.Systems.Input;
using Writer.Systems.Physics;
using Writer.Profiles;
using Writer.Settings;
using Writer.Levels;
using Writer.Systems.Scenes;
using Writer.Systems.Ui;

namespace Writer.Ecs;

/// <summary>
/// Owns Godot resources and composes the application's phase groups.
/// </summary>
public sealed class WriterSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;
    private readonly Rid _elementsOverlay;
    private readonly SceneDirector _sceneDirector;
    private EditorSystemGroup _editorSystems = null!;
    private readonly Rid _timeline;
    private readonly Rid _playfield;
    private readonly EntityStore _entities;
    private readonly MiniaudioTrackAudio _trackAudio = new();
    private readonly MiniaudioTrackAudio _playfieldAudio = new();
    private readonly MiniaudioTrackAudio _metronomeAudio = new();
    private readonly ScrollSystem _scroll = new();

    public Box2DPhysicsWorld Physics { get; } = new();

    public EditorHistory EditorHistory { get; }

    public EditorCommandBus Commands { get; }

    public SceneSystem Scenes { get; }

    public KeybindSettings Keybinds { get; }
    public IKeyboardState Keyboard { get; }

    public ToastSystem Toasts { get; }

    public OffsetSettings Offsets { get; }

    public DisplaySettings Display { get; }

    public ProfileStore Profiles { get; }

    public WriterSystemRoot(
        EntityStore entities,
        Rid parentCanvas,
        IPointerInputSource pointerInput,
        SceneDirector sceneDirector,
        Vector2 viewportSize,
        KeybindSettings? keybinds = null,
        bool transitions = true,
        IKeyboardState? keyboard = null) : base(entities, "Writer")
    {
        var canvas = new GodotRectCanvas();
        _entities = entities;
        entities.OnEntityDelete += OnEntityDelete;
        EditorHistory = new EditorHistory(entities);
        Commands = new EditorCommandBus();
        Keybinds = keybinds ?? new KeybindSettings();
        Keyboard = keyboard ?? new GodotKeyboardState();
        Toasts = new ToastSystem(
            entities,
            viewportSize,
            new GodotVisualRidFactory());
        Offsets = new OffsetSettings();
        Display = new DisplaySettings();
        Profiles = new ProfileStore();

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
            transitions: transitions,
            profiles: Profiles,
            display: Display);
        _sceneDirector = sceneDirector;
        sceneDirector.Changed += OnSceneChanged;

        _debugOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_debugOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_debugOverlay, 1000);
        RenderingServer.CanvasItemSetVisible(_debugOverlay, false);

        _elementsOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_elementsOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_elementsOverlay, 1001);
        RenderingServer.CanvasItemSetVisible(_elementsOverlay, false);

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
            Keybinds,
            Keyboard,
            Offsets));
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
        Add(new InputSystemGroup(
            entities,
            pointerInput,
            Scenes,
            Keybinds,
            Display,
            _scroll));
        Add(new DebugSystemGroup(
            canvas,
            _debugOverlay,
            _elementsOverlay,
            new GodotOverlayToggleSource(Key.F3),
            new GodotOverlayToggleSource(Key.F4),
            entities,
            this,
            new GodotFpsSource(),
            entities,
            pointerInput,
            Physics,
            new GodotOverlayToggleSource(Key.F6),
            new GodotOverlayPageSource()));
        Add(Toasts);

        SetMonitorPerf(true);
    }

    private void OnSceneChanged(SceneKind scene) =>
        _editorSystems.Enabled = scene == SceneKind.Editor;

    private void OnEntityDelete(EntityDelete entityDelete)
    {
        if (entityDelete.Entity.TryGetComponent<VisualRid>(out VisualRid visual))
            RenderingServer.FreeRid(visual.Value);

        if (entityDelete.Entity.TryGetComponent<PhysicsBody>(out PhysicsBody body))
            Physics.DestroyBody(body.Body);
    }


    /// <summary>Launches play from the selected or closest saved start point.</summary>
    public void StartPlayFromEditor()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<LevelInfo, Playhead, NoteSelection>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        StartPoint? start = null;
        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (entity.HasComponent<StartPoint>())
            {
                start = entity.GetComponent<StartPoint>();
                break;
            }
        }

        if (start is null)
        {
            long time = editor.GetComponent<Playhead>().TimeMs;
            long best = long.MaxValue;
            foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
            {
                StartPoint candidate = entity.GetComponent<StartPoint>();
                long distance = Math.Abs(candidate.TimeMs - time);
                if (distance >= best)
                    continue;
                best = distance;
                start = candidate;
            }
        }

        if (start is null)
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, editor);
        string path = editor.HasComponent<LevelFile>()
            ? editor.GetComponent<LevelFile>().Path
            : "";
        Scenes.StartPlay(new LevelEntry(path, document), start);
    }

    /// <summary>Queues a wheel scroll at a viewport position.</summary>
    public void Scroll(Vector2 position, int direction) =>
        _scroll.Request(position, direction);

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
        RenderingServer.FreeRid(_elementsOverlay);
    }
}
