using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;
using Writer.Editor;
using Writer.Levels;
using Writer.Scenes;
using Writer.Settings;
using Writer.Systems.Playfields;

namespace Writer.Tests.VisualTests;

/// <summary>The results screen shows the three final values in one column.</summary>
/// <summary>Regression: a movement note alone must produce a judgement.</summary>
public sealed class MovementJudgementSceneTest : SceneTest
{
    public override string Name => "Movement judgement";
    public override string Slug => "movement-judgement";

    protected override void SetUpSteps()
    {
        AddStep("start movement-only chart", () =>
        {
            var song = TestContent.Song("visual-movement", "Movement");
            LevelDocument document = TestContent.Level(
                "Movement Judgement Test", song);
            document.Notes.Add(new NoteDocument
            {
                TimeMs = 1_000,
                X = 500,
                Y = 1000,
                Target = KeybindAction.L3,
                Kind = NoteKind.Movement,
                DashDirection = DashDirection.Right,
            });
            Context.Scenes.StartPlay(
                new LevelEntry("memory://movement-judgement", document, song));
        });
        AddUntilStep(
            "wait for movement play",
            () => Context.Entities.Query<PlaySession>().Count == 1 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Started);
        AddUntilStep(
            "wait for movement time",
            () => PlayheadTime() >= 900);
        AddStep("align movement note", () =>
        {
            Entity player = Context.Entities.Query<Player, Position>()
                .ToEntityList()[0];
            Entity playfield = Context.Entities.Query<PlayfieldView>()
                .ToEntityList()[0];
            PlayfieldView view = playfield.GetComponent<PlayfieldView>();
            Vector2 position = view.ScreenToPlayfield(
                player.GetComponent<Position>().Value);
            foreach (Entity note in Context.Entities.Query<Note>().ToEntityList())
            {
                ref Note value = ref note.GetComponent<Note>();
                value.Position = position;
            }
        });
        AddStep("freeze movement time", () => SetPlayheadTime(1_000));
        AddStep("hit movement note", () =>
        {
            Context.Keyboard.Press(
                Context.Systems.Keybinds.Get(KeybindAction.L3));
        });
        AddUntilStep(
            "movement note consumed",
            () => Context.Entities.Query<Note>().Count == 0);
        AddUntilStep(
            "dash trail exists",
            () => Context.Entities.Query<DashTrail>().Count > 0);
        AddUntilStep(
            "movement judgement shown",
            () => Context.Entities.Query<ScoreState>().Count > 0 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().NotesHit == 1 &&
                  Context.Entities.Query<ScoreState>().ToEntityList()[0]
                      .GetComponent<ScoreState>().JudgementText
                      .GetComponent<Text>().Value != "");
    }

    private void SetPlayheadTime(long timeMs)
    {
        foreach (Entity entity in Context.Entities
                     .Query<Playhead>()
                     .ToEntityList())
        {
            ref Playhead playhead = ref entity.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.TimeMs = timeMs;
            playhead.SeekRequestMs = -1;
        }
    }

    private long PlayheadTime() =>
        Context.Entities.Query<Playhead>().Count == 0
            ? 0
            : Context.Entities.Query<Playhead>().ToEntityList()[0]
                .GetComponent<Playhead>().TimeMs;
}
