using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Ui;

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
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 64),
            new Vector2(900, 58),
            "SONG SELECT",
            40,
            SceneUi.AccentColor);

        SceneUi.Button(
            ui,
            new Vector2(100, 44),
            new Vector2(150, 44),
            "BACK",
            SceneKind.Main,
            18,
            SceneUi.MutedButtonColor);

        var cardCenter = new Vector2(screen.X / 2, screen.Y / 2 - 10);
        ui.RectEntity(cardCenter, new Vector2(780, 330), SceneUi.PanelColor);

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

        if (catalog.Count > 0)
        {
            Entity edit = SceneUi.Button(
                ui,
                cardCenter + new Vector2(0, 120),
                new Vector2(220, 42),
                "EDIT LEVEL",
                SceneKind.Editor,
                16,
                SceneUi.MutedButtonColor);
            edit.AddComponent(new EditLevelAction());

            SceneUi.Button(
                ui,
                new Vector2(screen.X / 2, screen.Y - 90),
                new Vector2(320, 64),
                "PLAY",
                SceneKind.Play,
                24);
        }

        var state = new SongSelectState(
            selected,
            catalog.Count,
            title,
            meta,
            stats,
            index);

        Refresh(state, catalog, selected);
        return state;
    }

    public static void Refresh(
        SongSelectState state,
        IReadOnlyList<LevelEntry> catalog,
        int selected)
    {
        if (catalog.Count == 0)
        {
            SceneUi.SetText(state.Title, "NO LEVELS FOUND");
            SceneUi.SetText(state.Meta, "CREATE A LEVEL IN THE EDITOR");
            SceneUi.SetText(state.Stats, "");
            SceneUi.SetText(state.Index, "0 / 0");
            return;
        }

        selected = Mathf.Clamp(selected, 0, catalog.Count - 1);
        LevelEntry entry = catalog[selected];
        LevelDocument document = entry.Document;

        string artist = string.IsNullOrWhiteSpace(document.Artist)
            ? "Unknown artist"
            : document.Artist;

        string difficulty = string.IsNullOrWhiteSpace(document.Difficulty)
            ? "Unrated"
            : document.Difficulty;

        SceneUi.SetText(
            state.Title,
            string.IsNullOrWhiteSpace(document.Name) ? "UNTITLED" : document.Name);
        SceneUi.SetText(state.Meta, $"{artist}   ·   {difficulty}");
        SceneUi.SetText(
            state.Stats,
            $"{document.Bpm} BPM   ·   {document.Notes.Count} notes");
        SceneUi.SetText(state.Index, $"{selected + 1} / {catalog.Count}");
    }
}
