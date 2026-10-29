using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Debug;
using Xanadu.Systems.Groups;
using Xanadu.Systems.Input;
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
    private readonly SceneDirector _sceneDirector;
    private readonly PlayfieldSystemGroup _playfieldSystems;
    private readonly EditorSystemGroup _editorSystems;
    private readonly AudioSystemGroup _audioSystems;
    private readonly DebugSystemGroup _debugSystems;
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
            replays: Xanadu.Replays.ReplayStore.Default,
            pointer: pointerInput);
        _sceneDirector = sceneDirector;
        sceneDirector.Changed += OnSceneChanged;

        Xanadu.Systems.Ui.UiBootstrap.AttachInitialization(this, parentCanvas);
        Add(new PhysicsSystemGroup(Physics, entities));
        Add(new Xanadu.Systems.Scenes.SongSelectScrollSystem());
        Add(new OffsetWizardSystem(
            entities,
            new MiniaudioTrackAudio(),
            Offsets,
            Keyboard,
            () => Scenes.TryBack()));
        Add(new Xanadu.Systems.Audio.SongPreviewSystem(entities, _previewAudio));
        Xanadu.Systems.Ui.UiBootstrap.AttachPresentation(this, canvas);
        _audioSystems = new AudioSystemGroup(
            _trackAudio,
            new GodotTrackClock(),
            canvas,
            parentCanvas);
        Add(_audioSystems);
        _playfieldSystems = new PlayfieldSystemGroup(
            entities,
            canvas,
            parentCanvas,
            _playfieldAudio,
            Keybinds,
            PlayKeyboard,
            Offsets,
            pointerInput,
            Hud,
            new GodotCharacterRigFiles(),
            PlayKeyboard,
            new GodotOverlayToggleSource(Key.F10));
        Add(_playfieldSystems);
        _editorSystems = new EditorSystemGroup(
            entities,
            Commands,
            pointerInput,
            canvas,
            _playfieldSystems.Playfield,
            parentCanvas,
            _metronomeAudio,
            EditorHistory);
        _editorSystems.SetActive(false);
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
        _debugSystems = new DebugSystemGroup(
            canvas,
            parentCanvas,
            new GodotOverlayToggleSource(Key.F3),
            entities,
            this,
            new GodotFpsSource(),
            new GodotOverlayPageSource());
        Add(_debugSystems);
        Add(new Xanadu.Systems.Shaders.ShaderTunerSystem(
            entities,
            Keyboard,
            Xanadu.Systems.Shaders.ShaderParamStore.Default));
        Add(Toasts);

        SetMonitorPerf(true);
    }

    private void OnSceneChanged(SceneKind scene) =>
        _editorSystems.SetActive(scene == SceneKind.Editor);

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
        _debugSystems.Dispose();
        _editorSystems.Dispose();
        _playfieldSystems.Dispose();
        _audioSystems.Dispose();
    }
}
