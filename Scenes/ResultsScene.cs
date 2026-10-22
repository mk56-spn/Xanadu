using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Strings;
using Writer.Scoring;

namespace Writer.Scenes;

/// <summary>Post-play results with score, accuracy, max combo, and hit ratio.</summary>
public static class ResultsScene
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        PlayResults results,
        IVisualRidFactory? visuals = null)
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

        ui.RectEntity(
            new Vector2(centerX, centerY),
            new Vector2(panelWidth, panelHeight),
            SceneUi.PanelColor);

        float y = panelTop + 45;

        SceneUi.Text(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.Accuracy),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(centerX, y + 55),
            new Vector2(panelWidth - 80, 72),
            $"{results.Accuracy * 100:0.00}%",
            56,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(centerX, y + 108),
            new Vector2(panelWidth - 80, 30),
            ScoreRules.RankFor(results.Accuracy),
            26,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 165;

        SceneUi.Text(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.NotesHit),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            $"{results.NotesHit} / {results.TotalNotes}",
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 125;

        SceneUi.Text(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.MaxCombo),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            results.MaxCombo.ToString(),
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        y += 125;

        SceneUi.Text(
            ui,
            new Vector2(centerX, y),
            new Vector2(panelWidth - 80, 28),
            LocalizedText.Get(ResultsStrings.Score),
            18,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        SceneUi.Text(
            ui,
            new Vector2(centerX, y + 48),
            new Vector2(panelWidth - 80, 56),
            results.Score.ToString("0000000"),
            48,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        Entity retry = ui.RectEntity(
            new Vector2(centerX - 240, screen.Y - 70),
            new Vector2(220, 52),
            SceneUi.ButtonColor,
            LocalizedText.Get(ResultsStrings.Retry),
            takesInput: true);
        retry.AddComponent(new TextStyle(18, SceneUi.TextColor));
        retry.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        retry.AddComponent(new RetryAction());

        SceneUi.Button(
            ui,
            new Vector2(centerX, screen.Y - 70),
            new Vector2(220, 52),
            LocalizedText.Get(ResultsStrings.SongSelect),
            SceneKind.SongSelect,
            18,
            SceneUi.MutedButtonColor);

        SceneUi.Button(
            ui,
            new Vector2(centerX + 240, screen.Y - 70),
            new Vector2(220, 52),
            LocalizedText.Get(ResultsStrings.MainMenu),
            SceneKind.Main,
            18,
            SceneUi.MutedButtonColor);
    }
}
