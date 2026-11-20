using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Screens.Shared;
using static Xanadu.Core.Theme.UiMetrics;

namespace Xanadu.Screens.Results;

/// <summary>
/// Post-play results, as a spec tree: the title and level, a card of labelled
/// stats, then the actions along the bottom.
/// </summary>
public static class ResultsScene
{
    private const float CardWidth = 760;
    private const float ActionWidth = 220;
    private const float ActionHeight = 52;

    public static void Build(
        EntityStore entities,
        Entity root,
        PlayResults results,
        IVisualItemFactory? visuals = null,
        bool canWatchReplay = false)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full);
        ui.Rect(SceneUi.BackgroundColor).Cover().Below();

        string level = string.IsNullOrWhiteSpace(results.LevelName) ? "Unknown level" : results.LevelName;
        string rank = results.Failed ? "FAIL" : ScoreRules.RankFor(results.Accuracy);

        ui.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label(ResultsStrings.Title, UiType.Display, ink: Ink.Accent),
            new Label(level, UiType.Section, ink: Ink.Dim),
            new Spacer(),
            new Column(width: CardWidth, pad: Space.Xxl, gap: Space.Sm, cross: Cross.Center, tone: Tone.Solid)
            {
                Stat(ResultsStrings.Accuracy, $"{results.Accuracy * 100:0.00}%", UiType.Display, Ink.Accent),
                new Label(rank, UiType.Section)
                {
                    Style = results.Failed ? n => n.TextColor(n.Roles.Danger.Fill) : null,
                },
                Stat(ResultsStrings.NotesHit, $"{results.NotesHit} / {results.TotalNotes}"),
                Stat(ResultsStrings.MaxCombo, results.MaxCombo.ToString()),
                Stat(ResultsStrings.Score, results.Score.ToString("0000000")),
                new Label("JUDGEMENTS", UiType.Caption, ink: Ink.Dim),
                new Label(
                    $"FLAWLESS+ {results.FlawlessPCount}   FLAWLESS {results.FlawlessCount}   " +
                    $"CLEAN {results.CleanCount}   FAIR {results.FairCount}",
                    UiType.Small),
                new Label(
                    $"DEFICIENT {results.DeficientCount}   TERRIBLE {results.TerribleCount}   " +
                    $"MISS {results.MissCount}",
                    UiType.Small),
            },
            new Spacer(),
            canWatchReplay
                ? Action(ResultsStrings.WatchReplay, Tone.Quiet, SceneActionId.WatchReplay)
                : null,
            new Row(gap: Space.Lg)
            {
                Action(ResultsStrings.Retry, Tone.Primary, SceneActionId.Retry),
                Action(ResultsStrings.SongSelect, Tone.Secondary, SceneActionId.SongSelect),
                Action(ResultsStrings.MainMenu, Tone.Secondary, SceneActionId.MainMenu),
            });
    }

    /// <summary>A stat's caption over its value.</summary>
    private static Column Stat(LString label, string value, int type = UiType.Title, Ink ink = Ink.Normal) =>
        new(cross: Cross.Center)
        {
            new Label(label, UiType.Caption, ink: Ink.Dim),
            new Label(value, type, ink: ink),
        };

    private static Button Action(LString label, Tone tone, SceneActionId action) =>
        new(label, tone: tone, width: ActionWidth, height: ActionHeight) { Style = n => n.On(action) };
}
