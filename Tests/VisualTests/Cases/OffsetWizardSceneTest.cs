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
