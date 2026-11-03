using Xanadu.Ecs;
using Xanadu.Scenes;

namespace Xanadu.Tests.VisualTests;

/// <summary>The offset wizard's ECS panel and controls.</summary>
public sealed class OffsetWizardSceneTest : SceneTest
{
    public override string Name => "Offset wizard";
    public override string Slug => "offset-wizard";

    protected override void SetUpSteps()
    {
        AddStep(
            "open offset wizard",
            () => Context.Scenes.Start(SceneKind.OffsetWizard));
        AddUntilStep(
            "wait for offset wizard",
            () => VisualAssert.IsSceneReady(
                Context,
                SceneKind.OffsetWizard,
                UiLayers.Menu) &&
                VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "wizard actions exist",
            () => VisualAssert.HasText(Context.Entities, "START") &&
                  VisualAssert.HasText(Context.Entities, "APPLY") &&
                  VisualAssert.HasText(Context.Entities, "BACK") &&
                  VisualAssert.HasText(Context.Entities, "RESET"));
        AddAssert(
            "wizard instructions are shown",
            () => VisualAssert.HasText(
                Context.Entities,
                "Tap SPACE with the clicks, by ear, for 36 clicks. Look away if it helps."));
        AddAssert(
            "wizard layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                UiLayers.Menu) == 0);
    }
}
