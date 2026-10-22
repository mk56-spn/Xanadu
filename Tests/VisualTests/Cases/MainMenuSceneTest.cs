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

public sealed class MainMenuSceneTest : SceneTest
{
    public override string Name => "Main menu";
    public override string Slug => "main";

    protected override void SetUpSteps()
    {
        AddStep("open main menu", () => Context.Scenes.Start(SceneKind.Main));
        AddUntilStep(
            "wait for main menu",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Main, SceneUi.MenuLayer) &&
                  VisualAssert.HasText(Context.Entities, "WRITER"));
        AddAssert(
            "WRITER title exists",
            () => VisualAssert.HasText(Context.Entities, "WRITER"));
        AddAssert(
            "PLAY button exists",
            () => VisualAssert.HasText(Context.Entities, "PLAY"));
        AddAssert(
            "PLAY button is animated",
            () => VisualAssert.HasAnimatedText(Context.Entities, "PLAY"));
        AddAssert(
            "offset wizard button exists",
            () => VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "menu layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.MenuLayer) == 0);
    }
}
