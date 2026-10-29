using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scoring;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Post-play results: the title, then a panel of labelled stats centred in the
/// card, with the actions along the bottom edge of the stage.
/// </summary>
public static class ResultsScene
{
    private const float TitleY = 95;
    private const float LevelY = 152;

    private const float PanelWidth = 760;
    private const float PanelHeight = 720;

    /// <summary>Where the stats card sits, below the stage's middle.</summary>
    private const float PanelCenterY = 600;

    private const float ActionWidth = 220;
    private const float ActionHeight = 52;
    private const float ActionGap = 20;
    private const float ActionBottom = 70;
    private const float ReplayBottom = 140;

    public static void Build(
        EntityStore entities,
        Entity root,
        PlayResults results,
        IVisualRidFactory? visuals = null,
        bool canWatchReplay = false)
    {
        UiStage stage = UiStage.Full;
        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);

        ui.Rect(SceneUi.BackgroundColor).Cover().Below();
        ui.Text(LocalizedText.Get(ResultsStrings.Title), UiType.Display, SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));
        ui.Text(
                string.IsNullOrWhiteSpace(results.LevelName)
                    ? "Unknown level"
                    : results.LevelName,
                UiType.Section,
                SceneUi.DimTextColor)
            .At(new Vector2(stage.Center.X, LevelY));

        UiNode card = ui.Panel()
            .Size(PanelWidth, PanelHeight)
            .At(new Vector2(stage.Center.X, PanelCenterY))
            .Main(StackMainAlignment.Center);

        Stat(card, ResultsStrings.Accuracy, $"{results.Accuracy * 100:0.00}%",
            UiType.Display, SceneUi.AccentColor);
        card.Text(
            results.Failed ? "FAIL" : ScoreRules.RankFor(results.Accuracy),
            UiType.Section,
            results.Failed ? SceneUi.DangerColor : SceneUi.TextColor);

        Stat(card, ResultsStrings.NotesHit,
            $"{results.NotesHit} / {results.TotalNotes}",
            UiType.Title, SceneUi.TextColor);
        Stat(card, ResultsStrings.MaxCombo,
            results.MaxCombo.ToString(),
            UiType.Title, SceneUi.TextColor);
        Stat(card, ResultsStrings.Score,
            results.Score.ToString("0000000"),
            UiType.Title, SceneUi.TextColor);

        card.Text("JUDGEMENTS", UiType.Caption, SceneUi.DimTextColor);
        card.Text(
            $"FLAWLESS+ {results.FlawlessPCount}   FLAWLESS {results.FlawlessCount}   " +
            $"CLEAN {results.CleanCount}   FAIR {results.FairCount}",
            UiType.Small,
            SceneUi.TextColor);
        card.Text(
            $"DEFICIENT {results.DeficientCount}   TERRIBLE {results.TerribleCount}   " +
            $"MISS {results.MissCount}",
            UiType.Small,
            SceneUi.TextColor);

        if (canWatchReplay)
        {
            ui.Button(
                    LocalizedText.Get(ResultsStrings.WatchReplay),
                    UiType.Body,
                    SceneUi.MutedButtonColor)
                .Size(ActionWidth, UiMetrics.Control.Lg)
                .At(new Vector2(stage.Center.X, stage.Height - ReplayBottom))
                .On(SceneActionId.WatchReplay);
        }

        UiNode actions = ui.Row()
            .Gap(ActionGap)
            .At(new Vector2(stage.Center.X, stage.Height - ActionBottom));

        actions.Button(
                LocalizedText.Get(ResultsStrings.Retry),
                UiType.Label,
                SceneUi.ButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.Retry);

        actions.Button(
                LocalizedText.Get(ResultsStrings.SongSelect),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.SongSelect);

        actions.Button(
                LocalizedText.Get(ResultsStrings.MainMenu),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(ActionWidth, ActionHeight)
            .On(SceneActionId.MainMenu);
    }

    private static void Stat(
        UiNode card,
        LString label,
        string value,
        int size,
        Color color)
    {
        card.Text(LocalizedText.Get(label), UiType.Caption, SceneUi.DimTextColor);
        card.Text(value, size, color);
    }
}
