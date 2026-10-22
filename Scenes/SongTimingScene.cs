using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;

namespace Writer.Scenes;

/// <summary>A separate screen for editing shared song timing.</summary>
public static class SongTimingScene
{
    public static Entity Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        SongDocument song,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 70),
            new Vector2(900, 54),
            "SONG TIMING",
            40,
            SceneUi.AccentColor);

        Entity title = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 140),
            new Vector2(900, 34),
            song.Title,
            24,
            SceneUi.TextColor);

        Entity bpm = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 250),
            new Vector2(500, 50),
            $"BPM {song.Bpm}",
            28,
            SceneUi.TextColor);
        Entity offset = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 330),
            new Vector2(500, 50),
            $"OFFSET {song.OffsetMs} MS",
            28,
            SceneUi.TextColor);

        AddAdjust(ui, new Vector2(screen.X / 2 - 180, 250),
            "-1 BPM", SongTimingAdjustKind.Bpm, -1);
        AddAdjust(ui, new Vector2(screen.X / 2 + 180, 250),
            "+1 BPM", SongTimingAdjustKind.Bpm, 1);
        AddAdjust(ui, new Vector2(screen.X / 2 - 180, 330),
            "-10 MS", SongTimingAdjustKind.Offset, -10);
        AddAdjust(ui, new Vector2(screen.X / 2 + 180, 330),
            "+10 MS", SongTimingAdjustKind.Offset, 10);

        Entity save = ui.RectEntity(
            new Vector2(screen.X / 2 - 110, 450),
            new Vector2(200, 46),
            SceneUi.ButtonColor,
            "SAVE",
            takesInput: true);
        save.AddComponent(new TextStyle(18, SceneUi.TextColor));
        save.AddComponent(ButtonVisual.Create(SceneUi.ButtonColor));
        save.AddComponent(new SongTimingSave());

        SceneUi.Button(
            ui,
            new Vector2(screen.X / 2 + 110, 450),
            new Vector2(200, 46),
            "BACK",
            SceneKind.Editor,
            18,
            SceneUi.MutedButtonColor);

        root.AddComponent(new SongTimingView(title, bpm, offset));
        return root;
    }

    private static void AddAdjust(
        UiBuilder ui,
        Vector2 center,
        string label,
        SongTimingAdjustKind kind,
        int delta)
    {
        Entity button = ui.RectEntity(
            center,
            new Vector2(150, 42),
            SceneUi.MutedButtonColor,
            label,
            takesInput: true);
        button.AddComponent(new TextStyle(16, SceneUi.TextColor));
        button.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        button.AddComponent(new SongTimingAdjust(kind, delta));
    }
}
