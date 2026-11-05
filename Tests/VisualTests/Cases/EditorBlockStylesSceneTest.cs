using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Block outlines and turned spikes in the real editor: no outline, thin and
/// thick ones in their own colours, white spikes at any angle, and the outline
/// controls showing in the sidebar for the selected block.
/// </summary>
public sealed class EditorBlockStylesSceneTest : SceneTest
{
    private Entity _selected;

    public override string Name => "Editor block styles";
    public override string Slug => "editor-block-styles";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorBlockOutlinePanel>().Count == 1);

        AddStep("place blocks and spikes", () =>
        {
            Entity editor = Editor();
            PlayfieldView view = editor.GetComponent<PlayfieldView>();
            Vector2 At(float x, float y) => (new Vector2(x, y) - view.ScreenOrigin) / view.Scale;
            var size = new Vector2(140, 90) / view.Scale;
            float[] widths = { 0f, 3f, 12f, 28f };
            Color[] outlines = { Colors.Black, new("#ff4060"), new("#40c0ff"), new("#ffd040") };
            for (int i = 0; i < widths.Length; i++)
            {
                _selected = EditorItems.Create(
                    Context.Entities,
                    new Block(At(520 + i * 170, 340), size, outlineWidth: widths[i], outlineColor: outlines[i]));
                SelfModulate.Put(_selected, new Color("#30405a"));
            }

            var tile = new Vector2(80, 80) / view.Scale;
            float[] angles = { 0f, 30f, 90f, 200f };
            for (int i = 0; i < angles.Length; i++)
                EditorItems.Create(Context.Entities, new Spike(At(540 + i * 170, 520), tile, angles[i]));

            editor.GetComponent<NoteSelection>().Note = _selected;
        });
        AddUntilStep("the outline controls show", () =>
            Editor().GetComponent<EditorBlockOutlinePanel>().Section.Enabled);
        AddStep("click the outline's +", () => Click(StepButton(1)));
        AddWaitStep("hold +", 2);
        AddStep("release +", () => Context.Pointer.Release());
        AddUntilStep("the outline thickened", () => _selected.GetComponent<Block>().OutlineWidth == 29f);

        AddStep("put the block in group 1", () => _selected.AddComponent(new ItemGroups(ItemGroups.Bit(0))));
        AddStep("click EDIT on the group bar", () => Click(Editor().GetComponent<EditorGroupBar>().Edit));
        AddWaitStep("hold EDIT", 2);
        AddStep("release EDIT", () => Context.Pointer.Release());
        AddUntilStep("the overlay opens", () => Editor().GetComponent<EditorGroupState>().Open == 0);

        AddStep("click OUTLINE", () => Click(Piece(EditorGroupPart.OutlineLink)));
        AddWaitStep("hold OUTLINE", 2);
        AddStep("release OUTLINE", () => Context.Pointer.Release());
        AddUntilStep("the outline is unhooked from the group", () =>
            (_selected.GetComponent<Block>().OutlineGroups & ItemGroups.Bit(0)) == 0);
        AddAssert("the interior is still hooked up", () =>
            (_selected.GetComponent<Block>().FillGroups & ItemGroups.Bit(0)) != 0);
        AddAssert("the button says so", () =>
            Piece(EditorGroupPart.OutlineLink).GetComponent<Text>().Value == "OUTLINE OFF");
    }

    private Entity StepButton(int delta)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.OutlineStep))
        {
            if (button.GetComponent<EditorButton>().Arg == delta)
                return button;
        }

        return default;
    }

    private Entity Piece(EditorGroupPart part)
    {
        foreach (Entity piece in Context.Entities.Query<EditorGroupPiece>().WithDisabled().ToEntityList())
        {
            if (piece.GetComponent<EditorGroupPiece>().Part == part)
                return piece;
        }

        return default;
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private Entity Editor() => Context.Entities.Query<EditorBlockOutlinePanel>().ToEntityList()[0];
}
