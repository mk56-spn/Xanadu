using System.Linq;
using Xanadu.Logic.Profiles;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Ecs.Components;
using System;
using System.Linq;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.VisualTests;

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
            () => UiCommand.All<ProfilesEntrance>(Context.Entities).Count(c => c.Id == ProfilesEntrance.Create) == 1);
    }
}
