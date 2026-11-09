using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// The block tool's three sizes in the real editor: a platform two cells wide,
/// four quarter blocks filling one cell, and a block one cell square, with the
/// BLOCK SIZE row lit on the size last picked.
/// </summary>
public sealed class EditorBlockSizesSceneTest : SceneTest
{
    public override string Name => "Editor block sizes";
    public override string Slug => "editor-block-sizes";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the sidebar",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorBlockOutlinePanel>().Count == 1);
        AddEditorSong();

        AddStep("pick the blocks tool", () => ClickTool(EditorPlacementMode.Blocks));
        AddWaitStep("hold the tool", 2);
        AddStep("release the tool", () => Context.Pointer.Release());
        AddUntilStep("the block size buttons show", () =>
            !SizeButton(BlockBrushSize.Quarter).IsNull && SizeButton(BlockBrushSize.Quarter).Enabled);

        AddStep("pick the platform", () => Click(SizeButton(BlockBrushSize.Platform)));
        AddWaitStep("hold 2×1", 2);
        AddStep("release 2×1", () => Context.Pointer.Release());
        AddUntilStep("the brush is a platform", () => Brush() == BlockBrushSize.Platform);
        Place("the platform", new Vector2(4f, 0.5f));

        AddStep("pick the quarter block", () => Click(SizeButton(BlockBrushSize.Quarter)));
        AddWaitStep("hold ½×½", 2);
        AddStep("release ½×½", () => Context.Pointer.Release());
        AddUntilStep("the brush is a quarter", () => Brush() == BlockBrushSize.Quarter);
        Place("the first quarter", new Vector2(0.25f, 0.25f));
        Place("the second quarter", new Vector2(0.75f, 0.25f));
        Place("the third quarter", new Vector2(0.25f, 0.75f));
        Place("the fourth quarter", new Vector2(0.75f, 0.75f));

        AddStep("pick a whole cell", () => Click(SizeButton(BlockBrushSize.Cell)));
        AddWaitStep("hold 1×1", 2);
        AddStep("release 1×1", () => Context.Pointer.Release());
        AddUntilStep("the brush is a cell", () => Brush() == BlockBrushSize.Cell);
        Place("the square block", new Vector2(2.5f, 0.5f));

        AddStep("move the pointer away", () =>
        {
            Context.Pointer.Release();
            Context.Pointer.Position = new Vector2(960, 20);
        });
        AddWaitStep("settle", 2);

        AddAssert("a platform, four quarters and a square are there", () =>
        {
            var sizes = new List<Vector2>();
            foreach (Entity block in Context.Entities.Query<Block>().ToEntityList())
                sizes.Add(block.GetComponent<Block>().Size);
            return sizes.Count(s => s == GridPlacement.NewBlockSize(Field(), 0f, BlockBrushSize.Platform)) == 1 &&
                   sizes.Count(s => s == GridPlacement.NewBlockSize(Field(), 0f, BlockBrushSize.Quarter)) == 4 &&
                   sizes.Count(s => s == GridPlacement.NewBlockSize(Field(), 0f, BlockBrushSize.Cell)) == 1;
        });
        AddAssert("the four quarters fill one cell", () =>
        {
            Vector2 size = GridPlacement.NewBlockSize(Field(), 0f, BlockBrushSize.Quarter);
            Rect2 covered = default;
            bool first = true;
            foreach (Entity block in Context.Entities.Query<Block>().ToEntityList())
            {
                Block placed = block.GetComponent<Block>();
                if (placed.Size != size)
                    continue;

                covered = first ? placed.Bounds : covered.Merge(placed.Bounds);
                first = false;
            }

            return covered.Size == GridPlacement.Cell(Field());
        });
    }

    /// <summary>
    /// Presses the field a number of cells from the cell at the middle of the
    /// view, then lets go of it.
    /// </summary>
    private void Place(string description, Vector2 cellOffset)
    {
        AddStep($"drop the selection before {description}", () =>
            Editor().GetComponent<NoteSelection>().Clear());
        AddStep($"place {description}", () =>
        {
            Playfield playfield = Field();
            PlayfieldView view = Editor().GetComponent<PlayfieldView>();
            Vector2 cell = GridPlacement.Cell(playfield);
            Vector2 middle = view.ScreenToPlayfield(view.ScreenSize / 2f);
            Vector2 corner = new(
                Mathf.Floor(middle.X / cell.X) * cell.X,
                Mathf.Floor(middle.Y / cell.Y) * cell.Y);
            Context.Pointer.Click(view.PlayfieldToScreen(corner + cellOffset * cell));
        });
        AddStep($"release {description}", () => Context.Pointer.Release());
        AddWaitStep($"settle {description}", 2);
    }

    private BlockBrushSize Brush() =>
        Editor().GetComponent<EditorPlacement>().BlockSize;

    private Playfield Field() => Editor().GetComponent<Playfield>();

    private Entity SizeButton(BlockBrushSize size)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.BlockSize))
        {
            if (button.GetComponent<EditorButton>().Arg == (int)size)
                return button;
        }

        return default;
    }

    private void ClickTool(EditorPlacementMode mode)
    {
        foreach (Entity button in EditorButton.All(Context.Entities, EditorButtonKind.Place))
        {
            if (button.GetComponent<EditorButton>().Arg == (int)mode)
                Click(button);
        }
    }

    private void Click(Entity entity) =>
        Context.Pointer.Click(UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value));

    private Entity Editor() => Context.Entities.Query<EditorBlockOutlinePanel>().ToEntityList()[0];
}
