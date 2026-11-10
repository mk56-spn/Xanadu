using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Playfields;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Step 11 of docs/render-architecture.md in the editor: the Screen reads tool
/// places a region on the current layer, the editor outlines it, and the resize
/// handles change its rect.
/// </summary>
public sealed class EditorScreenReadSceneTest : SceneTest
{
    private Entity _region;

    public override string Name => "Editor screen read";
    public override string Slug => "editor-screen-read";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the tool groups",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorToolGroup>().Count > 0);
        AddEditorSong();

        AddStep("open the text and screen reads group", () => Click(Group(4)));
        AddWaitStep("hold the group", 2);
        AddStep("release the group", () => Context.Pointer.Release());
        AddUntilStep("the text and reads flyout is open", () =>
        {
            EditorToolGroup group = Group(4).GetComponent<EditorToolGroup>();
            return group.Open && !UiLayout.IsHidden(group.Flyout);
        });

        AddStep("pick the screen read tool", () => Click(PlaceButton(EditorPlacementMode.ScreenRead)));
        AddWaitStep("hold the tool", 2);
        AddStep("release the tool", () => Context.Pointer.Release());
        AddUntilStep("the tool is current", () =>
            Editor().GetComponent<EditorPlacement>().Mode == EditorPlacementMode.ScreenRead);

        AddStep("place a region on the field", () => Context.Pointer.Click(new Vector2(980, 380)));
        AddWaitStep("hold the field", 2);
        AddStep("release the field", () => Context.Pointer.Release());
        AddUntilStep("a region exists and is selected", () =>
        {
            var regions = Context.Entities.Query<ScreenReadRegion>().ToEntityList();
            if (regions.Count != 1)
                return false;
            _region = regions[0];
            return Editor().GetComponent<NoteSelection>().Contains(_region);
        });
        AddAssert("it is on the gameplay layer", () =>
            ItemLayer.Of(_region).Layer == ItemLayer.Gameplay);

        AddStep("grab the region's right handle", () => Context.Pointer.Click(RightHandle()));
        AddWaitStep("hold the handle", 2);
        AddStep("drag the handle right", () => Context.Pointer.Position += new Vector2(90, 0));
        AddWaitStep("drag", 3);
        AddStep("release the handle", () => Context.Pointer.Release());
        AddUntilStep("the region grew wider", () =>
            _region.GetComponent<ScreenReadRegion>().Size.X > ScreenReadRegion.DefaultWidth + 20);
        AddWaitStep("look at the outlined region", 5);
    }

    private Entity Editor() =>
        Context.Entities.Query<EditorPlacement, PlayfieldView, NoteSelection>().ToEntityList()[0];

    private Entity Group(int index)
    {
        foreach (Entity entity in Context.Entities.Query<EditorToolGroup>().ToEntityList())
        {
            if (entity.GetComponent<EditorToolGroup>().Index == index)
                return entity;
        }

        return default;
    }

    private Entity PlaceButton(EditorPlacementMode mode)
    {
        foreach (Entity entity in Context.Entities.Query<EditorButton>().ToEntityList())
        {
            EditorButton button = entity.GetComponent<EditorButton>();
            if (button.Kind == EditorButtonKind.Place && button.Arg == (int)mode)
                return entity;
        }

        return default;
    }

    private Vector2 RightHandle()
    {
        PlayfieldView view = Editor().GetComponent<PlayfieldView>();
        ScreenReadRegion region = _region.GetComponent<ScreenReadRegion>();
        Vector2 point = GridPlacement.HandleCenter(region.Bounds, GridHandle.Right, 0f);
        return view.PlayfieldToScreen(point);
    }

    private void Click(Entity entity)
    {
        if (!entity.IsNull)
            Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));
    }
}
