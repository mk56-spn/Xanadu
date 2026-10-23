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

public sealed class PlaySceneTest : SceneTest
{
    public override string Name => "Play HUD and field";
    public override string Slug => "play";

    protected override void SetUpSteps()
    {
        AddStep("open synthetic play scene", OpenPlay);

        AddUntilStep(
            "wait for play HUD",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.Play,
                SceneUi.PlayHudLayer) &&
                VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "level title exists",
            () => VisualAssert.HasText(Context.Entities, "Visual Play Test"));
        AddAssert(
            "score HUD exists",
            () => VisualAssert.HasText(Context.Entities, "SCORE 0000000"));
        AddAssert(
            "combo HUD exists",
            () => VisualAssert.HasText(Context.Entities, "COMBO 0"));
        AddAssert(
            "PAUSE button is animated",
            () => VisualAssert.HasAnimatedText(Context.Entities, "PAUSE"));
        AddAssert(
            "play HUD layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.PlayHudLayer) == 0);
        AddAssert(
            "player exists",
            () => Context.Entities.Query<Player>().Count == 1);
        AddAssert(
            "judgement counter exists",
            () => VisualAssert.HasText(Context.Entities, "JUDGEMENTS") &&
                  VisualAssert.HasText(Context.Entities, "FLAWLESS+"));
        AddAssert(
            "key trail exists",
            () => Context.Entities.Query<KeyTrail>().Count == 1);
        AddAssert(
            "timing error bar exists",
            () => Context.Entities.Query<ErrorBar>().Count == 1);
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-play", "Visual Play");
        LevelDocument document = TestContent.Level("Visual Play Test", song);

        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 260, Y = 620 });
        document.Notes.Add(new NoteDocument { TimeMs = 1_000, X = 620, Y = 520 });
        document.Notes.Add(new NoteDocument { TimeMs = 2_000, X = 460, Y = 700 });

        Context.Scenes.StartPlay(
            new LevelEntry("visual://play", document, song));
    }
}
