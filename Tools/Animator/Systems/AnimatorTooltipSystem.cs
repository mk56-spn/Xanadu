using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Systems;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// Shows the <see cref="UiTooltip"/> of the hovered button just below the
/// pointer, widening the panel to fit. It hides while the pointer is pressed
/// and keeps the label on screen near the edges.
/// </summary>
public sealed class AnimatorTooltipSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private const float Height = 24f;
    private const float OffsetX = 14f;
    private const float OffsetY = 20f;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        AnimatorUi ui = session.GetComponent<AnimatorUi>();
        Entity tooltip = ui.Tooltip;
        if (tooltip.IsNull)
            return;

        Entity hovered = UiTooltipPanel.Hovered(Query.Store);
        PointerInput input = pointer.Read();
        if (hovered.IsNull || input.Pressed || input.Middle)
        {
            Hide(tooltip);
            return;
        }

        string text = hovered.GetComponent<UiTooltip>().Text;
        if (string.IsNullOrEmpty(text))
        {
            Hide(tooltip);
            return;
        }

        Vector2 size = UiTooltipPanel.Size(text, Height);
        Vector2 topLeft = AnimatorMenuPlacement.Anchor(
            input.Position + new Vector2(OffsetX, OffsetY),
            size,
            ui.ScreenSize);
        Show(tooltip, text, size, topLeft);
    }

    private static void Show(Entity tooltip, string text, Vector2 size, Vector2 topLeft)
    {
        if (!tooltip.HasComponent<Rectangle>() || !tooltip.HasComponent<Text>())
            return;

        UiTooltipPanel.SetContent(tooltip, text, size);
        AnimatorMenuPlacement.Place(tooltip, topLeft, size);
    }

    private static void Hide(Entity tooltip)
    {
        UiTooltipPanel.Clear(tooltip);
        AnimatorMenuPlacement.Hide(tooltip);
    }
}
