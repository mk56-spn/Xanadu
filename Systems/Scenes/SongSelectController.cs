using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Scenes;
using SceneState = Xanadu.Ecs.SceneState;

namespace Xanadu.Systems.Scenes;

/// <summary>Owns the song-select catalog, selection, and carousel.</summary>
public sealed class SongSelectController : ISceneController
{
    private readonly SceneSystem _system;
    private Entity _stateEntity;
    private List<LevelEntry> _catalog = new();

    public SongSelectController(SceneSystem system)
    {
        _system = system;
    }

    public SceneKind Kind => SceneKind.SongSelect;

    public LevelEntry? SelectedEntry =>
        _stateEntity.IsNull || _catalog.Count == 0
            ? null
            : _catalog[Math.Clamp(
                _stateEntity.GetComponent<SceneState>().SelectedIndex,
                0,
                _catalog.Count - 1)];
    public SceneKind BackTarget => SceneKind.Main;

    public bool TryForward(SceneSystem system)
    {
        if (_stateEntity.IsNull)
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        if (string.IsNullOrEmpty(state.SelectedLevelPath) ||
            SelectedEntry?.IsBlocked == true)
        {
            return false;
        }

        system.Request(SceneKind.Play);
        return true;
    }

    public void Build(SceneBuildContext context)
    {
        _stateEntity = context.StateEntity;
        try
        {
            _catalog = context.LoadLevels();
        }
        catch (Exception)
        {
            _catalog = new List<LevelEntry>();
        }

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        state.SelectedIndex = _catalog.Count == 0
            ? 0
            : Math.Clamp(state.SelectedIndex, 0, _catalog.Count - 1);
        state.SelectedLevelPath = _catalog.Count == 0
            ? ""
            : _catalog[state.SelectedIndex].Path;

        SongSelectState view = SongSelectScene.Build(
            context.Entities,
            context.Root,
            context.Screen,
            _catalog,
            state.SelectedIndex,
            context.Profiles,
            context.Visuals);
        context.Root.AddComponent(view);
    }

    public void HandleActions(SceneSystem system, ref SceneState state)
    {
        foreach (Entity entity in system.Entities
                     .Query<SongSelectTile>()
                     .AllTags(Tags.Get<Pressed>())
                     .ToEntityList())
        {
            SongSelectTile tile = entity.GetComponent<SongSelectTile>();
            if (tile.Index < 0 || tile.Index >= _catalog.Count)
                continue;

            state.SelectedIndex = tile.Index;
            state.SelectedLevelPath = _catalog[tile.Index].Path;
            foreach (Entity root in system.Entities
                         .Query<SongSelectState>()
                         .ToEntityList())
            {
                ref SongSelectState view = ref root.GetComponent<SongSelectState>();
                SongSelectScene.Refresh(
                    ref view,
                    _catalog,
                    tile.Index,
                    _system.Profiles);
                break;
            }

            entity.RemoveTag<Pressed>();
            return;
        }
    }

    public bool TryStep(int direction)
    {
        if (_stateEntity.IsNull)
            return false;

        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        if (state.Current != SceneKind.SongSelect ||
            state.Pending != state.Current ||
            _catalog.Count == 0)
        {
            return false;
        }

        int before = state.SelectedIndex;
        int next = Math.Clamp(before + direction, 0, _catalog.Count - 1);
        if (next == before)
            return false;

        state.SelectedIndex = next;
        state.SelectedLevelPath = _catalog[next].Path;
        foreach (Entity root in _stateEntity.Store
                     .Query<SongSelectState>()
                     .ToEntityList())
        {
            if (root.GetComponent<SongSelectState>().Count != _catalog.Count)
                continue;

            ref SongSelectState view = ref root.GetComponent<SongSelectState>();
            SongSelectScene.Refresh(ref view, _catalog, next, _system.Profiles);
            break;
        }

        return true;
    }
}
