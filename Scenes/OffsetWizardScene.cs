using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>Background scene for the native offset calibration wizard.</summary>
public static class OffsetWizardScene
{
    private const float TitleY = 90;
    private const float HintY = 140;
    private const float BackWidth = 150;

    public static void Build(
        EntityStore entities,
        Entity root,
        IVisualRidFactory? visuals = null)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        ui.Text(
                LocalizedText.Get(OffsetWizardStrings.Title),
                UiType.Title,
                SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));
        ui.Text("Tap SPACE with every click.", UiType.Label, SceneUi.DimTextColor)
            .At(new Vector2(stage.Center.X, HintY));

        ui.Button(
                LocalizedText.Get(OffsetWizardStrings.Back),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(BackWidth, UiMetrics.Control.Lg)
            .At(new Vector2(
                UiMetrics.Space.Xl + BackWidth / 2,
                UiMetrics.Space.Xl + UiMetrics.Control.Lg / 2))
            .On(SceneKind.Main);
    }
}
