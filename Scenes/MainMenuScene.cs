using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Title screen with play, editor and settings entry points. The buttons flow
/// in one column centered on the stage, so their pitch is a gap, not five
/// hand-placed coordinates.
/// </summary>
public static class MainMenuScene
{
    private const float TitleY = 140;
    private const float SubtitleY = 205;

    /// <summary>Centre of the button block.</summary>
    private const float MenuCenterY = 504;

    private const float MenuWidth = 360;
    private const float PlayHeight = 66;
    private const float MenuHeight = 56;
    private const float LastHeight = 50;
    private const float FooterInset = 40;

    public static void Build(
        EntityStore entities,
        Entity root,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        ui.Text(
                LocalizedText.Get(MainMenuStrings.Title),
                UiType.Display,
                SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));
        ui.Text(
                LocalizedText.Get(MainMenuStrings.Subtitle),
                UiType.Label,
                SceneUi.DimTextColor)
            .At(new Vector2(stage.Center.X, SubtitleY));

        UiNode menu = ui.Column()
            .Width(MenuWidth)
            .Gap(UiMetrics.Space.Md)
            .At(new Vector2(stage.Center.X, MenuCenterY));

        menu.Button(
                LocalizedText.Get(MainMenuStrings.Play),
                UiType.Section,
                SceneUi.ButtonColor)
            .Size(MenuWidth, PlayHeight)
            .On(SceneKind.SongSelect);

        menu.Button(
                LocalizedText.Get(MainMenuStrings.Editor),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(MenuWidth, MenuHeight)
            .On(SceneKind.Editor);

        menu.Button(
                LocalizedText.Get(MainMenuStrings.Settings),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(MenuWidth, MenuHeight)
            .On(SceneKind.Settings);

        menu.Button(
                LocalizedText.Get(MainMenuStrings.OffsetWizard),
                UiType.Small,
                SceneUi.MutedButtonColor)
            .Size(MenuWidth, LastHeight)
            .On(SceneKind.OffsetWizard);

        menu.Button(
                LocalizedText.Get(MainMenuStrings.Profiles),
                UiType.Small,
                SceneUi.MutedButtonColor)
            .Size(MenuWidth, LastHeight)
            .On(SceneKind.Profiles);

        ui.Text(
                LocalizedText.Get(MainMenuStrings.Footer),
                UiType.Small,
                SceneUi.DimTextColor)
            .At(new Vector2(stage.Center.X, stage.Height - FooterInset));
    }
}
