using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Scenes;
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

    private Entity _stateEntity;
    private Entity _labelEntity;
    private Entity _confirmationPanel;
    private Entity _sceneRoot;
    private List<LevelEntry> _catalog = new();

    private static readonly Vector2 HiddenConfirmation = new(-10_000, -10_000);

    public SceneSystem(
        EntityStore entities,
        SceneDirector director,
        Vector2 screen,
        IVisualRidFactory? visuals = null,
        Func<List<LevelEntry>>? loadLevels = null)
    {
        _entities = entities;
        _director = director;
        _screen = screen;
        _visuals = visuals ?? new GodotVisualRidFactory();
        _loadLevels = loadLevels ?? LevelStore.List;
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

    /// <summary>Moves one scene back, confirming when leaving play or the editor.</summary>
    public void TryBack()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();

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

        if (state.Current is SceneKind.Play or SceneKind.Editor)
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
        HideConfirmation();

        SceneKind next = state.Pending;
        LevelEntry? playEntry = null;

        if (next == SceneKind.Play)
        {
            playEntry = LoadEntry(state.SelectedLevelPath);
            if (playEntry is null)
                next = SceneKind.SongSelect;
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
                SettingsScene.Build(_entities, _sceneRoot, _screen, _visuals);
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
