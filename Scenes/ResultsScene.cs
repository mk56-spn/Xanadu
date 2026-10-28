using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Scoring;

namespace Xanadu.Scenes;

/// <summary>Post-play results with score, accuracy, max combo, and hit ratio.</summary>
public static class ResultsScene
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        PlayResults results,
        IVisualRidFactory? visuals = null,
        bool canWatchReplay = false)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 95),
            new Vector2(900, 64),
            LocalizedText.Get(ResultsStrings.Title),
            48,
            SceneUi.AccentColor);

        string levelName = string.IsNullOrWhiteSpace(results.LevelName)
            ? "Unknown level"
            : results.LevelName;

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 152),
            new Vector2(900, 30),
            levelName,
            22,
            SceneUi.DimTextColor);

        float centerX = screen.X / 2;
        float centerY = screen.Y / 2 + 60;
        const float panelWidth = 760;
        const float panelHeight = 720;
        float panelTop = centerY - panelHeight / 2;

        SceneUi.Panel(
            ui,
            new Vector2(centerX, centerY),
            new Vector2(panelWidth, panelHeight));

        float y = panelTop + 45;

        SceneUi.Label(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.Accuracy),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 55),
            new Vector2(panelWidth - 80, 72),
            $"{results.Accuracy * 100:0.00}%",
            56,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 108),
            new Vector2(panelWidth - 80, 30),
            results.Failed ? "FAIL" : ScoreRules.RankFor(results.Accuracy),
            26,
            results.Failed ? SceneUi.DangerColor : SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 165;

        SceneUi.Label(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.NotesHit),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            $"{results.NotesHit} / {results.TotalNotes}",
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 125;

        SceneUi.Label(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.MaxCombo),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            results.MaxCombo.ToString(),
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 125;

        SceneUi.Label(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.Score),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            results.Score.ToString("0000000"),
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 112),
            new Vector2(panelWidth - 80, 24),
            "JUDGEMENTS",
            16,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 140),
            new Vector2(panelWidth - 40, 28),
            $"FLAWLESS+ {results.FlawlessPCount}   FLAWLESS {results.FlawlessCount}   " +
            $"CLEAN {results.CleanCount}   FAIR {results.FairCount}",
            14,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        SceneUi.Label(
            ui,
            new Vector2(centerX, y + 166),
            new Vector2(panelWidth - 40, 28),
            $"DEFICIENT {results.DeficientCount}   TERRIBLE {results.TerribleCount}   " +
            $"MISS {results.MissCount}",
            14,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        if (canWatchReplay)
        {
            Entity watch = SceneUi.ButtonEntity(
                ui,
                new Vector2(centerX, screen.Y - 140),
                new Vector2(220, 44),
                LocalizedText.Get(ResultsStrings.WatchReplay),
                16,
                SceneUi.MutedButtonColor);
            watch.AddComponent(new SceneCommand(SceneActionId.WatchReplay));
        }

        Entity retry = SceneUi.ButtonEntity(
            ui,
            new Vector2(centerX - 240, screen.Y - 70),
            new Vector2(220, 52),
            LocalizedText.Get(ResultsStrings.Retry));
        retry.AddComponent(new SceneCommand(SceneActionId.Retry));

        Entity songs = SceneUi.ButtonEntity(
            ui,
            new Vector2(centerX, screen.Y - 70),
            new Vector2(220, 52),
            LocalizedText.Get(ResultsStrings.SongSelect),
            18,
            SceneUi.MutedButtonColor);
        songs.AddComponent(new SceneCommand(SceneActionId.SongSelect));

        Entity main = SceneUi.ButtonEntity(
            ui,
            new Vector2(centerX + 240, screen.Y - 70),
            new Vector2(220, 52),
            LocalizedText.Get(ResultsStrings.MainMenu),
            18,
            SceneUi.MutedButtonColor);
        main.AddComponent(new SceneCommand(SceneActionId.MainMenu));
    }
}
