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
                SceneUi.MenuLayer) &&
                VisualAssert.HasText(Context.Entities, "OFFSET WIZARD"));
        AddAssert(
            "wizard actions exist",
            () => VisualAssert.HasText(Context.Entities, "START") &&
                  VisualAssert.HasText(Context.Entities, "SAVE") &&
                  VisualAssert.HasText(Context.Entities, "BACK") &&
                  VisualAssert.HasText(Context.Entities, "RESET OFFSET"));
        AddAssert(
            "wizard instructions are shown",
            () => VisualAssert.HasText(
                Context.Entities,
                "Listen to the eight clicks and tap SPACE exactly with each one."));
        AddAssert(
            "wizard layer is initialized",
            () => VisualAssert.CountUninitialised(
                Context.Entities,
                SceneUi.MenuLayer) == 0);
    }
}
