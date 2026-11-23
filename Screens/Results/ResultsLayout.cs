using Xanadu.Core.Theme;
using Xanadu.Logic.Results;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Results;

/// <summary>Post-play results: the title and level, a card of labelled stats, then the actions along the bottom.</summary>
public sealed class ResultsLayout : IScreenLayout<ResultsLogic>
{
    private const float CardWidth = 760;
    private const float ActionWidth = 220;
    private const float ActionHeight = 52;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, ResultsLogic logic)
    {
        layer.Rect(SceneUi.BackgroundColor).Cover().Below();
        var results = logic.Results;

        layer.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label(ResultsStrings.Title, UiType.Display, ink: Ink.Accent),
            new Label(logic.LevelName, UiType.Section, ink: Ink.Dim),
            new Spacer(),
            new Column(width: CardWidth, pad: Space.Xxl, gap: Space.Sm, cross: Cross.Center, tone: Tone.Solid)
            {
                Stat(ResultsStrings.Accuracy, $"{results.Accuracy * 100:0.00}%", UiType.Display, Ink.Accent),
                new Label(logic.Rank, UiType.Section)
                {
                    Style = logic.Failed ? n => n.TextColor(n.Roles.Danger.Fill) : null,
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
            logic.CanWatchReplay
                ? Action(ResultsStrings.WatchReplay, Tone.Quiet, ResultsEntrance.WatchReplay)
                : null,
            new Row(gap: Space.Lg)
            {
                Action(ResultsStrings.Retry, Tone.Primary, ResultsEntrance.Retry),
                Action(ResultsStrings.SongSelect, Tone.Secondary, ResultsEntrance.SongSelect),
                Action(ResultsStrings.MainMenu, Tone.Secondary, ResultsEntrance.MainMenu),
            });
    }

    /// <summary>A stat's caption over its value.</summary>
    private static Column Stat(LString label, string value, int type = UiType.Title, Ink ink = Ink.Normal) =>
        new(cross: Cross.Center)
        {
            new Label(label, UiType.Caption, ink: Ink.Dim),
            new Label(value, type, ink: ink),
        };

    private static Button Action(LString label, Tone tone, ResultsEntrance entrance) =>
        new(label, tone: tone, cmd: Cmd(entrance), width: ActionWidth, height: ActionHeight);
}
