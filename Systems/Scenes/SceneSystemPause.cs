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
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void BuildPause()
    {
        UiBuilder builder = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.PauseLayer);

        builder.Container(
            UiLayout.Hidden,
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
        resume.AddComponent(new SceneCommand(SceneActionId.Resume));

        Entity retry = builder.RectEntity(
            Vector2.Zero,
            new Vector2(140, 42),
            SceneUi.ButtonColor,
            "RETRY",
            takesInput: true);
        retry.AddComponent(new TextStyle(18, SceneUi.TextColor));
        retry.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        retry.AddComponent(new SceneCommand(SceneActionId.Retry));

        Entity leave = builder.RectEntity(
            Vector2.Zero,
            new Vector2(160, 42),
            SceneUi.MutedButtonColor,
            "LEAVE LEVEL",
            takesInput: true);
        leave.AddComponent(new TextStyle(18, SceneUi.TextColor));
        leave.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        leave.AddComponent(new SceneCommand(SceneActionId.Leave));

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
