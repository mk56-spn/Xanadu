using Xanadu.Logic.Screens;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Scoring;

namespace Xanadu.Logic.Results;

/// <summary>The results screen's ways out.</summary>
public enum ResultsEntrance
{
    Retry,
    SongSelect,
    MainMenu,
    WatchReplay,
}

/// <summary>What the results screen asks of play: another go, or the run just played back.</summary>
public interface IPlayAgain
{
    void Retry();
    void WatchLastReplay();
}

/// <summary>The run just finished, as figures, and where to go next.</summary>
public sealed class ResultsLogic(PlayResults results, bool canWatchReplay, IScreenNavigator navigator, IPlayAgain play)
    : IScreenLogic<ResultsEntrance>
{
    public ScreenContract<ResultsEntrance> Contract { get; } = new ScreenContract<ResultsEntrance>()
        .Mandatory(ResultsEntrance.Retry)
        .Mandatory(ResultsEntrance.SongSelect)
        .Optional(ResultsEntrance.MainMenu)
        .Optional(ResultsEntrance.WatchReplay);

    public PlayResults Results => results;

    public string LevelName => string.IsNullOrWhiteSpace(results.LevelName) ? "Unknown level" : results.LevelName;

    public bool Failed => results.Failed;

    public string Rank => results.Failed ? "FAIL" : ScoreRules.RankFor(results.Accuracy);

    /// <summary>Whether this run was recorded, so <see cref="ResultsEntrance.WatchReplay"/> does something.</summary>
    public bool CanWatchReplay => canWatchReplay;

    public void Do(ResultsEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case ResultsEntrance.Retry:
                play.Retry();
                break;
            case ResultsEntrance.WatchReplay when canWatchReplay:
                play.WatchLastReplay();
                break;
            case ResultsEntrance.SongSelect:
                navigator.Go(SceneKind.SongSelect);
                break;
            case ResultsEntrance.MainMenu:
                navigator.Go(SceneKind.Main);
                break;
        }
    }
}
