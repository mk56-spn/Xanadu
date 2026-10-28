using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
using Xanadu.Systems.Particles;
using Xanadu.Systems.Physics;
using Xanadu.Profiles;
using Xanadu.Settings;
using Xanadu.Levels;
using Xanadu.Systems.Scenes;
using Xanadu.Systems.Ui;

namespace Xanadu.Ecs;

/// <summary>
/// Owns Godot resources and composes the application's phase groups.
/// </summary>
public sealed class XanaduSystemRoot : SystemRoot, IDisposable
{
    private readonly Rid _debugOverlay;
    private readonly SceneDirector _sceneDirector;
    private EditorSystemGroup _editorSystems = null!;
    private readonly Rid _timeline;
    private readonly Rid _hudEditOverlay;
    private readonly Rid _groupPreview;
    private readonly Rid _playfield;
    private readonly Rid _characters;
    private readonly Rid _particleCanvas;
    private readonly Rid _hitboxes;
    private readonly GodotParticleEmitter _particles;
    private readonly EntityStore _entities;
    private readonly MiniaudioTrackAudio _trackAudio = new();
    private readonly MiniaudioTrackAudio _playfieldAudio = new();
    private readonly MiniaudioTrackAudio _metronomeAudio = new();
    private readonly MiniaudioTrackAudio _previewAudio = new();
    private readonly ScrollSystem _scroll = new();

    public Box2DPhysicsWorld Physics { get; } = new();

    public EditorHistory EditorHistory { get; }

    public EditorCommandBus Commands { get; }

    public SceneSystem Scenes { get; }

    public KeybindSettings Keybinds { get; }
    public IKeyboardState Keyboard { get; }

    /// <summary>What the playfield reads: the keyboard, or a replay's keys.</summary>
    public Xanadu.Systems.Replays.ReplayKeyboard PlayKeyboard { get; }

    public ToastSystem Toasts { get; }

    public OffsetSettings Offsets { get; }

    public DisplaySettings Display { get; }

    public HudLayoutSettings Hud { get; }

    public ProfileStore Profiles { get; }

    public XanaduSystemRoot(
        EntityStore entities,
        Rid parentCanvas,
        IPointerInputSource pointerInput,
        SceneDirector sceneDirector,
        Vector2 viewportSize,
        KeybindSettings? keybinds = null,
        bool transitions = true,
        IKeyboardState? keyboard = null,
        HudLayoutSettings? hudLayout = null) : base(entities, "Xanadu")
    {
        var canvas = new GodotRectCanvas();
        _entities = entities;
        entities.OnEntityDelete += OnEntityDelete;
        EditorHistory = new EditorHistory(entities);
        Commands = new EditorCommandBus();
        Keybinds = keybinds ?? new KeybindSettings();
        Keyboard = keyboard ?? new GodotKeyboardState();
        PlayKeyboard = new Xanadu.Systems.Replays.ReplayKeyboard(Keyboard);
        Toasts = new ToastSystem(
            entities,
            viewportSize,
            new GodotVisualRidFactory());
        Offsets = new OffsetSettings();
        Display = new DisplaySettings();
        Hud = hudLayout ?? new HudLayoutSettings();
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
            display: Display,
            replays: Xanadu.Replays.ReplayStore.Default);
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

        // Character rig art: a child of the playfield, so it draws over the field.
        _characters = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_characters, _playfield);

        // Particle bursts: after the characters, so they draw over the rig.
        _particleCanvas = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_particleCanvas, _playfield);
        _particles = new GodotParticleEmitter(_particleCanvas);

        // The F10 hitbox outlines: after the art, so the rig cannot hide them,
        // but still inside the playfield's layer so the HUD stays on top.
        _hitboxes = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_hitboxes, _playfield);
        RenderingServer.CanvasItemSetVisible(_hitboxes, false);

        _hudEditOverlay = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_hudEditOverlay, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_hudEditOverlay, 950);

        // The group overlay's live preview draws above the dialog but below the
        // debug overlay; only the editor scene shows it.
        _groupPreview = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_groupPreview, parentCanvas);
        RenderingServer.CanvasItemSetZIndex(_groupPreview, 985);
        RenderingServer.CanvasItemSetVisible(_groupPreview, false);

        Xanadu.Systems.Ui.UiBootstrap.AttachInitialization(this, parentCanvas);
        Add(new PhysicsSystemGroup(Physics, entities));
        Add(new Xanadu.Systems.Scenes.SongSelectScrollSystem());
        Add(new Xanadu.Systems.Audio.SongPreviewSystem(entities, _previewAudio));
        Xanadu.Systems.Ui.UiBootstrap.AttachPresentation(this, canvas);
        Add(new AudioSystemGroup(_trackAudio, new GodotTrackClock(), canvas, _timeline));
        Add(new PlayfieldSystemGroup(
            entities,
            canvas,
            _playfield,
            _playfieldAudio,
            Keybinds,
            PlayKeyboard,
            Offsets,
            pointerInput,
            Hud,
            _hudEditOverlay,
            new GodotCharacterRigFiles(),
            () => new SvgAttachmentVisuals(_characters),
            _particles,
            PlayKeyboard,
            _hitboxes,
            new GodotOverlayToggleSource(Key.F10)));
        _editorSystems = new EditorSystemGroup(
            entities,
            Commands,
            pointerInput,
            canvas,
            _playfield,
            _groupPreview,
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
            _scroll,
            Keyboard,
            new GodotVisualRidFactory(),
            viewportSize));
        Add(new DebugSystemGroup(
            canvas,
            _debugOverlay,
            parentCanvas,
            new GodotOverlayToggleSource(Key.F3),
            entities,
            this,
            new GodotFpsSource(),
            entities,
            pointerInput,
            Physics,
            new GodotOverlayToggleSource(Key.F6),
            new GodotOverlayPageSource()));
        Add(new Xanadu.Systems.Shaders.ShaderTunerSystem(
            entities,
            Keyboard,
            Xanadu.Systems.Shaders.ShaderParamStore.Default));
        Add(Toasts);

        SetMonitorPerf(true);
    }

    private void OnSceneChanged(SceneKind scene)
    {
        _editorSystems.Enabled = scene == SceneKind.Editor;
        RenderingServer.CanvasItemSetVisible(_groupPreview, scene == SceneKind.Editor);
    }

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
                start = StartWithGroups(entity);
                break;
            }
        }

        if (start is null)
        {
            long time = editor.GetComponent<Playhead>().TimeMs;
            long best = long.MaxValue;
            foreach (Entity entity in _entities.Query<StartPoint>().ToEntityList())
            {
                StartPoint candidate = StartWithGroups(entity);
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

    /// <summary>A start point with its group membership copied for the play launch.</summary>
    private static StartPoint StartWithGroups(Entity entity)
    {
        StartPoint start = entity.GetComponent<StartPoint>();
        if (entity.TryGetComponent(out ItemGroups groups))
            start.Groups = groups.Mask;
        return start;
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
        _previewAudio.Dispose();
        Physics.Dispose();
        _particles.Dispose();
        RenderingServer.FreeRid(_particleCanvas);
        RenderingServer.FreeRid(_hitboxes);
        RenderingServer.FreeRid(_characters);
        RenderingServer.FreeRid(_playfield);
        RenderingServer.FreeRid(_groupPreview);
        RenderingServer.FreeRid(_timeline);
        RenderingServer.FreeRid(_debugOverlay);
    }
}
