using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// The global offset calibration wizard: a panel that plays a click track and
/// averages the player's taps. The scene builds the handles; the system drives
/// the run and the labels.
/// </summary>
public static class OffsetWizardScene
{
    private const float PanelWidth = 780;
    private const float PanelHeight = 470;
    private const float TextWidth = 680;
    private const float InstructionsHeight = 30;
    private const float OffsetHeight = 48;
    private const float StatusHeight = 48;
    private const float ActionWidth = 180;
    private const float ActionHeight = 46;

    public static void Build(
        EntityStore entities,
        Entity root,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(new Color(0.01f, 0.02f, 0.04f, 0.82f)).Cover().Below().Input();

        UiNode panel = ui.Column()
            .Size(PanelWidth, PanelHeight)
            .At(stage.Center)
            .Fill(new Color(0.035f, 0.05f, 0.09f, 0.98f))
            .Pad(UiMetrics.Space.Xl)
            .Gap(UiMetrics.Space.Md)
            .Main(StackMainAlignment.Center)
            .Cross(StackCrossAlignment.Center);

        panel.Text(
                LocalizedText.Get(OffsetWizardStrings.Title),
                UiType.Heading,
                SceneUi.AccentColor,
                TextAlign.Center)
            .Bold();
        panel.Text(
                "Listen to the eight clicks and tap SPACE exactly with each one.",
                UiType.Body,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(TextWidth, InstructionsHeight);
        panel.Text(
                "The average timing error becomes your global offset.",
                UiType.Body,
                SceneUi.DimTextColor,
                TextAlign.Center)
            .Size(TextWidth, InstructionsHeight);

        UiNode offset = panel.Text(
                "", UiType.Section, SceneUi.TextColor, TextAlign.Center)
            .Size(TextWidth, OffsetHeight)
            .Bold();
        UiNode status = panel.Text(
                "", UiType.Body, SceneUi.DimTextColor, TextAlign.Center)
            .Size(TextWidth, StatusHeight);

        UiNode actions = panel.Row().Gap(UiMetrics.Space.Xxl);
        UiNode start = actions.Button(
                "START", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight);
        UiNode save = actions.Button(
                "SAVE", UiType.Label, SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight);
        actions.Button("BACK", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Back);

        UiNode reset = panel.Button(
                "RESET OFFSET", UiType.Label, SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight);

        panel.With(new OffsetWizard(offset, status, start, save, reset));
    }
}
