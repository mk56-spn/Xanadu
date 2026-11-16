using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Systems.Input;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// F4 over the main menu: pointing at PLAY in pick mode outlines and names it;
/// clicking opens the tree on its parent with PLAY inspected.
/// </summary>
public sealed class UiTreeSceneTest(bool click) : SceneTest
{
    public override string Name => click ? "F4 UI tree, picked" : "F4 UI tree, pointing";
    public override string Slug => click ? "ui-tree" : "ui-tree-pick";

    protected override void SetUpSteps()
    {
        AddStep("open main menu", () => Context.Scenes.Start(SceneKind.Main));
        AddUntilStep(
            "wait for main menu",
            () => VisualAssert.IsSceneReady(Context, SceneKind.Main, UiLayers.Menu) &&
                  VisualAssert.HasText(Context.Entities, "PLAY"));
        AddStep("press F4", () => Context.Keyboard.Press(DebugKeys.UiTree));
        AddWaitStep("hold", 2);
        AddStep("release F4", () => Context.Keyboard.Release(DebugKeys.UiTree));
        AddUntilStep("the panel is open", () => Catcher() is { IsNull: false });
        AddStep("point at PLAY", () => Context.Pointer.Position = Centre(Play()));
        AddUntilStep("PLAY is outlined", () => Play().Tags.Has<UiTreeOverlayHovered>());
        if (!click)
        {
            AddWaitStep("settle", 10);
            return;
        }

        AddStep("click it", () => Context.Pointer.Click(Centre(Play())));
        AddWaitStep("hold", 2);
        AddStep("release", () => Context.Pointer.Release());
        AddUntilStep("PLAY is inspected", () => Play().Tags.Has<UiTreeOverlaySelected>());
        AddStep("move off it", () => Context.Pointer.Position = new Vector2(400, 900));
        AddWaitStep("settle", 10);
        AddAssert("the inspector lists its components", () =>
            Context.Entities.Query<Text>().ToEntityList().Any(e => e.GetComponent<Text>().Value == "▪ Position"));
    }

    private Entity Catcher() =>
        Context.Entities.Query<UiCommand>().ToEntityList().FirstOrDefault(e => e.GetComponent<UiCommand>().Is(UiTreeCommand.PickHere));

    private Entity Play() =>
        Context.Entities.Query<Text>().ToEntityList().First(e =>
            e.GetComponent<Text>().Value == "PLAY" && e.HasComponent<ButtonVisual>());

    private static Vector2 Centre(Entity entity) => UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
}
