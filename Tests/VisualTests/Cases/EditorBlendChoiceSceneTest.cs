using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The sidebar's blend control is a list of modes, not a pair of arrows. The
/// screenshot shows the open choice over the inspector.
/// </summary>
public sealed class EditorBlendChoiceSceneTest : SceneTest
{
    public override string Name => "Editor blend choice";
    public override string Slug => "editor-blend-choice";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the tool groups",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorToolGroup>().Count > 0);

        AddStep("place and select a block", () =>
        {
            PlayfieldView view = Context.Entities.Query<PlayfieldView>().ToEntityList()[0].GetComponent<PlayfieldView>();
            Entity block = EditorItems.Create(
                Context.Entities,
                new Block((new Vector2(900, 480) - view.ScreenOrigin) / view.Scale, new Vector2(240, 240)));
            Context.Entities.Query<NoteSelection>().ToEntityList()[0]
                .GetComponent<NoteSelection>().SetMany(new[] { block });
        });

        AddUntilStep("the blend reads as a choice", () =>
            VisualAssert.HasText(Context.Entities, "BLEND  MIX  ▾"));

        AddStep("open the blend choice", () =>
        {
            Entity button = BlendButton();
            Context.Pointer.Click(UiTransform.WorldPosition(button, button.GetComponent<Position>().Value));
        });
        AddWaitStep("hold the blend choice", 2);
        AddStep("release the blend choice", () => Context.Pointer.Release());
        AddUntilStep("the list of modes is open", () => BlendButton().GetComponent<Dropdown>().Open);
        AddWaitStep("look at the list", 6);
    }

    private Entity BlendButton()
    {
        foreach (Entity entity in Context.Entities.Query<EditorDropdownControl>().ToEntityList())
        {
            if (entity.GetComponent<EditorDropdownControl>().Kind == EditorDropdownKind.Blend)
                return entity;
        }

        return default;
    }
}
