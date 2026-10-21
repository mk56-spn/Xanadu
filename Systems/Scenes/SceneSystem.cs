using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Scenes;
using Writer.Ui;
using SceneState = Writer.Ecs.SceneState;

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
    private Entity _sceneRoot;
    private List<LevelEntry> _catalog = new();

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

    protected override void OnUpdate()
    {
        if (_stateEntity.IsNull)
            return;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        HandleActions(ref state);

        if (state.Pending != state.Current)
            Switch(ref state);
    }

    private void EnsureState()
    {
        if (_stateEntity.IsNull)
            _stateEntity = _entities.CreateEntity(new SceneState(SceneKind.None));

        if (!_labelEntity.IsNull)
            return;

        UiBuilder label = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.SceneLabelLayer);

        _labelEntity = label.RectEntity(
            new Vector2(_screen.X - 89, 32),
            new Vector2(130, 28),
            new Color("#1e293b"),
            "");

        _labelEntity.AddComponent(new TextStyle(14, SceneUi.DimTextColor));
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
