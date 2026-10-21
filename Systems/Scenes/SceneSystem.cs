using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;
using Writer.Ui;
using SceneState = Writer.Ecs.SceneState;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Scenes;

/// <summary>ECS-owned scene stack for the main menu, song select, play, editor, and settings.</summary>
public sealed class SceneSystem : QuerySystem<SceneState>
{
    private readonly EntityStore _entities;
    private readonly SceneDirector _director;
    private readonly Vector2 _screen;
    private readonly IVisualRidFactory _visuals;
    private readonly Func<List<LevelEntry>> _loadLevels;
    private readonly KeybindSettings _keybinds;

    private Entity _stateEntity;
    private Entity _labelEntity;
    private Entity _confirmationPanel;
    private Entity _pausePanel;
    private Entity _transitionPanel;
    private Entity _sceneRoot;
    private List<LevelEntry> _catalog = new();
    private LevelEntry? _pendingPlayEntry;
    private LevelEntry? _lastPlayEntry;
    private PlayResults? _pendingResults;
    private TransitionPhase _transitionPhase;
    private bool _restartRequested;
    private float _transitionAlpha;
    private readonly bool _transitionsEnabled;

    private const float TransitionSeconds = 0.22f;
    private static readonly Vector2 HiddenConfirmation = new(-10_000, -10_000);
    private static readonly Vector2 HiddenPause = new(-10_000, -10_000);
    private static readonly Vector2 HiddenTransition = new(-10_000, -10_000);

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
        bool transitions = true)
    {
        _entities = entities;
        _director = director;
        _screen = screen;
        _visuals = visuals ?? new GodotVisualRidFactory();
        _loadLevels = loadLevels ?? LevelStore.List;
        _keybinds = keybinds ?? new KeybindSettings(persist: false);
        _transitionsEnabled = transitions;
    }

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

    /// <summary>Starts play with an already-loaded level entry.</summary>
    public void StartPlay(LevelEntry entry)
    {
        EnsureState();
        _pendingPlayEntry = entry;
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

        SceneKind target = state.Current switch
        {
            SceneKind.SongSelect => SceneKind.Main,
            SceneKind.Play => SceneKind.SongSelect,
            SceneKind.Results => SceneKind.SongSelect,
            SceneKind.Editor => SceneKind.Main,
            SceneKind.Settings => SceneKind.Main,
            _ => SceneKind.None,
        };

        if (target == SceneKind.None)
            return;

        if (state.Current == SceneKind.Play)
        {
            ShowPause();
            return;
        }

        if (state.Current == SceneKind.Editor)
        {
            ShowConfirmation(state.Current, target);
            return;
        }

        state.Pending = target;
    }

    /// <summary>Steps the song-select carousel; returns false if it cannot move.</summary>
    public bool TryStepCarousel(int direction)
    {
        if (_stateEntity.IsNull || IsTransitioning())
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

        if (state.Current != SceneKind.SongSelect ||
            state.Pending != state.Current ||
            _catalog.Count == 0 ||
            _sceneRoot.IsNull ||
            !_sceneRoot.HasComponent<SongSelectState>())
        {
            return false;
        }

        int before = _sceneRoot.GetComponent<SongSelectState>().Selected;
        StepCarousel(ref state, direction);
        return _sceneRoot.GetComponent<SongSelectState>().Selected != before;
    }

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

        switch (state.Current)
        {
            case SceneKind.Main:
                state.Pending = SceneKind.SongSelect;
                break;

            case SceneKind.SongSelect:
                if (!string.IsNullOrEmpty(state.SelectedLevelPath))
                    state.Pending = SceneKind.Play;
                break;

            case SceneKind.Results:
                state.Pending = SceneKind.SongSelect;
                break;
        }
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

        if (state.Pending != state.Current || _restartRequested)
        {
            BeginFadeOut();
            return;
        }

        if (HandlePause(ref state))
        {
            if (state.Pending != state.Current || _restartRequested)
                BeginFadeOut();

            return;
        }

        if (HandleConfirmation(ref state))
        {
            if (state.Pending != state.Current || _restartRequested)
                BeginFadeOut();

            return;
        }

        if (state.Current == SceneKind.Play &&
            state.Pending == SceneKind.Play &&
            IsPlayFinished())
        {
            state.Pending = SceneKind.Results;
        }

        HandleActions(ref state);

        if (state.Pending != state.Current || _restartRequested)
            BeginFadeOut();
    }

    private void UpdateImmediate(ref SceneState state)
    {
        if (_restartRequested)
        {
            Switch(ref state);
            return;
        }

        if (HandlePause(ref state))
        {
            if (state.Pending != state.Current || _restartRequested)
                Switch(ref state);

            return;
        }

        if (HandleConfirmation(ref state))
        {
            if (state.Pending != state.Current || _restartRequested)
                Switch(ref state);

            return;
        }

        if (state.Current == SceneKind.Play &&
            state.Pending == SceneKind.Play &&
            IsPlayFinished())
        {
            state.Pending = SceneKind.Results;
        }

        HandleActions(ref state);

        if (state.Pending != state.Current || _restartRequested)
            Switch(ref state);
    }

    private void BeginFadeOut()
    {
        _transitionPhase = TransitionPhase.FadeOut;
        _transitionAlpha = 0f;
        SetTransitionVisual(0f, visible: true);
    }

    private void BeginFadeIn()
    {
        _transitionPhase = TransitionPhase.FadeIn;
        _transitionAlpha = 1f;
        SetTransitionVisual(1f, visible: true);
    }

    private void UpdateTransition(ref SceneState state)
    {
        float step = Tick.deltaTime / TransitionSeconds;

        if (_transitionPhase == TransitionPhase.FadeOut)
        {
            _transitionAlpha = Math.Min(1f, _transitionAlpha + step);
            SetTransitionVisual(_transitionAlpha, visible: true);

            if (_transitionAlpha < 1f)
                return;

            Switch(ref state);
            _transitionPhase = TransitionPhase.FadeIn;
            _transitionAlpha = 1f;
            SetTransitionVisual(1f, visible: true);
            return;
        }

        _transitionAlpha = Math.Max(0f, _transitionAlpha - step);
        SetTransitionVisual(_transitionAlpha, visible: true);

        if (_transitionAlpha > 0f)
            return;

        _transitionPhase = TransitionPhase.None;
        SetTransitionVisual(0f, visible: false);
    }

    private bool IsTransitioning() =>
        _transitionsEnabled && _transitionPhase != TransitionPhase.None;

    private void SetTransitionVisual(float alpha, bool visible)
    {
        if (_transitionPanel.IsNull)
            return;

        ref FillColor fill = ref _transitionPanel.GetComponent<FillColor>();
        fill.Value = new Color(0f, 0f, 0f, alpha);

        ref Position position = ref _transitionPanel.GetComponent<Position>();
        position.Value = visible ? _screen / 2 : HiddenTransition;

        _transitionPanel.AddTag<Moved>();
        _transitionPanel.AddTag<Dirty>();
    }

    private void EnsureState()
    {
        if (_stateEntity.IsNull)
            _stateEntity = _entities.CreateEntity(new SceneState(SceneKind.None));

        if (_labelEntity.IsNull)
            BuildLabel();

        if (_confirmationPanel.IsNull)
            BuildConfirmation();

        if (_pausePanel.IsNull)
            BuildPause();

        if (_transitionPanel.IsNull)
            BuildTransition();
    }

    private void BuildTransition()
    {
        UiBuilder builder = UiBuilder.Begin(_entities, _visuals)
            .Layer(1000);

        _transitionPanel = builder.RectEntity(
            HiddenTransition,
            _screen,
            new Color(0f, 0f, 0f, 0f),
            takesInput: true);
    }

    private void BuildLabel()
    {
        UiBuilder label = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.SceneLabelLayer);

        var position = new Vector2(_screen.X - 210, 52);
        var size = new Vector2(380, 100);

        (Vector2 Size, float Alpha)[] backdrops =
        {
            (new Vector2(500, 160), 0.025f),
            (new Vector2(460, 140), 0.030f),
            (new Vector2(410, 115), 0.035f),
            (size, 0.040f),
        };

        foreach ((Vector2 backdropSize, float alpha) in backdrops)
        {
            label.LinearGradientEntity(
                position,
                backdropSize,
                new LinearGradient(
                    new Color(0f, 0f, 0f, 0f),
                    new Color(0f, 0f, 0f, alpha)));
        }

        _labelEntity = label.RectEntity(
            position,
            size,
            new Color(0f, 0f, 0f, 0f),
            "");

        _labelEntity.AddComponent(new TextStyle(
            56,
            new Color(0f, 0f, 0f, 0.45f)));
    }

    private void BuildConfirmation()
    {
        UiBuilder builder = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.ConfirmationLayer);

        builder.Container(
            HiddenConfirmation,
            new Vector2(520, 200),
            StackDirection.Vertical,
            spacing: 12,
            color: new Color("#2a1215"));

        Entity panel = builder.Current;

        Entity title = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 30),
            "",
            26,
            SceneUi.AccentColor,
            new Color("#9f1d1d"));

        Entity message = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 50),
            "",
            16,
            SceneUi.TextColor,
            new Color("#2a1215"));

        builder.Container(
            Vector2.Zero,
            new Vector2(340, 44),
            StackDirection.Horizontal,
            spacing: 12);

        Entity yes = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.ButtonColor,
            "YES",
            takesInput: true);
        yes.AddComponent(new TextStyle(18, SceneUi.TextColor));
        yes.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));

        Entity no = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.MutedButtonColor,
            "CANCEL",
            takesInput: true);
        no.AddComponent(new TextStyle(18, SceneUi.TextColor));
        no.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));

        builder.End();
        builder.End();

        panel.AddComponent(new SceneConfirmation(title, message, yes, no));
        _confirmationPanel = panel;
    }

    private void BuildPause()
    {
        UiBuilder builder = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.PauseLayer);

        builder.Container(
            HiddenPause,
            _screen,
            StackDirection.Vertical,
            color: new Color(0.02f, 0.03f, 0.06f, 0.82f),
            takesInput: true);

        Entity root = builder.Current;

        builder.Container(
            Vector2.Zero,
            new Vector2(520, 300),
            StackDirection.Vertical,
            spacing: 12,
            color: SceneUi.PanelColor);

        Entity panel = builder.Current;

        Entity title = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 44),
            "PAUSED",
            36,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        Entity message = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 36),
            "",
            16,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        builder.Container(
            Vector2.Zero,
            new Vector2(480, 44),
            StackDirection.Horizontal,
            spacing: 12);

        Entity resume = builder.RectEntity(
            Vector2.Zero,
            new Vector2(140, 42),
            SceneUi.ButtonColor,
            "RESUME",
            takesInput: true);
        resume.AddComponent(new TextStyle(18, SceneUi.TextColor));
        resume.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));

        Entity retry = builder.RectEntity(
            Vector2.Zero,
            new Vector2(140, 42),
            SceneUi.ButtonColor,
            "RETRY",
            takesInput: true);
        retry.AddComponent(new TextStyle(18, SceneUi.TextColor));
        retry.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        retry.AddComponent(new RetryAction());

        Entity leave = builder.RectEntity(
            Vector2.Zero,
            new Vector2(160, 42),
            SceneUi.MutedButtonColor,
            "LEAVE LEVEL",
            takesInput: true);
        leave.AddComponent(new TextStyle(18, SceneUi.TextColor));
        leave.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));

        builder.End();
        builder.End();
        builder.End();

        root.AddComponent(new ScenePause(title, message, resume, retry, leave));
        _pausePanel = root;
    }

    private bool HandlePause(ref SceneState state)
    {
        if (_pausePanel.IsNull)
            return false;

        ref ScenePause pause = ref _pausePanel.GetComponent<ScenePause>();

        if (!pause.Visible)
            return false;

        if (!pause.Resume.IsNull && pause.Resume.Tags.Has<Pressed>())
        {
            ResumeFromPause();
            return true;
        }

        if (!pause.Retry.IsNull && pause.Retry.Tags.Has<Pressed>())
        {
            RetryPlay(ref state);
            return true;
        }

        if (!pause.Leave.IsNull && pause.Leave.Tags.Has<Pressed>())
        {
            LeavePlay(ref state);
            return true;
        }

        return true;
    }

    private bool IsPauseVisible() =>
        !_pausePanel.IsNull &&
        _pausePanel.GetComponent<ScenePause>().Visible;

    private void ShowPause()
    {
        if (_pausePanel.IsNull)
            return;

        ref ScenePause pause = ref _pausePanel.GetComponent<ScenePause>();

        if (pause.Visible)
            return;

        SceneUi.SetText(pause.Message, "Resume, retry, or leave the level.");
        ref Position position = ref _pausePanel.GetComponent<Position>();
        position.Value = _screen / 2;
        pause.Visible = true;
        _pausePanel.AddTag<Moved>();
        SetPlayback(false);
    }

    private void ResumeFromPause()
    {
        HidePauseVisual();
        SetPlayback(true);
    }

    private void LeavePlay(ref SceneState state)
    {
        HidePauseVisual();
        SetPlayback(false);
        state.Pending = SceneKind.SongSelect;
    }

    /// <summary>Restarts the most recent level from its beginning.</summary>
    public bool RetryPlay()
    {
        if (_stateEntity.IsNull || IsTransitioning())
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        if (state.Current is not (SceneKind.Play or SceneKind.Results))
            return false;

        if (_lastPlayEntry is null)
            return false;

        RetryPlay(ref state);
        return true;
    }

    private void RetryPlay(ref SceneState state)
    {
        if (_lastPlayEntry is null)
            return;

        HidePauseVisual();
        SetPlayback(false);
        _pendingPlayEntry = _lastPlayEntry;
        _pendingResults = null;
        _restartRequested = true;
        state.Pending = SceneKind.Play;
    }

    private void HidePauseVisual()
    {
        if (_pausePanel.IsNull)
            return;

        ref ScenePause pause = ref _pausePanel.GetComponent<ScenePause>();

        if (!pause.Visible)
            return;

        pause.Visible = false;
        ref Position position = ref _pausePanel.GetComponent<Position>();
        position.Value = HiddenPause;
        _pausePanel.AddTag<Moved>();
    }

    private void SetPlayback(bool playing)
    {
        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldMode>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            entity.GetComponent<Playhead>().Playing = playing;

            if (playing)
                entity.RemoveTag<PlayPaused>();
            else
                entity.AddTag<PlayPaused>();

            return;
        }
    }

    private bool HandleConfirmation(ref SceneState state)
    {
        if (_confirmationPanel.IsNull)
            return false;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        if (!confirmation.Visible)
            return false;

        if (!confirmation.Yes.IsNull && confirmation.Yes.Tags.Has<Pressed>())
        {
            Confirm(ref state);
            return true;
        }

        if (!confirmation.No.IsNull && confirmation.No.Tags.Has<Pressed>())
        {
            HideConfirmation();
            return true;
        }

        return true;
    }

    private bool IsConfirmationVisible() =>
        !_confirmationPanel.IsNull &&
        _confirmationPanel.GetComponent<SceneConfirmation>().Visible;

    private void ShowConfirmation(SceneKind current, SceneKind target)
    {
        if (_confirmationPanel.IsNull)
            return;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        bool play = current == SceneKind.Play;
        confirmation.Target = target;
        SceneUi.SetText(
            confirmation.Title,
            play ? "LEAVE PLAY?" : "LEAVE EDITOR?");
        SceneUi.SetText(
            confirmation.Message,
            play
                ? "Return to song select?"
                : "Return to the main menu? Save your level first if needed.");

        ref Position position = ref _confirmationPanel.GetComponent<Position>();
        position.Value = _screen / 2;
        confirmation.Visible = true;
        _confirmationPanel.AddTag<Moved>();
    }

    private void HideConfirmation()
    {
        if (_confirmationPanel.IsNull)
            return;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        if (!confirmation.Visible)
            return;

        confirmation.Visible = false;
        ref Position position = ref _confirmationPanel.GetComponent<Position>();
        position.Value = HiddenConfirmation;
        _confirmationPanel.AddTag<Moved>();
    }

    private void Confirm(ref SceneState state)
    {
        if (_confirmationPanel.IsNull)
            return;

        SceneKind target =
            _confirmationPanel.GetComponent<SceneConfirmation>().Target;

        HideConfirmation();

        if (target != SceneKind.None)
            state.Pending = target;
    }

    private void HandleActions(ref SceneState state)
    {
        if (state.Current == SceneKind.Settings)
        {
            foreach (Entity entity in _entities
                         .Query<KeybindButton>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                _keybinds.BeginCapture(
                    entity.GetComponent<KeybindButton>().Action);
                return;
            }
        }

        if (state.Current == SceneKind.Play)
        {
            foreach (Entity entity in _entities
                         .Query<PauseAction>()
                         .AllTags(Tags.Get<Pressed>())
                         .ToEntityList())
            {
                ShowPause();
                return;
            }
        }

        foreach (Entity entity in _entities
                     .Query<RetryAction>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            if (state.Current == SceneKind.Results)
                RetryPlay(ref state);

            return;
        }

        foreach (Entity entity in _entities
                     .Query<SceneAction>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            SceneKind target = entity.GetComponent<SceneAction>().Target;
            if (target == SceneKind.None)
                continue;

            state.Pending = target;
            return;
        }

        if (state.Current != SceneKind.SongSelect ||
            state.Pending != state.Current)
        {
            return;
        }

        foreach (Entity entity in _entities
                     .Query<CarouselStep>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            StepCarousel(ref state, entity.GetComponent<CarouselStep>().Direction);
            return;
        }
    }

    private void Switch(ref SceneState state)
    {
        _restartRequested = false;
        _keybinds.CancelCapture();
        HidePauseVisual();
        HideConfirmation();

        SceneKind next = state.Pending;
        LevelEntry? playEntry = null;
        PlayResults results = default;

        if (next == SceneKind.Play)
        {
            playEntry = _pendingPlayEntry ?? LoadEntry(state.SelectedLevelPath);
            _pendingPlayEntry = null;

            if (playEntry is null)
            {
                next = SceneKind.SongSelect;
            }
            else
            {
                _lastPlayEntry = playEntry;
            }
        }
        else
        {
            _pendingPlayEntry = null;
        }

        if (next == SceneKind.Results)
        {
            results = _pendingResults ?? CapturePlayResults();
            _pendingResults = null;
        }
        else
        {
            _pendingResults = null;
        }

        DestroyScene();

        _sceneRoot = _entities.CreateEntity(new SceneRoot(next));

        switch (next)
        {
            case SceneKind.Main:
                MainMenuScene.Build(_entities, _sceneRoot, _screen, _visuals);
                break;

            case SceneKind.SongSelect:
                BuildSongSelect(ref state);
                break;

            case SceneKind.Play:
                PlayScene.Build(_entities, _sceneRoot, _screen, playEntry!, _visuals);
                break;

            case SceneKind.Results:
                ResultsScene.Build(
                    _entities,
                    _sceneRoot,
                    _screen,
                    results,
                    _visuals);
                break;

            case SceneKind.Editor:
                EditorScene.Build(_entities, _screen, _sceneRoot);
                break;

            case SceneKind.Settings:
                SettingsScene.Build(
                    _entities,
                    _sceneRoot,
                    _screen,
                    _keybinds,
                    _visuals);
                break;
        }

        state.Current = next;
        state.Pending = next;
        SceneUi.SetText(_labelEntity, LabelFor(next));
        _director.Apply(next);
    }

    private void BuildSongSelect(ref SceneState state)
    {
        try
        {
            _catalog = _loadLevels();
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to list levels: {exception.Message}");
            _catalog = new List<LevelEntry>();
        }

        state.SelectedIndex = _catalog.Count == 0
            ? 0
            : Math.Clamp(state.SelectedIndex, 0, _catalog.Count - 1);

        state.SelectedLevelPath = _catalog.Count == 0
            ? ""
            : _catalog[state.SelectedIndex].Path;

        SongSelectState view = SongSelectScene.Build(
            _entities,
            _sceneRoot,
            _screen,
            _catalog,
            state.SelectedIndex,
            _visuals);

        _sceneRoot.AddComponent(view);
    }

    private void StepCarousel(ref SceneState state, int direction)
    {
        if (_catalog.Count == 0 ||
            _sceneRoot.IsNull ||
            !_sceneRoot.HasComponent<SongSelectState>())
        {
            return;
        }

        ref SongSelectState view = ref _sceneRoot.GetComponent<SongSelectState>();
        int selected = Math.Clamp(
            view.Selected + direction,
            0,
            _catalog.Count - 1);

        if (selected == view.Selected)
            return;

        view.Selected = selected;
        state.SelectedIndex = selected;
        state.SelectedLevelPath = _catalog[selected].Path;
        SongSelectScene.Refresh(view, _catalog, selected);
    }

    private LevelEntry? LoadEntry(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return new LevelEntry(path, LevelStore.Load(path));
        }
        catch (Exception exception)
        {
            GD.PushError($"Failed to load level '{path}': {exception.Message}");
            return null;
        }
    }

    private bool IsPlayFinished()
    {
        foreach (Entity entity in _entities.Query<PlaySession>().ToEntityList())
        {
            if (entity.GetComponent<PlaySession>().Finished)
                return true;
        }

        return false;
    }

    private PlayResults CapturePlayResults()
    {
        foreach (Entity entity in _entities
                     .Query<ScoreState, PlaySession, LevelInfo>()
                     .ToEntityList())
        {
            LevelInfo level = entity.GetComponent<LevelInfo>();
            ScoreState score = entity.GetComponent<ScoreState>();
            return new PlayResults(
                level.Name,
                score.Score,
                score.Accuracy,
                score.MaxCombo,
                score.NotesHit,
                score.TotalNotes);
        }

        return new PlayResults("", 0, 0, 0, 0, 0);
    }

    private void DestroyScene()
    {
        if (!_sceneRoot.IsNull)
        {
            DeleteSubtree(_sceneRoot);
            _sceneRoot = default;
        }

        foreach (Entity note in _entities.Query<Note>().ToEntityList())
            note.DeleteEntity();

        foreach (Entity movement in _entities.Query<MovementEvent>().ToEntityList())
            movement.DeleteEntity();
    }

    private static void DeleteSubtree(Entity entity)
    {
        var children = new List<Entity>();
        foreach (Entity child in entity.ChildEntities)
            children.Add(child);

        foreach (Entity child in children)
            DeleteSubtree(child);

        entity.DeleteEntity();
    }

    private static string LabelFor(SceneKind scene) => scene switch
    {
        SceneKind.Main => "MAIN",
        SceneKind.SongSelect => "SONG SELECT",
        SceneKind.Play => "PLAY",
        SceneKind.Results => "RESULTS",
        SceneKind.Editor => "EDITOR",
        SceneKind.Settings => "SETTINGS",
        _ => "",
    };
}
