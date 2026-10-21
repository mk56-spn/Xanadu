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
    private Entity _sceneRoot;
    private List<LevelEntry> _catalog = new();
    private LevelEntry? _pendingPlayEntry;

    private static readonly Vector2 HiddenConfirmation = new(-10_000, -10_000);
    private static readonly Vector2 HiddenPause = new(-10_000, -10_000);

    public SceneSystem(
        EntityStore entities,
        SceneDirector director,
        Vector2 screen,
        IVisualRidFactory? visuals = null,
        Func<List<LevelEntry>>? loadLevels = null,
        KeybindSettings? keybinds = null)
    {
        _entities = entities;
        _director = director;
        _screen = screen;
        _visuals = visuals ?? new GodotVisualRidFactory();
        _loadLevels = loadLevels ?? LevelStore.List;
        _keybinds = keybinds ?? new KeybindSettings(persist: false);
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

    /// <summary>Moves one scene back, pausing in play and confirming when leaving the editor.</summary>
    public void TryBack()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

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

    /// <summary>Moves one scene forward when the current scene has a forward action.</summary>
    public void TryForward()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

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
        }
    }

    protected override void OnUpdate()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

        if (HandlePause(ref state))
        {
            if (state.Pending != state.Current)
                Switch(ref state);

            return;
        }

        if (HandleConfirmation(ref state))
        {
            if (state.Pending != state.Current)
                Switch(ref state);

            return;
        }

        HandleActions(ref state);

        if (state.Pending != state.Current)
            Switch(ref state);
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
    }

    private void BuildLabel()
    {
        UiBuilder label = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.SceneLabelLayer);

        _labelEntity = label.RectEntity(
            new Vector2(_screen.X - 89, 32),
            new Vector2(130, 28),
            new Color("#1e293b"),
            "");

        _labelEntity.AddComponent(new TextStyle(14, SceneUi.DimTextColor));
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

        Entity no = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.MutedButtonColor,
            "CANCEL",
            takesInput: true);
        no.AddComponent(new TextStyle(18, SceneUi.TextColor));

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
            new Vector2(460, 240),
            StackDirection.Vertical,
            spacing: 12,
            color: SceneUi.PanelColor);

        Entity panel = builder.Current;

        Entity title = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(420, 44),
            "PAUSED",
            36,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        Entity message = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(420, 36),
            "",
            16,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        builder.Container(
            Vector2.Zero,
            new Vector2(340, 44),
            StackDirection.Horizontal,
            spacing: 12);

        Entity resume = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.ButtonColor,
            "RESUME",
            takesInput: true);
        resume.AddComponent(new TextStyle(18, SceneUi.TextColor));

        Entity leave = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.MutedButtonColor,
            "LEAVE LEVEL",
            takesInput: true);
        leave.AddComponent(new TextStyle(18, SceneUi.TextColor));

        builder.End();
        builder.End();
        builder.End();

        root.AddComponent(new ScenePause(title, message, resume, leave));
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

        SceneUi.SetText(pause.Message, "Resume or leave the level.");
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
        _keybinds.CancelCapture();
        HidePauseVisual();
        HideConfirmation();

        SceneKind next = state.Pending;
        LevelEntry? playEntry = null;

        if (next == SceneKind.Play)
        {
            playEntry = _pendingPlayEntry ?? LoadEntry(state.SelectedLevelPath);
            _pendingPlayEntry = null;

            if (playEntry is null)
                next = SceneKind.SongSelect;
        }
        else
        {
            _pendingPlayEntry = null;
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

    private void DestroyScene()
    {
        if (!_sceneRoot.IsNull)
        {
            DeleteSubtree(_sceneRoot);
            _sceneRoot = default;
        }

        foreach (Entity note in _entities.Query<Note>().ToEntityList())
            note.DeleteEntity();
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
        SceneKind.Editor => "EDITOR",
        SceneKind.Settings => "SETTINGS",
        _ => "",
    };
}
