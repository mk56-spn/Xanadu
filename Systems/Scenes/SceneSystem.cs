using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Replays;
using Xanadu.Profiles;
using Xanadu.Scenes;
using Xanadu.Settings;
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

/// <summary>ECS-owned scene stack for the main menu, song select, play, editor, and settings.</summary>
public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private readonly EntityStore _entities;
    private readonly SceneDirector _director;
    private readonly Vector2 _screen;
    private readonly IVisualRidFactory _visuals;
    private readonly Func<List<LevelEntry>> _loadLevels;
    private readonly KeybindSettings _keybinds;
    private readonly ProfileStore _profiles;

    private Entity _stateEntity;
    private Entity _confirmationPanel;
    private Entity _pausePanel;
    private Entity _transitionPanel;
    private Entity _sceneRoot;
    private LevelEntry? _pendingPlayEntry;
    private StartPoint? _pendingStart;
    private LevelEntry? _pendingEditorEntry;
    private LevelEntry? _lastPlayEntry;
    private PlayResults? _pendingResults;
    private TransitionPhase _transitionPhase;
    private bool _restartRequested;
    private float _transitionAlpha;
    private readonly bool _transitionsEnabled;
    private readonly SceneControllerRegistry _controllers;
    private readonly SceneActionRegistry _actionRegistry = new();
    private readonly DisplaySettings _display;

    private const float TransitionSeconds = 0.22f;

    private enum TransitionPhase
    {
        None,
        FadeOut,
        FadeIn,
    }

    public SceneSystem(
        EntityStore entities,
        SceneDirector director,
        Vector2 screen,
        IVisualRidFactory? visuals = null,
        Func<List<LevelEntry>>? loadLevels = null,
        KeybindSettings? keybinds = null,
        bool transitions = true,
        ProfileStore? profiles = null,
        DisplaySettings? display = null,
        ReplayStore? replays = null)
    {
        _entities = entities;
        _director = director;
        _screen = screen;
        _visuals = visuals ?? new GodotVisualRidFactory();
        _loadLevels = loadLevels ?? LevelStore.List;
        _keybinds = keybinds ?? new KeybindSettings(persist: false);
        _profiles = profiles ?? new ProfileStore(persist: false);
        _display = display ?? new DisplaySettings(persist: false, apply: false);
        _replays = replays;
        _transitionsEnabled = transitions;
        _controllers = SceneControllerRegistry.CreateDefault(this);
        EditorSceneActions.Register(_actionRegistry);
        ResultsSceneActions.Register(_actionRegistry);
        PauseSceneActions.Register(_actionRegistry);
        SongSelectSceneActions.Register(_actionRegistry);
        SongTimingSceneActions.Register(_actionRegistry);
    }

    public ProfileStore Profiles => _profiles;

    public LevelEntry? SelectedLevel =>
        CurrentController is SongSelectController controller
            ? controller.SelectedEntry
            : null;

    public void ReloadCurrent()
    {
        EnsureState();
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = state.Current;
        _restartRequested = true;
    }
    public EntityStore Entities => _entities;
    public KeybindSettings Keybinds => _keybinds;
    public DisplaySettings Display => _display;

    /// <summary>Saves the active editor before confirming a leave, when available.</summary>
    public Func<bool>? SaveEditor { get; set; }

    /// <summary>Queues a scene transition.</summary>
    public void Request(SceneKind scene)
    {
        EnsureState();
        _stateEntity.GetComponent<SceneState>().Pending = scene;
    }

    private ISceneController? CurrentController =>
        _stateEntity.IsNull ? null : _controllers.Get(Current);

    public SceneKind Current =>
        _stateEntity.IsNull
            ? SceneKind.None
            : _stateEntity.GetComponent<SceneState>().Current;

    /// <summary>Requests a scene. The transition happens on the next system update.</summary>
    public void Start(SceneKind scene)
    {
        EnsureState();
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = scene;
    }

    public void OpenSongTiming() => OpenSongTiming("", SceneKind.Editor);

    public void OpenSongTiming(string songId, SceneKind back)
    {
        EnsureState();
        if (string.IsNullOrWhiteSpace(songId))
        {
            foreach (Entity editor in _entities.Query<LevelInfo>().ToEntityList())
            {
                songId = editor.GetComponent<LevelInfo>().SongId;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(songId))
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.SelectedSongId = songId;
        state.SongTimingBack = back;
        state.Pending = SceneKind.SongTiming;
        if (state.Current == SceneKind.SongTiming)
            _restartRequested = true;
    }

    public void CloseSongTiming()
    {
        EnsureState();
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = state.SongTimingBack;
    }

    /// <summary>Starts play with an already-loaded level entry.</summary>
    public void StartPlay(LevelEntry entry, StartPoint? start = null)
    {
        EnsureState();
        _pendingPlayEntry = entry;
        _pendingStart = start;
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = SceneKind.Play;
    }

    /// <summary>Starts the results screen with an explicit result snapshot.</summary>
    public void StartResults(PlayResults results)
    {
        EnsureState();
        _pendingResults = results;
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.Pending = SceneKind.Results;
    }

    /// <summary>Moves one scene back, pausing in play and confirming when leaving the editor.</summary>
    public void TryBack()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

        if (IsTransitioning())
            return;

        if (state.Current == SceneKind.SongTiming)
        {
            state.Pending = state.SongTimingBack;
            return;
        }

        if (state.Current == SceneKind.Settings &&
            _keybinds.Capturing is not null)
        {
            _keybinds.CancelCapture();
            return;
        }

        if (IsPauseVisible())
        {
            ResumeFromPause();
            return;
        }

        if (IsConfirmationVisible())
        {
            HideConfirmation();
            return;
        }

        ISceneController? controller = CurrentController;
        SceneKind target = controller?.BackTarget ?? SceneKind.None;
        if (target == SceneKind.None)
            return;

        if (controller?.PausesOnBack == true)
        {
            ShowPause();
            return;
        }

        if (controller?.ConfirmsOnBack == true)
        {
            ShowConfirmation(state.Current, target);
            return;
        }

        state.Pending = target;
    }

    /// <summary>Steps the song-select carousel; returns false if it cannot move.</summary>

    /// <summary>Moves one scene forward when the current scene has a forward action.</summary>
    public void TryForward()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

        if (IsTransitioning())
            return;

        if (IsPauseVisible())
        {
            ResumeFromPause();
            return;
        }

        if (IsConfirmationVisible())
        {
            Confirm(ref state);
            return;
        }

        CurrentController?.TryForward(this);
    }

    protected override void OnUpdate()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

        if (!_transitionsEnabled)
        {
            UpdateImmediate(ref state);
            return;
        }

        if (_transitionPhase != TransitionPhase.None)
        {
            UpdateTransition(ref state);
            return;
        }

        if (state.Current == SceneKind.None &&
            state.Pending != SceneKind.None)
        {
            Switch(ref state);
            BeginFadeIn();
            return;
        }

        if (SceneChanged(ref state))
        {
            BeginFadeOut();
            return;
        }

        if (StepScene(ref state))
            BeginFadeOut();
    }

    /// <summary>Restarts the most recent level from its beginning.</summary>

    private void RetryPlay(ref SceneState state)
    {
        if (_lastPlayEntry is null)
            return;

        HidePauseVisual();
        SetPlayback(false);
        _pendingPlayEntry = _lastPlayEntry;
        _pendingStart = CurrentPreviewStart;
        _pendingReplay = _watchingReplay ? _lastReplay : null;
        _pendingResults = null;
        _restartRequested = true;
        state.Pending = SceneKind.Play;
    }
}
