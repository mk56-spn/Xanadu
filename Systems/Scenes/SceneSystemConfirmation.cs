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
using Xanadu.Ui.Ecs.Ui;
using SceneState = Xanadu.Ecs.SceneState;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private const float ConfirmWidth = 520;
    private const float ConfirmHeight = 200;
    private const float ConfirmTextWidth = 480;
    private const float ConfirmTitleHeight = 30;
    private const float ConfirmMessageHeight = 50;
    private const float ConfirmActionHeight = 42;
    private const float SaveWidth = 190;
    private const float ConfirmActionWidth = 150;

    private void BuildConfirmation()
    {
        UiNode ui = UiNode.Layer(
            _entities, default, UiLayers.Confirmation, _visuals, UiStage.Full);

        UiNode panel = ui.Column()
            .Size(ConfirmWidth, ConfirmHeight)
            .Hidden()
            .Fill(Palette.DangerSurface)
            .Pad(UiMetrics.Space.Md)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        UiNode title = panel.Text(
                "", UiType.Section, SceneUi.AccentColor, TextAlign.Center)
            .Size(ConfirmTextWidth, ConfirmTitleHeight)
            .Fill(Palette.DangerDark);
        UiNode message = panel.Text(
                "", UiType.Body, SceneUi.TextColor, TextAlign.Center)
            .Size(ConfirmTextWidth, ConfirmMessageHeight);

        UiNode buttons = panel.Row().Gap(UiMetrics.Space.Sm);
        UiNode save = buttons.Button(
                "SAVE & LEAVE", UiType.Small, SceneUi.ButtonColor)
            .Size(SaveWidth, ConfirmActionHeight);
        UiNode yes = buttons.Button("YES", UiType.Label, SceneUi.ButtonColor)
            .Size(ConfirmActionWidth, ConfirmActionHeight);
        UiNode no = buttons.Button("CANCEL", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ConfirmActionWidth, ConfirmActionHeight);

        panel.With(new SceneConfirmation(title, message, yes, no, save));
        _confirmationPanel = panel.Entity;
    }

    private bool HandleConfirmation(ref SceneState state)
    {
        if (_confirmationPanel.IsNull)
            return false;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        if (UiLayout.IsHidden(_confirmationPanel))
            return false;

        if (IsPressed(confirmation.Yes))
        {
            Confirm(ref state);
            return true;
        }

        if (IsPressed(confirmation.Save) && SaveEditor is { } save)
        {
            // A level that cannot be saved stays open, so the dialog has to say
            // why: the editor's status line is not where anyone is looking.
            EditorSaveOutcome outcome = save();
            if (outcome.Saved)
            {
                confirmation.Target = SceneKind.SongSelect;
                Confirm(ref state);
            }
            else
            {
                SceneUi.SetText(
                    confirmation.Message,
                    $"Can't save: {outcome.Problem}. YES leaves without saving.");
            }

            return true;
        }

        if (IsPressed(confirmation.No))
        {
            HideConfirmation();
            return true;
        }

        return true;
    }

    private bool IsConfirmationVisible() =>
        !_confirmationPanel.IsNull &&
        !UiLayout.IsHidden(_confirmationPanel);

    private void ShowConfirmation(SceneKind current, SceneKind target)
    {
        if (_confirmationPanel.IsNull)
            return;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        bool play = current == SceneKind.Play;
        if (current == SceneKind.Editor && !EditorHasSavedLevel())
            target = SceneKind.Main;
        confirmation.Target = target;
        if (!confirmation.Save.IsNull)
        {
            UiLayout.SetHidden(confirmation.Save, play);
        }
        SceneUi.SetText(
            confirmation.Title,
            play ? "LEAVE PLAY?" : "LEAVE EDITOR?");
        SceneUi.SetText(
            confirmation.Message,
            play
                ? "Return to song select?"
                : "Save this level before returning to song select?");

        ref Position position = ref _confirmationPanel.GetComponent<Position>();
        position.Value = _screen / 2;
        _confirmationPanel.AddTag<Moved>();
        UiLayout.SetHidden(_confirmationPanel, false);
    }

    private bool EditorHasSavedLevel()
    {
        foreach (Entity entity in _entities.Query<LevelFile>().ToEntityList())
        {
            if (!string.IsNullOrWhiteSpace(entity.GetComponent<LevelFile>().Path))
                return true;
        }

        return false;
    }

    private void HideConfirmation()
    {
        if (_confirmationPanel.IsNull)
            return;

        UiLayout.SetHidden(_confirmationPanel, true);
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
}
