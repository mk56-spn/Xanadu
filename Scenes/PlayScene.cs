using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Scoring;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Scenes;

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
        IVisualRidFactory? visuals = null,
        StartPoint? start = null,
        Replay? replay = null,
        bool record = false)
    {
        LevelDocument document = entry.Document;
        SongDocument? song = entry.Song;

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
            new LevelInfo(document.Name, song?.AudioPath ?? ""),
            new LevelFile(entry.Path),
            new Playfield(document.PlayfieldWidth, document.PlayfieldHeight),
            new Playhead(
                timeMs: 0,
                bpm: song?.Bpm ?? 120,
                offsetMs: song?.OffsetMs ?? 0,
                playing: false),
            new PlayfieldView(origin, scale, screen),
            new PlayfieldMode(PlayfieldKind.Play),
            new Player(Player.ScaledSize(scale)),
            new Position(new Vector2(
                origin.X + fieldWidth / 2,
                origin.Y + fieldHeight / 2)),
            new Character(1f),
            new Momentum(System.Numerics.Vector2.Zero));

        playfieldEntity.AddComponent(LevelShaders.Create());
        root.AddChild(playfieldEntity);
        playfieldEntity.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfieldEntity.AddComponent(new ContactCounters());
        playfieldEntity.AddTag<DashMovement>();
        playfieldEntity.AddTag<ContactCharacter>();
        playfieldEntity.AddComponent(new KeyTrail());
        string[] keyLabels = new string[KeybindSettings.Actions.Length];
        for (int i = 0; i < keyLabels.Length; i++)
        {
            keyLabels[i] = KeybindSettings.KeyLabel(
                keybinds.Get(KeybindSettings.Actions[i]));
        }
        playfieldEntity.AddComponent(new KeyTrailLabels(keyLabels));

        LevelDocumentMapper.Apply(entities, playfieldEntity, document);

        if (start is StartPoint point)
        {
            ref Playhead playhead = ref playfieldEntity.GetComponent<Playhead>();
            playhead.TimeMs = point.TimeMs;
            playhead.SeekRequestMs = point.TimeMs;
            Vector2 spawn = playfieldEntity.TryGetComponent(out LevelGroups levelGroups)
                ? SpawnPosition(levelGroups, point)
                : point.Position;
            ref Position playerPosition = ref playfieldEntity.GetComponent<Position>();
            playerPosition.Value = playfieldEntity
                .GetComponent<PlayfieldView>()
                .PlayfieldToScreen(spawn);
            ref Momentum momentum = ref playfieldEntity.GetComponent<Momentum>();
            momentum.Value = point.Momentum;
        }

        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.PlayHudLayer, visuals);
        HudLayout hud = new();

        Entity pause = ui.RectEntity(
            new Vector2(95, 44),
            new Vector2(150, 44),
            SceneUi.MutedButtonColor,
            LocalizedText.Get(PlayStrings.Pause),
            takesInput: true);

        pause.AddComponent(new TextStyle(18, SceneUi.TextColor));
        pause.AddComponent(ButtonVisual.Create(SceneUi.MutedButtonColor));
        pause.AddComponent(new SceneCommand(SceneActionId.Pause));

        Entity titleText = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 30),
            new Vector2(620, 30),
            string.IsNullOrWhiteSpace(document.Name) ? "UNTITLED" : document.Name,
            20,
            SceneUi.TextColor,
            SceneUi.PanelColor);

        string artist = string.IsNullOrWhiteSpace(song?.Artist)
            ? "Unknown artist"
            : song.Artist;
        string difficulty = string.IsNullOrWhiteSpace(document.Name)
            ? "Unrated"
            : document.Name;

        Entity artistText = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 58),
            new Vector2(620, 22),
            $"{artist}   ·   {difficulty}",
            14,
            SceneUi.DimTextColor,
            SceneUi.PanelColor);

        hud.Add(
            "song_info",
            new Vector2(screen.X / 2, 44),
            new Rect2(screen.X / 2 - 310, 14, 620, 58),
            titleText,
            artistText);

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

        BuildErrorBar(ui, playfieldEntity, screen, hud);

        var contactLabels = new Entity[KeybindSettings.Actions.Length];
        for (int i = 0; i < contactLabels.Length; i++)
        {
            KeybindAction action = KeybindSettings.Actions[i];
            contactLabels[i] = SceneUi.Text(
                ui,
                new Vector2(screen.X / 2 + (i - 3) * 74, 92),
                new Vector2(70, 22),
                $"{KeybindSettings.LabelFor(action)} 0",
                13,
                SceneUi.DimTextColor,
                new Color(0, 0, 0, 0));
        }

        hud.Add(
            "contact_counters",
            new Vector2(screen.X / 2, 92),
            new Rect2(screen.X / 2 - 260, 80, 520, 26),
            contactLabels);
        playfieldEntity.AddComponent(new ContactCounterHud(contactLabels));

        hud.Add(
            "accuracy",
            new Vector2(screen.X - 150, 68),
            new Rect2(screen.X - 260, 56, 220, 24),
            accuracyText);
        // The judgement text is not a HUD layout item: it is placed on the note
        // that was hit, so it cannot be dragged away from it.
        hud.Add(
            "score",
            new Vector2(160, screen.Y - 24),
            new Rect2(20, screen.Y - 36, 280, 24),
            scoreText);
        hud.Add(
            "combo",
            new Vector2(screen.X - 150, screen.Y - 24),
            new Rect2(screen.X - 260, screen.Y - 36, 220, 24),
            comboText);
        hud.Add(
            "status",
            new Vector2(screen.X / 2, screen.Y - 24),
            new Rect2(screen.X / 2 - 380, screen.Y - 36, 760, 24),
            status);

        float counterX = screen.X - 150;
        var counterMembers = new List<Entity>();
        counterMembers.Add(SceneUi.Text(
            ui,
            new Vector2(counterX, 190),
            new Vector2(260, 22),
            LocalizedText.Get(PlayStrings.Judgements),
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0)));

        counterMembers.Add(CounterText(ui, screen.X - 200, 220, "FLAWLESS+",
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.FlawlessP),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 246, LocalizedText.Get(PlayStrings.Flawless),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Flawless),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 272, LocalizedText.Get(PlayStrings.Clean),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Clean),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 298, LocalizedText.Get(PlayStrings.Fair),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Fair),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 324, LocalizedText.Get(PlayStrings.Deficient),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Deficient),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 350, LocalizedText.Get(PlayStrings.Terrible),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Terrible),
            TextAlign.Left));
        counterMembers.Add(CounterText(ui, screen.X - 200, 376, LocalizedText.Get(PlayStrings.Miss),
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Miss),
            TextAlign.Left));

        Entity flawlessPlus = CounterNumber(ui, screen.X - 65, 220,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.FlawlessP));
        Entity flawless = CounterNumber(ui, screen.X - 65, 246,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Flawless));
        Entity clean = CounterNumber(ui, screen.X - 65, 272,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Clean));
        Entity fair = CounterNumber(ui, screen.X - 65, 298,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Fair));
        Entity deficient = CounterNumber(ui, screen.X - 65, 324,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Deficient));
        Entity terrible = CounterNumber(ui, screen.X - 65, 350,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Terrible));
        Entity miss = CounterNumber(ui, screen.X - 65, 376,
            PlayScoreSystem.ColorFor(Xanadu.Scoring.Judgement.Miss));
        counterMembers.Add(flawlessPlus);
        counterMembers.Add(flawless);
        counterMembers.Add(clean);
        counterMembers.Add(fair);
        counterMembers.Add(deficient);
        counterMembers.Add(terrible);
        counterMembers.Add(miss);
        hud.Add(
            "judgement_counts",
            new Vector2(screen.X - 130, 285),
            new Rect2(screen.X - 280, 178, 280, 220),
            counterMembers.ToArray());

        Entity countdown = SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, screen.Y / 2),
            new Vector2(700, 180),
            "",
            120,
            SceneUi.AccentColor,
            new Color(0, 0, 0, 0));

        hud.Add(
            "countdown",
            new Vector2(screen.X / 2, screen.Y / 2),
            new Rect2(screen.X / 2 - 350, screen.Y / 2 - 90, 700, 180),
            countdown);

        HudItemLayout keyTrailItem = hud.Add(
            "key_trail",
            new Vector2(origin.X - 220, origin.Y + fieldHeight - 140),
            new Rect2(origin.X - 440, origin.Y + fieldHeight - 280, 430, 280));
        keyTrailItem.Handle = entities.CreateEntity();

        playfieldEntity.AddComponent(hud);

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
        long lastNote = document.Notes.Count > 0
            ? document.Notes.Max(note => note.TimeMs)
            : -1;
        long lastMine = document.Mines.Count > 0
            ? document.Mines.Max(mine => mine.TimeMs)
            : -1;
        playfieldEntity.AddComponent(new PlaySession(
            status,
            countdown,
            Math.Max(lastNote, lastMine),
            start?.TimeMs ?? 0));

        if (replay is not null)
            playfieldEntity.AddComponent(new ReplayPlayer(replay));
        else if (record)
            playfieldEntity.AddComponent(ReplayRecorder.Create());
    }

    /// <summary>Where the player starts: the start point plus its groups' move at its time.</summary>
    public static Vector2 SpawnPosition(LevelGroups groups, StartPoint start) =>
        start.Position + GroupModulation.Offset(groups.Groups, start.Groups, start.TimeMs / 1000f);

    private static void BuildErrorBar(
        UiBuilder ui,
        Entity playfield,
        Vector2 screen,
        HudLayout hud)
    {
        const float fullWidth = 420;
        const float innerWidth = 360;
        float centerX = screen.X / 2;
        float y = screen.Y - 94;
        float maxMs = (float)ScoreRules.JudgementDeviation(Judgement.Miss);
        float pixelsPerMs = innerWidth / (maxMs * 2);

        var members = new List<Entity>();
        members.Add(ui.RectEntity(
            new Vector2(centerX, y),
            new Vector2(fullWidth + 24, 38),
            Palette.Background));

        (Judgement Judgement, double Inner, double Outer)[] bands =
        {
            (Judgement.FlawlessP, 0, ScoreRules.JudgementDeviation(Judgement.FlawlessP)),
            (Judgement.Flawless, ScoreRules.JudgementDeviation(Judgement.FlawlessP), ScoreRules.JudgementDeviation(Judgement.Flawless)),
            (Judgement.Clean, ScoreRules.JudgementDeviation(Judgement.Flawless), ScoreRules.JudgementDeviation(Judgement.Clean)),
            (Judgement.Fair, ScoreRules.JudgementDeviation(Judgement.Clean), ScoreRules.JudgementDeviation(Judgement.Fair)),
            (Judgement.Deficient, ScoreRules.JudgementDeviation(Judgement.Fair), ScoreRules.JudgementDeviation(Judgement.Deficient)),
            (Judgement.Terrible, ScoreRules.JudgementDeviation(Judgement.Deficient), ScoreRules.JudgementDeviation(Judgement.Terrible)),
            (Judgement.Miss, ScoreRules.JudgementDeviation(Judgement.Terrible), maxMs),
        };

        foreach ((Judgement judgement, double inner, double outer) in bands)
        {
            Color color = PlayScoreSystem.ColorFor(judgement);
            color.A = 0.82f;
            float width = (float)(outer - inner) * pixelsPerMs;
            float offset = (float)((inner + outer) / 2) * pixelsPerMs;
            members.Add(ui.RectEntity(
                new Vector2(centerX - offset, y),
                new Vector2(width, 14),
                color));
            members.Add(ui.RectEntity(
                new Vector2(centerX + offset, y),
                new Vector2(width, 14),
                color));
        }

        members.Add(ui.RectEntity(
            new Vector2(centerX, y),
            new Vector2(2, 26),
            Colors.White));

        Entity marker = ui.RectEntity(
            new Vector2(centerX, y),
            new Vector2(4, 30),
            new Color(1, 1, 1, 0),
            "");
        members.Add(marker);

        members.Add(SceneUi.Text(
            ui,
            new Vector2(centerX - innerWidth / 2, y + 28),
            new Vector2(100, 20),
            "EARLY",
            12,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0)));

        members.Add(SceneUi.Text(
            ui,
            new Vector2(centerX + innerWidth / 2, y + 28),
            new Vector2(100, 20),
            "LATE",
            12,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0)));

        hud.Add(
            "error_bar",
            new Vector2(centerX, y),
            new Rect2(centerX - fullWidth / 2 - 12, y - 20, fullWidth + 24, 78),
            members.ToArray());

        playfield.AddComponent(new ErrorBar(
            marker,
            centerX,
            y,
            innerWidth / 2,
            maxMs));
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
