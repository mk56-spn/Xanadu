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
using SceneState = Writer.Ecs.SceneState;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Scenes;

public sealed partial class SceneSystem : QuerySystem<SceneState>
{
    private void BuildConfirmation()
    {
        UiBuilder builder = UiBuilder.Begin(_entities, _visuals)
            .Layer(SceneUi.ConfirmationLayer);

        builder.Container(
            HiddenConfirmation,
            new Vector2(520, 200),
            StackDirection.Vertical,
            spacing: 12,
            color: new Color("#2a1215"));

        Entity panel = builder.Current;

        Entity title = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 30),
            "",
            26,
            SceneUi.AccentColor,
            new Color("#9f1d1d"));

        Entity message = SceneUi.Text(
            builder,
            Vector2.Zero,
            new Vector2(480, 50),
            "",
            16,
            SceneUi.TextColor,
            new Color("#2a1215"));

        builder.Container(
            Vector2.Zero,
            new Vector2(520, 44),
            StackDirection.Horizontal,
            spacing: 10);

        Entity save = builder.RectEntity(
            Vector2.Zero,
            new Vector2(190, 42),
            SceneUi.ButtonColor,
            "SAVE & LEAVE",
            takesInput: true);
        save.AddComponent(new TextStyle(15, SceneUi.TextColor));
        save.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));

        Entity yes = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.ButtonColor,
            "YES",
            takesInput: true);
        yes.AddComponent(new TextStyle(18, SceneUi.TextColor));
        yes.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));

        Entity no = builder.RectEntity(
            Vector2.Zero,
            new Vector2(150, 42),
            SceneUi.MutedButtonColor,
            "CANCEL",
            takesInput: true);
        no.AddComponent(new TextStyle(18, SceneUi.TextColor));
        no.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));

        builder.End();
        builder.End();

        panel.AddComponent(new SceneConfirmation(title, message, yes, no, save));
        _confirmationPanel = panel;
    }

    private bool HandleConfirmation(ref SceneState state)
    {
        if (_confirmationPanel.IsNull)
            return false;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        if (!confirmation.Visible)
            return false;

        if (!confirmation.Yes.IsNull && confirmation.Yes.Tags.Has<Pressed>())
        {
            Confirm(ref state);
            return true;
        }

        if (!confirmation.Save.IsNull &&
            confirmation.Save.Tags.Has<Pressed>() &&
            SaveEditor?.Invoke() == true)
        {
            Confirm(ref state);
            return true;
        }

        if (!confirmation.No.IsNull && confirmation.No.Tags.Has<Pressed>())
        {
            HideConfirmation();
            return true;
        }

        return true;
    }

    private bool IsConfirmationVisible() =>
        !_confirmationPanel.IsNull &&
        _confirmationPanel.GetComponent<SceneConfirmation>().Visible;

    private void ShowConfirmation(SceneKind current, SceneKind target)
    {
        if (_confirmationPanel.IsNull)
            return;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        bool play = current == SceneKind.Play;
        confirmation.Target = target;
        if (!confirmation.Save.IsNull)
        {
            if (play)
                confirmation.Save.AddTag<LayoutIgnore>();
            else
                confirmation.Save.RemoveTag<LayoutIgnore>();

            Entity parent = confirmation.Save.Parent;
            if (!parent.IsNull && parent.HasComponent<UiContainer>())
                parent.AddTag<LayoutDirty>();
        }
        SceneUi.SetText(
            confirmation.Title,
            play ? "LEAVE PLAY?" : "LEAVE EDITOR?");
        SceneUi.SetText(
            confirmation.Message,
            play
                ? "Return to song select?"
                : "Save this level before returning to the main menu?");

        ref Position position = ref _confirmationPanel.GetComponent<Position>();
        position.Value = _screen / 2;
        confirmation.Visible = true;
        _confirmationPanel.AddTag<Moved>();
    }

    private void HideConfirmation()
    {
        if (_confirmationPanel.IsNull)
            return;

        ref SceneConfirmation confirmation =
            ref _confirmationPanel.GetComponent<SceneConfirmation>();

        if (!confirmation.Visible)
            return;

        confirmation.Visible = false;
        ref Position position = ref _confirmationPanel.GetComponent<Position>();
        position.Value = HiddenConfirmation;
        _confirmationPanel.AddTag<Moved>();
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
