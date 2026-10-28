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

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void EnsureState()
    {
        if (_stateEntity.IsNull)
            _stateEntity = _entities.CreateEntity(new SceneState(SceneKind.None));

        if (_confirmationPanel.IsNull)
            BuildConfirmation();

        if (_pausePanel.IsNull)
            BuildPause();

        if (_transitionPanel.IsNull)
            BuildTransition();
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

        Replay? replay = null;
        if (next == SceneKind.Play)
        {
            replay = _pendingReplay;
            _pendingReplay = null;
            _watchingReplay = replay is not null;
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
            _pendingReplay = null;
        }

        if (next == SceneKind.Results)
        {
            results = _pendingResults ?? CapturePlayResults();
            if (_pendingResults is null && _lastPlayEntry is not null && !_watchingReplay)
            {
                // A death is not a score to record, but the attempt is still a replay.
                if (!results.Failed)
                    _profiles.RecordScore(_lastPlayEntry.Path, results);
                SaveReplay(_lastPlayEntry, results);
            }

            _pendingResults = null;
        }
        else
        {
            _pendingResults = null;
        }

        ClearPreviewIfLeaving(next);
        DestroyScene();

        _sceneRoot = _entities.CreateEntity(new SceneRoot(next));

        var context = new SceneBuildContext(
            _entities,
            _sceneRoot,
            _stateEntity,
            _screen,
            _visuals,
            _keybinds,
            _profiles,
            _display,
            _loadLevels,
            next == SceneKind.Editor ? _pendingEditorEntry : playEntry,
            results,
            _pendingStart,
            replay,
            RecordReplay: _replays is not null &&
                          next == SceneKind.Play &&
                          replay is null &&
                          _pendingStart is null &&
                          !IsEditorPreview,
            CanWatchReplay: _lastReplay is not null);
        _pendingEditorEntry = null;
        _pendingStart = null;

        if (!_controllers.Build(next, context))
        {
            next = SceneKind.Main;
            _controllers.Build(
                next,
                context with { Entry = null, Results = null });
        }

        state.Current = next;
        state.Pending = next;
        RestorePreviewPlayhead(next);
        _director.Apply(next);
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
            PlaySession session = entity.GetComponent<PlaySession>();
            return new PlayResults(
                level.Name,
                score.Score,
                score.Accuracy,
                score.MaxCombo,
                score.NotesHit,
                score.TotalNotes,
                score.FlawlessPCount,
                score.FlawlessCount,
                score.CleanCount,
                score.FairCount,
                score.DeficientCount,
                score.TerribleCount,
                score.MissCount,
                Failed: session.Died);
        }

        return new PlayResults("", 0, 0, 0, 0, 0);
    }

    private void DestroyScene()
    {
        if (!_sceneRoot.IsNull)
        {
            UiLayout.DeleteSubtree(_sceneRoot);
            _sceneRoot = default;
        }

        foreach (Entity note in _entities.Query<Note>().ToEntityList())
            note.DeleteEntity();

        foreach (Entity mine in _entities.Query<Mine>().ToEntityList())
            mine.DeleteEntity();

        foreach (Entity block in _entities.Query<Block>().ToEntityList())
            block.DeleteEntity();

        foreach (Entity spike in _entities.Query<Spike>().ToEntityList())
            spike.DeleteEntity();

        foreach (Entity trail in _entities.Query<HitTrail>().ToEntityList())
            trail.DeleteEntity();

        foreach (Entity pop in _entities.Query<NotePop>().ToEntityList())
            pop.DeleteEntity();

        foreach (Entity trail in _entities.Query<KeyPressTrail>().ToEntityList())
            trail.DeleteEntity();
    }
}
