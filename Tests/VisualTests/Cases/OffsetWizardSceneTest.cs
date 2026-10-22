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

public sealed class OffsetWizardSceneTest : SceneTest
{
    public override string Name => "Offset wizard";
    public override string Slug => "offset-wizard";

    protected override void SetUpSteps()
    {
        AddStep("open offset wizard", () => Context.Scenes.Start(SceneKind.OffsetWizard));
        AddUntilStep(
            "wait for offset wizard",
            () => Context.Scenes.Current == SceneKind.OffsetWizard &&
                  VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "offset instructions exist",
            () => VisualAssert.HasText(
                Context.Entities,
                "Tap SPACE with every click."));
    }
}
