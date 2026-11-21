using Xanadu.Storage.Profiles;
using Xanadu.Storage.Replays;
using Xanadu.Storage.Settings;
using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Bench;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.Commands;
using Xanadu.Logic.Profiles;
using Xanadu.Logic.Replays;
using Xanadu.Screens.SongSelect;
using Xanadu.Wiring.Groups;
using Xanadu.Wiring.Scenes;
using Xanadu.Audio;

namespace Xanadu.Wiring;

/// <summary>
/// The game's own systems: scenes, audio, playfield, editor. It owns no UI:
/// the host's <see cref="UiRoot"/> runs UI input before it and layout, drawing and
/// the debug overlays after it, on the same store.
/// </summary>
public sealed class XanaduSystemRoot : SystemRoot, IDisposable
{
    private readonly SceneDirector _sceneDirector;
    private readonly PlayfieldSystemGroup _playfieldSystems;
    private readonly EditorSystemGroup _editorSystems;
    private readonly Xanadu.Logic.Editor.EditorWheelSystem? _editorWheel;
    private readonly AudioSystemGroup _audioSystems;
    private readonly EntityStore _entities;
    private readonly MiniaudioTrackAudio _trackAudio = new();
    private readonly MiniaudioTrackAudio _playfieldAudio = new();
    private readonly MiniaudioTrackAudio _metronomeAudio = new();
    private readonly MiniaudioTrackAudio _previewAudio = new();
    private readonly FeedbackSoundPlayer _feedbackSounds = new();
    private readonly FeedbackSoundSystem _feedbackSoundSystem;
    private readonly DocsOverlaySystem _docs;


    public EditorHistory EditorHistory { get; }

    public EditorCommandBus Commands { get; }

    public SceneSystem Scenes { get; }

    public KeybindSettings Keybinds { get; }

    /// <summary>Key presses the host feeds in; <see cref="AppShortcutSystem"/> hands them out.</summary>
    public KeyPressQueue KeyPresses { get; } = new();

    public IKeyboardState Keyboard { get; }

    /// <summary>What the playfield reads: the keyboard, or a replay's keys.</summary>
    public Xanadu.Logic.Replays.ReplayKeyboard PlayKeyboard { get; }

    public ToastSystem Toasts { get; }

    public OffsetSettings Offsets { get; }

    public MapOffsetSettings MapOffsets { get; }

    public DisplaySettings Display { get; }

    public HudLayoutSettings Hud { get; }

    public ProfileStore Profiles { get; }

    public XanaduSystemRoot(
        EntityStore entities,
        Handle parentCanvas,
        IPointerInputSource pointerInput,
        SceneDirector sceneDirector,
        Vector2 viewportSize,
        KeybindSettings? keybinds = null,
        bool transitions = true,
        IKeyboardState? keyboard = null,
        HudLayoutSettings? hudLayout = null,
        IWheelSource? wheel = null,
        bool autoPlay = false) : base(entities, "Xanadu")
    {
        IRectCanvas canvas = HostPlatform.NewCanvas();
        _entities = entities;
        EditorHistory = new EditorHistory(entities);
        Commands = new EditorCommandBus();
        Keybinds = keybinds ?? new KeybindSettings();
        Keyboard = keyboard ?? HostPlatform.Keyboard;
        PlayKeyboard = new Xanadu.Logic.Replays.ReplayKeyboard(Keyboard);
        Toasts = new ToastSystem(
            entities,
            viewportSize,
            HostPlatform.Visuals);
        Offsets = new OffsetSettings();
        MapOffsets = new MapOffsetSettings();
        Display = new DisplaySettings();
        Hud = hudLayout ?? new HudLayoutSettings();
        Profiles = new ProfileStore();

        // Register the keybind component archetype before the first runtime
        // Settings build; first-time component registration during scene
        // construction leaves the new layer's structural tags stuck.
        Entity keybindWarmup = entities.CreateEntity(
            UiCommand.Of(KeybindAction.Left));
        keybindWarmup.DeleteEntity();

        Scenes = new SceneSystem(
            entities,
            sceneDirector,
            viewportSize,
            keybinds: Keybinds,
            transitions: transitions,
            profiles: Profiles,
            display: Display,
            replays: ReplayStore.Default,
            pointer: pointerInput);
        Scenes.AddScreen(new OffsetWizardHost(new MiniaudioTrackAudio(), Offsets, Keyboard));
        _sceneDirector = sceneDirector;
        sceneDirector.Changed += OnSceneChanged;

        // The carousel moves and rebinds its rows before layout runs, so a row
        // whose labels changed width is laid out in the same frame it is drawn.
        Add(new Xanadu.Screens.SongSelect.SongSelectHoldSystem(Keyboard, Scenes));
        Add(new Xanadu.Screens.SongSelect.SongSelectScrollbarSystem(pointerInput, Scenes));
        Add(new Xanadu.Screens.SongSelect.SongSelectDragSystem(pointerInput, Scenes));
        Add(new Xanadu.Screens.SongSelect.SongSelectScrollSystem());
        Add(new BenchSystem(entities, new MiniaudioTrackAudio(), Keyboard));
        Add(new Xanadu.Logic.Audio.SongPreviewSystem(entities, _previewAudio));
        _audioSystems = new AudioSystemGroup(
            _trackAudio,
            new StopwatchTrackClock(),
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
            PlayKeyboard,
            MapOffsets,
            autoPlay);
        Add(_playfieldSystems);
        _editorSystems = new EditorSystemGroup(
            entities,
            Commands,
            pointerInput,
            canvas,
            _playfieldSystems.Playfield,
            _metronomeAudio,
            EditorHistory,
            wheel,
            () => Display.EditorAutosave,
            Toasts);
        _editorSystems.SetActive(false);
        Add(_editorSystems);
        Add(new InputSystemGroup(Scenes));
        if (wheel is not null)
        {
            // The UI's input ran first, so this only sees what the UI did not scroll with.
            _editorWheel = new Xanadu.Logic.Editor.EditorWheelSystem(Commands, wheel) { Enabled = false };
            Add(_editorWheel);
        }
        _docs = new DocsOverlaySystem(entities);
        Add(new AppShortcutSystem(KeyPresses, Keybinds, Scenes, _feedbackSounds, _docs));
        Add(_docs);
        // Loaded at boot, so the first sound (a run's first hit) doesn't stall its frame.
        _feedbackSounds.Prepare();
        _feedbackSoundSystem = new FeedbackSoundSystem(entities, _feedbackSounds);
        Add(_feedbackSoundSystem);
        Add(Toasts);

        SetMonitorPerf(true);
    }

    private void OnSceneChanged(SceneKind scene)
    {
        _editorSystems.SetActive(scene == SceneKind.Editor);
        if (_editorWheel is not null)
            _editorWheel.Enabled = scene == SceneKind.Editor;
    }



    public void Dispose()
    {
        _sceneDirector.Changed -= OnSceneChanged;
        _trackAudio.Dispose();
        _playfieldAudio.Dispose();
        _metronomeAudio.Dispose();
        _previewAudio.Dispose();
        _feedbackSoundSystem.Dispose();
        _docs.Dispose();
        _feedbackSounds.Dispose();
        _editorSystems.Dispose();
        _playfieldSystems.Dispose();
        _audioSystems.Dispose();
    }
}
