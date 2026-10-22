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

public sealed class ProfilesSceneTest : SceneTest
{
    public override string Name => "Profiles";
    public override string Slug => "profiles";

    protected override void SetUpSteps()
    {
        AddStep("open profiles", () => Context.Scenes.Start(SceneKind.Profiles));
        AddUntilStep(
            "wait for profiles",
            () => Context.Scenes.Current == SceneKind.Profiles &&
                  VisualAssert.HasText(Context.Entities, "WHO'S PLAYING?"));
        AddAssert(
            "new profile button exists",
            () => VisualAssert.HasText(Context.Entities, "NEW PROFILE"));
        AddAssert(
            "profile selector exists",
            () => Context.Entities.Query<ProfileCreate>().Count == 1);
    }
}
