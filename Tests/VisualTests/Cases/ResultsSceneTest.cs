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

public sealed class ResultsSceneTest : SceneTest
{
    public override string Name => "Results";
    public override string Slug => "results";

    protected override void SetUpSteps()
    {
        AddStep(
            "open results",
            () => Context.Scenes.StartResults(
                new PlayResults(
                    "Results Test",
                    987_654,
                    0.9876,
                    42,
                    30,
                    42)));

        AddUntilStep(
            "wait for results ECS",
            () => Context.Scenes.Current == SceneKind.Results &&
                  VisualAssert.HasText(Context.Entities, "RESULTS") &&
                  VisualAssert.HasText(Context.Entities, "98.76%"));

        AddAssert(
            "accuracy is shown",
            () => VisualAssert.HasText(Context.Entities, "98.76%"));
        AddAssert(
            "rank is shown",
            () => VisualAssert.HasText(Context.Entities, "S"));
        AddAssert(
            "notes hit ratio is shown",
            () => VisualAssert.HasText(Context.Entities, "30 / 42"));
        AddAssert(
            "max combo is shown",
            () => VisualAssert.HasText(Context.Entities, "42"));
        AddAssert(
            "score is shown",
            () => VisualAssert.HasText(Context.Entities, "0987654"));
        AddAssert(
            "level name is shown",
            () => VisualAssert.HasText(Context.Entities, "Results Test"));
        AddAssert(
            "results has retry and exit buttons",
            () => VisualAssert.HasText(Context.Entities, "RETRY") &&
                  VisualAssert.HasText(Context.Entities, "SONG SELECT") &&
                  VisualAssert.HasText(Context.Entities, "MAIN MENU"));
    }
}
