using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.VisualTests;

/// <summary>
/// Generic UI focus and tooltips in the editor: Tab shows the focus ring, and
/// hovering a group cell shows its explanation after the hover delay.
/// </summary>
public sealed class EditorFocusSceneTest : SceneTest
{
    public override string Name => "Editor focus and tooltips";
    public override string Slug => "editor-focus";

    protected override void SetUpSteps()
    {
        AddStep("open editor", () => Context.Scenes.Start(SceneKind.Editor));
        AddUntilStep(
            "wait for the groups grid",
            () => Context.Scenes.Current == SceneKind.Editor &&
                  Context.Entities.Query<EditorGroupCell>().Count == GroupLimits.Count);

        AddStep("move the pointer off the UI", () => Context.Pointer.Position = new Vector2(20, 20));
        AddStep("press Tab", () => Context.Keyboard.Press(Key.Tab));
        AddWaitStep("hold Tab", 2);
        AddStep("release Tab", () => Context.Keyboard.Release(Key.Tab));
        AddUntilStep(
            "a control has keyboard focus",
            () => Context.Entities
                .Query<Rectangle>()
                .AllTags(Tags.Get<Focused>())
                .Count > 0);
        AddAssert(
            "the focus ring is visible",
            () => Context.Entities
                .Query<UiFocusRingEdge>()
                .ToEntityList()
                .Any(edge => edge.GetComponent<Position>().Value != UiLayout.Hidden));

        AddStep("hover group 1", () => Context.Pointer.Position = CellCenter(0));
        AddUntilStep(
            "the group tooltip appears",
            () => TooltipText() == "Edit group 1",
            timeoutSeconds: 3);
        AddWaitStep("hold the tooltip", 20);

        AddStep("move the pointer away", () => Context.Pointer.Position = new Vector2(20, 20));
        AddUntilStep("the tooltip hides", () => TooltipText().Length == 0);
    }

    private Entity Cell(int index)
    {
        foreach (Entity entity in Context.Entities.Query<EditorGroupCell>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupCell>().Index == index)
                return entity;
        }

        return default;
    }

    private Vector2 CellCenter(int index)
    {
        Entity cell = Cell(index);
        Rectangle rect = cell.GetComponent<Rectangle>();
        Position position = cell.GetComponent<Position>();
        Vector2 world = UiTransform.WorldPosition(cell, position.Value);
        return new Rect2(world + rect.Bounds.Position, rect.Bounds.Size).GetCenter();
    }

    private string TooltipText()
    {
        foreach (Entity entity in Context.Entities.Query<Text, Drawable>().ToEntityList())
        {
            if (entity.GetComponent<Drawable>().ZIndex == UiTheme.TooltipLayer)
                return entity.GetComponent<Text>().Value;
        }

        return "";
    }
}
