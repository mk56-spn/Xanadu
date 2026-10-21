using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Levels;
using Writer.Ui;

namespace Writer.Scenes;

/// <summary>Reuses the editor playfield to play a selected level.</summary>
public static class PlayScene
{
    private const float TopInset = 92;
    private const float BottomInset = 54;
    private const float SideInset = 40;

    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        LevelEntry entry,
        IVisualRidFactory? visuals = null)
    {
        LevelDocument document = entry.Document;

        float logicalSize = Mathf.Max(1, Mathf.Max(
            document.PlayfieldWidth,
            document.PlayfieldHeight));
        float available = Mathf.Min(
            screen.X - SideInset * 2,
            screen.Y - TopInset - BottomInset);
        float renderedSize = Mathf.Min(logicalSize, Mathf.Max(1, available));
        float scale = renderedSize / logicalSize;

        float fieldWidth = document.PlayfieldWidth * scale;
        float fieldHeight = document.PlayfieldHeight * scale;
        var origin = new Vector2(
            (screen.X - fieldWidth) / 2,
            TopInset + (screen.Y - TopInset - BottomInset - fieldHeight) / 2);

        Entity playfieldEntity = entities.CreateEntity(
            new LevelInfo(document.Name, document.AudioPath),
            new LevelFile(entry.Path),
            new Playfield(document.PlayfieldWidth, document.PlayfieldHeight),
            new Playhead(
                timeMs: 0,
                bpm: document.Bpm,
                offsetMs: document.OffsetMs,
                playing: false),
            new PlayfieldView(origin, scale, screen),
            new PlayfieldMode(PlayfieldKind.Play));

        root.AddChild(playfieldEntity);

        LevelDocumentMapper.Apply(entities, playfieldEntity, document);

        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.PlayHudLayer, visuals);

        Entity pause = ui.RectEntity(
            new Vector2(95, 44),
            new Vector2(150, 44),
            SceneUi.MutedButtonColor,
            "PAUSE",
            takesInput: true);

        pause.AddComponent(new TextStyle(18, SceneUi.TextColor));
        pause.AddComponent(new PauseAction());

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 30),
            new Vector2(620, 30),
            string.IsNullOrWhiteSpace(document.Name) ? "UNTITLED" : document.Name,
            20,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        string artist = string.IsNullOrWhiteSpace(document.Artist)
            ? "Unknown artist"
            : document.Artist;
        string difficulty = string.IsNullOrWhiteSpace(document.Difficulty)
            ? "Unrated"
            : document.Difficulty;

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 58),
            new Vector2(620, 22),
            $"{artist}   ·   {difficulty}",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        Entity status = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y - 24),
            new Vector2(760, 24),
            "LOADING...",
            16,
            SceneUi.DimTextColor);

        playfieldEntity.AddComponent(new PlaySession(status));
    }
}
