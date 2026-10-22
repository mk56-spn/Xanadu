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
            if (_pendingResults is null && _lastPlayEntry is not null)
                _profiles.RecordScore(_lastPlayEntry.Path, results);

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
            case SceneKind.Profiles:
                ProfilesScene.Build(
                    _entities,
                    _sceneRoot,
                    _screen,
                    _profiles,
                    _visuals);
                break;

            case SceneKind.Main:
                MainMenuScene.Build(_entities, _sceneRoot, _screen, _visuals);
                break;

            case SceneKind.SongSelect:
                BuildSongSelect(ref state);
                break;

            case SceneKind.Play:
                PlayScene.Build(
                    _entities,
                    _sceneRoot,
                    _screen,
                    playEntry!,
                    _keybinds,
                    _visuals);
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
                EditorScene.Build(
                    _entities,
                    _screen,
                    _sceneRoot,
                    _pendingEditorEntry);
                _pendingEditorEntry = null;
                break;

            case SceneKind.OffsetWizard:
                OffsetWizardScene.Build(
                    _entities,
                    _sceneRoot,
                    _screen,
                    _visuals);
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

        foreach (Entity mine in _entities.Query<Mine>().ToEntityList())
            mine.DeleteEntity();

        foreach (Entity trail in _entities.Query<HitTrail>().ToEntityList())
            trail.DeleteEntity();

        foreach (Entity trail in _entities.Query<KeyPressTrail>().ToEntityList())
            trail.DeleteEntity();
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
        SceneKind.Profiles => "PROFILES",
        SceneKind.Editor => "EDITOR",
        SceneKind.OffsetWizard => "OFFSET WIZARD",
        SceneKind.Settings => "SETTINGS",
        _ => "",
    };
}
