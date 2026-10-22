using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Levels;
using Writer.Settings;
using Writer.Systems.Playfields;
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
        KeybindSettings keybinds,
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
            new PlayfieldMode(PlayfieldKind.Play),
            new Player(Player.DefaultSize),
            new Position(new Vector2(
                origin.X + fieldWidth / 2,
                origin.Y + fieldHeight / 2)),
            new Character(1f),
            new Momentum(System.Numerics.Vector2.Zero));

        root.AddChild(playfieldEntity);
        playfieldEntity.AddComponent(new KeyTrail());
        var keyLabels = new string[KeybindSettings.Actions.Length];
        for (int i = 0; i < keyLabels.Length; i++)
        {
            keyLabels[i] = KeybindSettings.KeyLabel(
                keybinds.Get(KeybindSettings.Actions[i]));
        }
        playfieldEntity.AddComponent(new KeyTrailLabels(keyLabels));

        LevelDocumentMapper.Apply(entities, playfieldEntity, document);

        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.PlayHudLayer, visuals);

        Entity pause = ui.RectEntity(
            new Vector2(95, 44),
            new Vector2(150, 44),
            SceneUi.MutedButtonColor,
            "PAUSE",
            takesInput: true);

        pause.AddComponent(new TextStyle(18, SceneUi.TextColor));
        pause.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
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

        Entity scoreText = SceneUi.Text(
            ui,
            new Vector2(160, screen.Y - 24),
            new Vector2(280, 24),
            "SCORE 0000000",
            18,
            SceneUi.TextColor);

        Entity comboText = SceneUi.Text(
            ui,
            new Vector2(screen.X - 150, screen.Y - 24),
            new Vector2(220, 24),
            "COMBO 0",
            16,
            SceneUi.DimTextColor);

        Entity accuracyText = SceneUi.Text(
            ui,
            new Vector2(screen.X - 150, 68),
            new Vector2(220, 24),
            "ACC 0.00%  FAIL",
            14,
            SceneUi.DimTextColor);

        Entity judgementText = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y / 2 - 120),
            new Vector2(520, 44),
            "",
            28,
            SceneUi.AccentColor,
            new Color(0, 0, 0, 0));

        float counterX = screen.X - 150;
        SceneUi.Text(
            ui,
            new Vector2(counterX, 190),
            new Vector2(260, 22),
            "JUDGEMENTS",
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));

        CounterText(ui, screen.X - 200, 220, "FLAWLESS+",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.FlawlessP),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 246, "FLAWLESS",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Flawless),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 272, "CLEAN",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Clean),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 298, "FAIR",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Fair),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 324, "DEFICIENT",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Deficient),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 350, "TERRIBLE",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Terrible),
            TextAlign.Left);
        CounterText(ui, screen.X - 200, 376, "MISS",
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Miss),
            TextAlign.Left);

        Entity flawlessPlus = CounterNumber(ui, screen.X - 65, 220,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.FlawlessP));
        Entity flawless = CounterNumber(ui, screen.X - 65, 246,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Flawless));
        Entity clean = CounterNumber(ui, screen.X - 65, 272,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Clean));
        Entity fair = CounterNumber(ui, screen.X - 65, 298,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Fair));
        Entity deficient = CounterNumber(ui, screen.X - 65, 324,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Deficient));
        Entity terrible = CounterNumber(ui, screen.X - 65, 350,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Terrible));
        Entity miss = CounterNumber(ui, screen.X - 65, 376,
            PlayScoreSystem.ColorFor(Writer.Scoring.Judgement.Miss));

        Entity countdown = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y / 2),
            new Vector2(700, 180),
            "",
            120,
            SceneUi.AccentColor,
            new Color(0, 0, 0, 0));

        playfieldEntity.AddComponent(new JudgementCounter(
            flawlessPlus,
            flawless,
            clean,
            fair,
            deficient,
            terrible,
            miss));

        playfieldEntity.AddComponent(
            new ScoreState(
                scoreText,
                comboText,
                accuracyText,
                judgementText,
                document.Notes.Count));
        playfieldEntity.AddComponent(new PlaySession(
            status,
            countdown,
            document.Notes.Count > 0
                ? document.Notes.Max(note => note.TimeMs)
                : -1));
    }

    private static Entity CounterText(
        UiBuilder ui,
        float x,
        float y,
        string text,
        Color color,
        TextAlign align)
    {
        Entity entity = SceneUi.Text(
            ui,
            new Vector2(x, y),
            new Vector2(160, 22),
            text,
            13,
            color,
            new Color(0, 0, 0, 0));
        entity.AddComponent(new TextAlignment(align));
        return entity;
    }

    private static Entity CounterNumber(
        UiBuilder ui,
        float x,
        float y,
        Color color)
    {
        Entity entity = SceneUi.Text(
            ui,
            new Vector2(x, y),
            new Vector2(90, 22),
            "0",
            13,
            color,
            new Color(0, 0, 0, 0));
        entity.AddComponent(new TextAlignment(TextAlign.Right));
        return entity;
    }
}
