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
        if (StepScene(ref state))
            Switch(ref state);
    }

    /// <summary>
    /// Runs one scene decision step — restart, pause, confirmation, a finished
    /// play, then scene actions — and reports whether the scene should switch.
    /// The immediate path switches on the spot and the fading path starts a fade
    /// out, but both run this same sequence, so a new scene action cannot apply
    /// to only one of them.
    /// </summary>
    private bool StepScene(ref SceneState state)
    {
        if (_restartRequested)
            return true;

        if (HandlePause(ref state))
            return SceneChanged(ref state);

        if (HandleConfirmation(ref state))
            return SceneChanged(ref state);

        if (HandleSongMenu(ref state))
            return SceneChanged(ref state);

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
        return SceneChanged(ref state);
    }

    /// <summary>Whether a handler has asked for a different scene.</summary>
    private bool SceneChanged(ref SceneState state) =>
        state.Pending != state.Current || _restartRequested;

    /// <summary>
    /// Fades through black, unless both scenes share a background: then only the
    /// scene's own layers fade, so the background stays on screen.
    /// </summary>
    private void BeginSwitch(ref SceneState state)
    {
        _fadeSceneOnly = SharesBackground(state.Current, state.Pending);
        BeginFadeOut();
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
        _fadeSceneOnly = false;
    }

    private bool IsTransitioning() =>
        _transitionsEnabled && _transitionPhase != TransitionPhase.None;

    private void SetTransitionVisual(float alpha, bool visible)
    {
        if (_transitionPanel.IsNull)
            return;

        ref FillColor fill = ref _transitionPanel.GetComponent<FillColor>();
        // The panel stays up even when clear: it blocks input during the fade.
        fill.Value = new Color(0f, 0f, 0f, _fadeSceneOnly ? 0f : alpha);
        if (_fadeSceneOnly)
            SetSceneOpacity(1f - alpha);

        ref Position position = ref _transitionPanel.GetComponent<Position>();
        position.Value = _screen / 2;

        _transitionPanel.AddTag<Moved>();
        _transitionPanel.AddTag<Dirty>();
        UiLayout.SetHidden(_transitionPanel, !visible);
    }

    private void SetSceneOpacity(float opacity)
    {
        if (_sceneRoot.IsNull)
            return;

        foreach (Entity child in _sceneRoot.ChildEntities)
        {
            if (child.HasComponent<Layer>())
                UiLayout.SetLayerOpacity(child, opacity);
        }
    }

    private void BuildTransition()
    {
        UiNode ui = UiNode.Layer(_entities, default, UiLayers.SceneTransition, _visuals, UiStage.Full);

        _transitionPanel = ui.Rect(new Color(0f, 0f, 0f, 0f), size: _screen)
            .Hidden()
            .Input()
            .Entity;
    }
}
