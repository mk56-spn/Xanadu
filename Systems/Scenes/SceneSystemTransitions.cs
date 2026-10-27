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
            if (IsEditorPreview)
                ReturnToEditor(ref state);
            else
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
}
