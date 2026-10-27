using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;
using Xanadu.Editor;
using Xanadu.Levels;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Systems.Playfields;

namespace Xanadu.Tests.VisualTests;

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
                  VisualAssert.HasText(Context.Entities, "XANADU"));
        AddAssert(
            "XANADU title exists",
            () => VisualAssert.HasText(Context.Entities, "XANADU"));
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
