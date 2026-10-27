using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Shows the <see cref="UiTooltip"/> of the hovered button just below the
/// pointer, widening the panel to fit. It hides while the pointer is pressed
/// and keeps the label on screen near the edges.
/// </summary>
public sealed class AnimatorTooltipSystem(IPointerInputSource pointer)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private const float Height = 24f;
    private const float CharWidth = 6.2f;
    private const float HorizontalPadding = 24f;
    private const float MinWidth = 96f;
    private const float MaxWidth = 360f;
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

        Entity hovered = Hovered();
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

        float width = Mathf.Clamp(
            HorizontalPadding + text.Length * CharWidth,
            MinWidth,
            MaxWidth);
        var size = new Vector2(width, Height);
        Vector2 topLeft = AnimatorMenuPlacement.Anchor(
            input.Position + new Vector2(OffsetX, OffsetY),
            size,
            ui.ScreenSize);
        Show(tooltip, text, size, topLeft);
    }

    /// <summary>The topmost hovered entity that carries an explanation.</summary>
    private Entity Hovered()
    {
        foreach (Entity entity in Query.Store
                     .Query<UiTooltip>()
                     .AllTags(Tags.Get<Hovered>())
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }

    private static void Show(Entity tooltip, string text, Vector2 size, Vector2 topLeft)
    {
        if (!tooltip.HasComponent<Rectangle>() || !tooltip.HasComponent<Text>())
            return;

        ref Rectangle rectangle = ref tooltip.GetComponent<Rectangle>();
        if (rectangle.Bounds.Size != size)
        {
            rectangle.Bounds = new Rect2(-size / 2, size);
            tooltip.AddTag<Dirty>();
        }

        ref Text label = ref tooltip.GetComponent<Text>();
        if (label.Value != text)
        {
            label.Value = text;
            tooltip.AddTag<Dirty>();
        }

        AnimatorMenuPlacement.Place(tooltip, topLeft, size);
    }

    private static void Hide(Entity tooltip)
    {
        if (tooltip.HasComponent<Text>())
        {
            ref Text label = ref tooltip.GetComponent<Text>();
            if (label.Value.Length > 0)
            {
                label.Value = "";
                tooltip.AddTag<Dirty>();
            }
        }

        AnimatorMenuPlacement.Place(tooltip, UiLayout.Hidden);
    }
}
