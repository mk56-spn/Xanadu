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

/// <summary>Regression: Retry must rebuild the level from results and pause.</summary>
public sealed class RestartSceneTest : SceneTest
{
    public override string Name => "Retry";
    public override string Slug => "restart";

    protected override void SetUpSteps()
    {
        AddStep("open retry play scene", OpenPlay);
        AddUntilStep(
            "wait for play",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<PlaySession>().Count == 1);

        AddStep("finish play", FinishPlay);
        AddUntilStep(
            "wait for results",
            () => Context.Scenes.Current == SceneKind.Results &&
                  VisualAssert.HasText(Context.Entities, "RETRY"));

        AddStep("click results retry", ClickRetry);
        AddWaitStep("hold results retry", 2);
        AddStep("release results retry", () => Context.Pointer.Release());
        AddUntilStep(
            "restart from results",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<Note>().Count == 2 &&
                  Context.Entities.Query<PlaySession>().ToEntityList()[0]
                      .GetComponent<PlaySession>().Finished == false);
        AddAssert(
            "restarted score is reset",
            () => Context.Entities.Query<ScoreState>().ToEntityList()[0]
                .GetComponent<ScoreState>().ScorePoints == 0);

        AddStep("open pause", () => Context.Scenes.TryBack());
        AddUntilStep(
            "wait for pause retry",
            () => Context.Scenes.Current == SceneKind.Play &&
                  PauseVisible());

        AddStep("click pause retry", ClickRetry);
        AddWaitStep("hold pause retry", 2);
        AddStep("release pause retry", () => Context.Pointer.Release());
        AddUntilStep(
            "restart from pause",
            () => Context.Scenes.Current == SceneKind.Play &&
                  Context.Entities.Query<Note>().Count == 2 &&
                  !PauseVisible());
    }

    private void OpenPlay()
    {
        var song = TestContent.Song("visual-retry", "Retry");
        LevelDocument document = TestContent.Level("Retry Test", song);

        document.Notes.Add(new NoteDocument { TimeMs = 0, X = 200, Y = 300 });
        document.Notes.Add(new NoteDocument { TimeMs = 1_000, X = 600, Y = 500 });

        Context.Scenes.StartPlay(new LevelEntry("memory://retry", document, song));
    }

    private void FinishPlay()
    {
        Entity playfield = Context.Entities.Query<PlaySession>().ToEntityList()[0];
        ref PlaySession session = ref playfield.GetComponent<PlaySession>();
        session.Finished = true;
    }

    private void ClickRetry()
    {
        foreach (Entity entity in Context.Entities
                     .Query<RetryAction, Position>()
                     .ToEntityList())
        {
            Vector2 world = WorldPosition(
                entity,
                entity.GetComponent<Position>().Value);

            if (world.X < -1_000)
                continue;

            Context.Pointer.Click(world);
            return;
        }
    }

    private bool PauseVisible()
    {
        foreach (Entity entity in Context.Entities.Query<ScenePause>().ToEntityList())
            return entity.GetComponent<ScenePause>().Visible;

        return false;
    }

    private static Vector2 WorldPosition(Entity entity, Vector2 local)
    {
        Vector2 world = local;
        Entity parent = entity.Parent;

        while (!parent.IsNull)
        {
            if (parent.TryGetComponent<Position>(out var position))
                world += position.Value;

            parent = parent.Parent;
        }

        return world;
    }
}
