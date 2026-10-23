using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Strings;
using Writer.Levels;
using Writer.Profiles;

namespace Writer.Scenes;

/// <summary>Carousel of saved levels with play and back actions.</summary>
public static class SongSelectScene
{
    public static SongSelectState Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        IReadOnlyList<LevelEntry> catalog,
        int selected,
        ProfileStore profiles,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 64),
            new Vector2(900, 58),
            LocalizedText.Get(SongSelectStrings.Title),
            40,
            SceneUi.AccentColor);

        SceneUi.Button(
            ui,
            new Vector2(100, 44),
            new Vector2(150, 44),
            LocalizedText.Get(SongSelectStrings.Back),
            SceneKind.Main,
            18,
            SceneUi.MutedButtonColor);

        Vector2 profileBox = new(screen.X - 154, 44);
        ui.RectEntity(profileBox, new Vector2(260, 64), SceneUi.PanelColor);
        Texture2D? star = visuals is GodotVisualRidFactory
            ? LoadStarTexture()
            : null;
        if (star is not null)
        {
            ui.ImageEntity(
                profileBox + new Vector2(-92, 0),
                new Vector2(36, 36),
                star);
        }
        else
        {
            SceneUi.Text(
                ui,
                profileBox + new Vector2(-92, 0),
                new Vector2(36, 36),
                "★",
                26,
                SceneUi.AccentColor,
                SceneUi.PanelColor);
        }

        SceneUi.Text(
            ui,
            profileBox + new Vector2(24, 0),
            new Vector2(170, 24),
            profiles.ActiveProfile?.Name ??
                LocalizedText.Get(SongSelectStrings.NoProfile),
            16,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        var cardCenter = new Vector2(screen.X / 2, screen.Y / 2 - 10);
        SceneUi.Panel(ui, cardCenter, new Vector2(780, 330));

        Entity title = SceneUi.Text(
            ui,
            cardCenter + new Vector2(0, -100),
            new Vector2(720, 54),
            "",
            32,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        Entity meta = SceneUi.Text(
            ui,
            cardCenter + new Vector2(0, -42),
            new Vector2(720, 28),
            "",
            18,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        Entity stats = SceneUi.Text(
            ui,
            cardCenter + new Vector2(0, 2),
            new Vector2(720, 26),
            "",
            16,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        Entity index = SceneUi.Text(
            ui,
            cardCenter + new Vector2(0, 48),
            new Vector2(720, 24),
            "",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);


        UiBuilder scoreUi = SceneUi.Layer(
            entities,
            root,
            SceneUi.MenuLayer,
            visuals);
        float scoreWidth = 360;
        float scoreHeight = 330;
        Vector2 scoreCenter = new(
            screen.X - scoreWidth / 2 - 28,
            cardCenter.Y);
        SceneUi.Panel(scoreUi, scoreCenter, new Vector2(scoreWidth, scoreHeight));
        SceneUi.Text(
            scoreUi,
            new Vector2(scoreCenter.X, scoreCenter.Y - scoreHeight / 2 + 24),
            new Vector2(scoreWidth - 30, 24),
            LocalizedText.Get(SongSelectStrings.Scores),
            18,
            SceneUi.AccentColor,
            SceneUi.PanelColor);

        const int maxScoreRows = 24;
        const float scoreRowHeight = 28;
        Entity scoreViewport = SceneUi.ScrollView(
            scoreUi,
            new Vector2(scoreCenter.X, scoreCenter.Y + 10),
            new Vector2(scoreWidth - 24, scoreHeight - 70),
            maxScoreRows * (scoreRowHeight + 2),
            out Entity scoreContent);
        var scoreRows = new List<Entity>();
        for (int i = 0; i < maxScoreRows; i++)
        {
            scoreRows.Add(SceneUi.Text(
                scoreUi,
                Vector2.Zero,
                new Vector2(scoreWidth - 40, scoreRowHeight - 2),
                "",
                14,
                SceneUi.TextColor,
                new Color(0, 0, 0, 0)));
        }

        if (catalog.Count > 1)
        {
            SceneUi.CarouselButton(
                ui,
                cardCenter + new Vector2(-480, 0),
                new Vector2(64, 64),
                "<",
                -1);

            SceneUi.CarouselButton(
                ui,
                cardCenter + new Vector2(480, 0),
                new Vector2(64, 64),
                ">",
                1);
        }

        bool blocked = catalog.Count > 0 &&
            catalog[Mathf.Clamp(selected, 0, catalog.Count - 1)].IsBlocked;
        if (catalog.Count > 0 && !blocked)
        {
            Entity edit = SceneUi.ButtonEntity(
                ui,
                cardCenter + new Vector2(0, 120),
                new Vector2(220, 42),
                LocalizedText.Get(SongSelectStrings.EditLevel),
                16,
                SceneUi.MutedButtonColor);
            edit.AddComponent(new SceneCommand(SceneActionId.Edit));

            Entity play = SceneUi.ButtonEntity(
                ui,
                new Vector2(screen.X / 2, screen.Y - 90),
                new Vector2(320, 64),
                LocalizedText.Get(SongSelectStrings.Play),
                24);
            play.AddComponent(new SceneCommand(SceneActionId.Play));
        }

        var state = new SongSelectState(
            selected,
            catalog.Count,
            title,
            meta,
            stats,
            index,
            scoreViewport,
            scoreContent,
            scoreRows);

        Refresh(state, catalog, selected, profiles);
        return state;
    }

    public static void Refresh(
        SongSelectState state,
        IReadOnlyList<LevelEntry> catalog,
        int selected,
        ProfileStore profiles)
    {
        if (catalog.Count == 0)
        {
            SceneUi.SetText(state.Title, LocalizedText.Get(SongSelectStrings.NoLevels));
            SceneUi.SetText(state.Meta, LocalizedText.Get(SongSelectStrings.CreateLevel));
            SceneUi.SetText(state.Stats, "");
            SceneUi.SetText(state.Index, "0 / 0");
            ClearScores(state);
            return;
        }

        selected = Mathf.Clamp(selected, 0, catalog.Count - 1);
        LevelEntry entry = catalog[selected];
        LevelDocument document = entry.Document;
        SongDocument? song = entry.Song;
        string artist = song?.Artist ?? "";
        string difficulty = document.Name;
        string title = song?.Title ?? document.Name;

        SceneUi.SetText(
            state.Title,
            string.IsNullOrWhiteSpace(document.Name)
                ? LocalizedText.Get(SongSelectStrings.Untitled)
                : document.Name);
        SceneUi.SetText(
            state.Meta,
            string.IsNullOrWhiteSpace(title)
                ? "Missing song"
                : $"{title}   ·   {(string.IsNullOrWhiteSpace(artist) ? "Unknown artist" : artist)}");
        SceneUi.SetText(
            state.Stats,
            entry.IsBlocked
                ? "SONG MISSING — cannot play or edit"
                : $"{song?.Bpm ?? 120} BPM   ·   {document.Notes.Count} notes");
        SceneUi.SetText(state.Index, $"{selected + 1} / {catalog.Count}");

        RefreshScores(state, profiles.AllScores(entry.Path));
    }

    private static void ClearScores(SongSelectState state)
    {
        foreach (Entity row in state.ScoreRows)
            SceneUi.SetText(row, "");
    }

    private static void RefreshScores(
        SongSelectState state,
        IReadOnlyList<ScoreEntry> scores)
    {
        for (int i = 0; i < state.ScoreRows.Count; i++)
        {
            SceneUi.SetText(
                state.ScoreRows[i],
                i < scores.Count
                    ? $"{scores[i].ProfileName}   {scores[i].Record.Score:0000000}"
                    : "");
        }
    }

    private static Texture2D? LoadStarTexture()
    {
        try
        {
            string svg = FileAccess.GetFileAsString("res://Assets/star.svg");
            if (string.IsNullOrEmpty(svg))
                return null;

            var image = new Image();
            image.LoadSvgFromString(svg, 2f);
            return ImageTexture.CreateFromImage(image);
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
