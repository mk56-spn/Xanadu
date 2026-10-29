using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Profiles;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Theme;
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private const float PauseWidth = 520;
    private const float PauseHeight = 300;
    private const float MessageWidth = 480;
    private const float MessageHeight = 36;
    private const float ActionWidth = 140;
    private const float LeaveWidth = 160;
    private const float ActionHeight = 42;

    private void BuildPause()
    {
        UiNode ui = UiNode.Layer(
            _entities, default, SceneUi.PauseLayer, _visuals, UiStage.Full);

        UiNode root = ui.Column()
            .Box(UiStage.Full.Bounds)
            .At(UiLayout.Hidden)
            .Fill(new Color(0.02f, 0.03f, 0.06f, 0.82f))
            .Input()
            .Cross(StackCrossAlignment.Center)
            .Main(StackMainAlignment.Center);

        UiNode panel = root.Column()
            .Size(PauseWidth, PauseHeight)
            .Fill(SceneUi.PanelColor)
            .Pad(UiMetrics.Space.Md)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        Entity title = panel.Text(
            "PAUSED", UiType.Heading, SceneUi.AccentColor, TextAlign.Center);
        Entity message = panel.Text(
                "", UiType.Body, SceneUi.TextColor, TextAlign.Center)
            .Size(MessageWidth, MessageHeight);

        UiNode buttons = panel.Row().Gap(UiMetrics.Space.Md);
        UiNode resume = buttons.Button("RESUME", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Resume);
        UiNode retry = buttons.Button("RETRY", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Retry);
        UiNode leave = buttons.Button(
                "LEAVE LEVEL", UiType.Label, SceneUi.MutedButtonColor)
            .Size(LeaveWidth, ActionHeight)
            .On(SceneActionId.Leave);

        root.With(new ScenePause(title, message, resume, retry, leave));
        _pausePanel = root.Entity;
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
        if (IsEditorPreview)
        {
            ReturnToEditor(ref state);
            return;
        }

        state.Pending = CurrentController?.BackTarget ?? SceneKind.SongSelect;
    }

    public void ResumePause()
    {
        EnsureState();
        ResumeFromPause();
    }

    public void LeavePause()
    {
        EnsureState();
        ref SceneState state = ref _stateEntity.GetComponent<SceneState>();
        LeavePlay(ref state);
    }

    public void PausePlay()
    {
        EnsureState();
        ShowPause();
    }

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

    private void HidePauseVisual()
    {
        if (_pausePanel.IsNull)
            return;

        ref ScenePause pause = ref _pausePanel.GetComponent<ScenePause>();

        if (!pause.Visible)
            return;

        pause.Visible = false;
        ref Position position = ref _pausePanel.GetComponent<Position>();
        position.Value = UiLayout.Hidden;
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
}
