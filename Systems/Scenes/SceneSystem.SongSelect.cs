using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Profiles;
using Writer.Scenes;
using Writer.Settings;
using Writer.Ui;
using SceneState = Writer.Ecs.SceneState;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
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
            _profiles,
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
        SongSelectScene.Refresh(view, _catalog, selected, _profiles);
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
}
