using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Scoring;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>The play HUD: score, combo, accuracy, judgement counts, contact and weapon readouts.</summary>
internal static class PlayHudBuilder
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Entity playfield,
        Vector2 screen,
        LevelDocument document,
        SongDocument? song,
        Vector2 origin,
        Rect2 viewport,
        float fieldHeight,
        IVisualRidFactory? visuals,
        long startMs)
    {
        UiNode ui = UiNode.Layer(
            entities, root, UiLayers.PlayHud, visuals, UiStage.Full)
            .Theme(UiThemes.Hud);
        UiThemeRoles theme = ui.Roles;
        HudLayout hud = new();

        ui.HudButton(
                new Vector2(95, 44),
                new Vector2(150, 44),
                LocalizedText.Get(PlayStrings.Pause),
                UiType.Label)
            .Secondary()
            .On(SceneActionId.Pause);

        // Under PAUSE, clear of the field; PlaySkipSystem shows it while the entrance can be skipped.
        Entity skipButton = ui.HudButton(
                new Vector2(95, 100),
                new Vector2(150, 44),
                LocalizedText.Get(PlayStrings.Skip),
                UiType.Label)
            .Primary()
            .Hidden()
            .Entity;

        Entity titleText = ui.HudText(new Vector2(screen.X / 2, 30),
            new Vector2(620, 30),
            string.IsNullOrWhiteSpace(document.Name) ? "UNTITLED" : document.Name,
            20,
            SceneUi.TextColor,
            theme.Backing);

        string artist = string.IsNullOrWhiteSpace(song?.Artist)
            ? "Unknown artist"
            : song.Artist;
        string difficulty = string.IsNullOrWhiteSpace(document.Name)
            ? "Unrated"
            : document.Name;

        Entity artistText = ui.HudText(new Vector2(screen.X / 2, 58),
            new Vector2(620, 22),
            $"{artist}   ·   {difficulty}",
            14,
            SceneUi.DimTextColor,
            theme.Backing);

        hud.Add(
            "song_info",
            new Vector2(screen.X / 2, 44),
            new Rect2(screen.X / 2 - 310, 14, 620, 58),
            titleText,
            artistText);

        Entity status = ui.HudText(new Vector2(screen.X / 2, screen.Y - 24),
            new Vector2(760, 24),
            "LOADING...",
            16,
            SceneUi.DimTextColor);

        Entity scoreText = ui.HudText(new Vector2(160, screen.Y - 24),
            new Vector2(280, 24),
            "SCORE 0000000",
            18,
            SceneUi.TextColor);

        // The combo: a big number over a small caption, animated by ComboHudSystem.
        var comboCenter = new Vector2(screen.X - 150, screen.Y - 126);
        var comboArea = new Rect2(comboCenter.X - 130, comboCenter.Y - 42, 260, 104);
        Entity comboBacking = ui.HudBacking(comboArea);
        Entity comboText = ui.HudText(comboCenter,
            new Vector2(260, 84),
            "0",
            UiType.Display,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        Entity comboCaption = ui.HudText(comboCenter + new Vector2(0, 46),
            new Vector2(260, 20),
            "COMBO",
            UiType.Small,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        playfield.AddComponent(new ComboHud(comboText, comboCaption));

        Entity accuracyText = ui.HudText(new Vector2(screen.X - 150, 68),
            new Vector2(220, 24),
            "ACC 0.00%  FAIL",
            14,
            SceneUi.DimTextColor);

        Entity judgementText = ui.HudText(new Vector2(screen.X / 2, screen.Y / 2 - 120),
            new Vector2(520, 44),
            "",
            28,
            theme.Accent,
            new Color(0, 0, 0, 0));

        Entity timingText = ui.HudText(new Vector2(screen.X / 2, screen.Y / 2 - 90),
            new Vector2(520, 22),
            "",
            UiType.Small,
            theme.Accent,
            new Color(0, 0, 0, 0))
            .Hidden();
        playfield.AddComponent(new TimingIndicator(timingText));

        BuildErrorBar(ui, playfield, screen, hud);

        var contactArea = new Rect2(screen.X / 2 - 160, 80, 320, 26);
        Entity contactBacking = ui.HudBacking(contactArea);
        Entity leftStack = ui.HudText(new Vector2(screen.X / 2 - 80, 92),
            new Vector2(150, 22),
            "L 0",
            13,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        Entity rightStack = ui.HudText(new Vector2(screen.X / 2 + 80, 92),
            new Vector2(150, 22),
            "R 0",
            13,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));

        hud.Add(
            "contact_counters",
            new Vector2(screen.X / 2, 92),
            contactArea,
            new[] { contactBacking, leftStack, rightStack });
        playfield.AddComponent(new ContactCounterHud(leftStack, rightStack));

        Entity weaponText = ui.HudText(
            new Vector2(screen.X - 150, 96),
            new Vector2(220, 22),
            "",
            14,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0));
        hud.Add(
            "weapon",
            new Vector2(screen.X - 150, 96),
            new Rect2(screen.X - 260, 84, 220, 24),
            weaponText);
        playfield.AddComponent(new WeaponHud(weaponText));

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
            comboCenter + new Vector2(0, 10),
            comboArea,
            comboBacking,
            comboText,
            comboCaption);
        hud.Add(
            "status",
            new Vector2(screen.X / 2, screen.Y - 24),
            new Rect2(screen.X / 2 - 380, screen.Y - 36, 760, 24),
            status);

        float counterX = screen.X - 150;
        var countsArea = new Rect2(screen.X - 280, 178, 280, 220);
        var counterMembers = new List<Entity> { ui.HudBacking(countsArea) };
        counterMembers.Add(ui.HudText(new Vector2(counterX, 190),
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
            countsArea,
            counterMembers.ToArray());

        Entity countdown = ui.HudText(new Vector2(screen.X / 2, screen.Y / 2),
            new Vector2(700, 180),
            "",
            120,
            theme.Accent,
            new Color(0, 0, 0, 0));

        hud.Add(
            "countdown",
            new Vector2(screen.X / 2, screen.Y / 2),
            new Rect2(screen.X / 2 - 350, screen.Y / 2 - 90, 700, 180),
            countdown);

        // Left of the field, or at the screen's left edge when the field fills the width.
        float trailX = Mathf.Max(origin.X - 220, PlayScene.SideInset + 220);
        float shownBottom = origin.Y + Mathf.Min(fieldHeight, viewport.Size.Y);
        HudItemLayout keyTrailItem = hud.Add(
            "key_trail",
            new Vector2(trailX, shownBottom - 140),
            new Rect2(trailX - 220, shownBottom - 280, 430, 280));
        keyTrailItem.Handle = entities.CreateEntity();

        playfield.AddComponent(hud);

        playfield.AddComponent(new JudgementCounter(
            flawlessPlus,
            flawless,
            clean,
            fair,
            deficient,
            terrible,
            miss));

        playfield.AddComponent(
            new ScoreState(
                scoreText,
                comboText,
                accuracyText,
                judgementText,
                document.Notes.Count(note =>
                    note.Kind is not (NoteKind.Contact or NoteKind.ChannelEnd))));
        long lastNote = document.Notes.Count > 0
            ? document.Notes.Max(note => note.TimeMs)
            : -1;
        long lastMine = document.Mines.Count > 0
            ? document.Mines.Max(mine => mine.TimeMs)
            : -1;
        playfield.AddComponent(new PlaySession(
            status,
            countdown,
            Math.Max(lastNote, lastMine),
            startMs,
            PlaySkipSystem.SkipTarget(
                document.Notes.Select(note => note.TimeMs)
                    .Concat(document.Mines.Select(mine => mine.TimeMs)),
                startMs),
            skipButton));

        Entity mapOffset = ui.HudText(
            new Vector2(screen.X / 2, 124),
            new Vector2(620, 28),
            "",
            UiType.Body,
            theme.Accent,
            theme.Backing)
            .Hidden();
        playfield.AddComponent(new PlayOffset(document.Id, mapOffset));


    }

    private static void BuildErrorBar(
        UiNode ui,
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
        members.Add(ui.HudRect(
            new Vector2(centerX, y),
            new Vector2(fullWidth + 24, 38),
            default).Backing());

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
            members.Add(ui.HudRect(
                new Vector2(centerX - offset, y),
                new Vector2(width, 14),
                color));
            members.Add(ui.HudRect(
                new Vector2(centerX + offset, y),
                new Vector2(width, 14),
                color));
        }

        members.Add(ui.HudRect(
            new Vector2(centerX, y),
            new Vector2(2, 26),
            Colors.White));

        Entity marker = ui.HudRect(
            new Vector2(centerX, y),
            new Vector2(4, 30),
            new Color(1, 1, 1, 0)).Entity;
        members.Add(marker);

        members.Add(ui.HudText(new Vector2(centerX - innerWidth / 2, y + 28),
            new Vector2(100, 20),
            LocalizedText.Get(PlayStrings.Early),
            12,
            SceneUi.DimTextColor,
            new Color(0, 0, 0, 0)));

        members.Add(ui.HudText(new Vector2(centerX + innerWidth / 2, y + 28),
            new Vector2(100, 20),
            LocalizedText.Get(PlayStrings.Late),
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
        UiNode ui,
        float x,
        float y,
        string text,
        Color color,
        TextAlign align)
    {
        return ui.HudText(
                new Vector2(x, y),
                new Vector2(160, 22),
                text,
                UiType.Small,
                color,
                new Color(0, 0, 0, 0))
            .Align(align);
    }

    private static Entity CounterNumber(
        UiNode ui,
        float x,
        float y,
        Color color)
    {
        return ui.HudText(
                new Vector2(x, y),
                new Vector2(90, 22),
                "0",
                UiType.Small,
                color,
                new Color(0, 0, 0, 0))
            .Align(TextAlign.Right);
    }
}
