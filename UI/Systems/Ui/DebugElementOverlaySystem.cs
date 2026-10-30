using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// F4 overlay showing button and container bounds. The entity picked in the F4
/// tree is drawn last in its own colour, so its children's boxes cannot hide it.
/// </summary>
public sealed class DebugElementOverlaySystem(
    IRectCanvas canvas,
    Rid overlay,
    IOverlayToggleSource toggle)
    : QuerySystem<Rectangle, Position>
{
    private static readonly Color ButtonFill = new(1f, 0.2f, 0.2f, 0.16f);
    private static readonly Color ButtonBorder = new(1f, 0.3f, 0.3f, 0.8f);
    private static readonly Color ContainerFill = new(1f, 0.8f, 0.2f, 0.10f);
    private static readonly Color ContainerBorder = new(1f, 0.85f, 0.25f, 0.7f);
    private static readonly Color SelectedFill = new(0.2f, 0.9f, 1f, 0.3f);
    private static readonly Color SelectedBorder = new(0.3f, 0.95f, 1f, 1f);
    private static readonly Color PinColor = new(0.3f, 1f, 0.45f, 0.95f);
    private readonly OverlayLatch _latch = new(toggle);

    protected override void OnUpdate()
    {
        if (_latch.Update())
            canvas.SetVisible(overlay, _latch.Visible);
        canvas.Clear(overlay);
        if (!_latch.Visible)
            return;

        Rect2? selected = null;
        Query.ForEachEntity((ref rect, ref position, entity) =>
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            bool button = entity.HasComponent<ButtonVisual>();
            bool container = entity.HasComponent<UiContainer>();
            if (!button && !container)
                return;

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            var bounds = new Rect2(
                world + rect.Bounds.Position,
                rect.Bounds.Size);
            if (entity.Tags.Has<UiTreeOverlaySelected>())
            {
                selected = bounds;
            }
            else
            {
                if (button)
                    DrawBounds(bounds, ButtonFill, ButtonBorder);
                if (container)
                    DrawBounds(bounds, ContainerFill, ContainerBorder);
            }

            if (entity.HasComponent<UiAnchor>())
            {
                UiAnchor anchor = entity.GetComponent<UiAnchor>();
                Vector2 pin = world +
                              rect.Bounds.Position +
                              rect.Bounds.Size * anchor.Anchor;
                canvas.DrawCircle(overlay, pin, 6, PinColor);
                canvas.DrawCircleOutline(
                    overlay,
                    pin,
                    7,
                    new Color(0.05f, 0.2f, 0.08f, 0.95f),
                    1);
            }
        });

        if (selected is { } picked)
            DrawBounds(picked, SelectedFill, SelectedBorder, UiMetrics.Border.Medium);

        canvas.DrawRect(
            overlay,
            new Rect2(16, 16, 310, 58),
            new Color(0.02f, 0.03f, 0.06f, 0.85f));
        canvas.DrawTextLeft(
            overlay,
            new Rect2(24, 20, 294, 16),
            "F4 BOUNDS",
            new Color(1f, 0.9f, 0.35f));
        canvas.DrawTextLeft(
            overlay,
            new Rect2(24, 36, 294, 16),
            "RED BUTTONS   YELLOW CONTAINERS",
            new Color(0.85f, 0.9f, 1f));
        canvas.DrawTextLeft(
            overlay,
            new Rect2(24, 52, 294, 16),
            "GREEN PINS",
            PinColor);
        canvas.DrawTextLeft(
            overlay,
            new Rect2(150, 52, 168, 16),
            "CYAN SELECTED",
            SelectedBorder);
    }

    private void DrawBounds(
        Rect2 bounds,
        Color fill,
        Color border,
        float width = UiMetrics.Border.Thin)
    {
        canvas.DrawRect(overlay, bounds, fill);
        canvas.DrawRect(
            overlay,
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)),
            border);
        canvas.DrawRect(
            overlay,
            new Rect2(
                new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)),
            border);
        canvas.DrawRect(
            overlay,
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)),
            border);
        canvas.DrawRect(
            overlay,
            new Rect2(
                new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)),
            border);
    }
}
