using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// F4 boxes around buttons and containers; the tree panel carries their legend. The entity picked in the F4
/// tree is drawn last in its own colour, so its children's boxes cannot hide it.
/// </summary>
public sealed class DebugElementOverlaySystem : QuerySystem<Rectangle, Position>
{
    private readonly IRectCanvas _canvas;
    private readonly Handle _overlay;
    private readonly OverlayLatch _latch;
    private readonly RetainedCanvasDraw _draw;

    public DebugElementOverlaySystem(IRectCanvas canvas, Handle overlay, IOverlayToggleSource toggle)
    {
        _canvas = canvas;
        _overlay = overlay;
        _latch = new OverlayLatch(toggle);
        _draw = new RetainedCanvasDraw(canvas, overlay);
        Filter.WithoutAnyTags(Tags.Get<DebugUi>());
    }

    protected override void OnUpdate()
    {
        if (_latch.Update())
        {
            _canvas.SetVisible(_overlay, _latch.Visible);
            if (!_latch.Visible)
                _draw.Reset();
        }

        if (!_latch.Visible)
            return;

        _draw.Begin();

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
                    DrawBounds(bounds, DebugPalette.ButtonFill, DebugPalette.ButtonBorder);
                if (container)
                    DrawBounds(bounds, DebugPalette.ContainerFill, DebugPalette.ContainerBorder);
            }

            if (entity.HasComponent<UiAnchor>())
            {
                UiAnchor anchor = entity.GetComponent<UiAnchor>();
                Vector2 pin = world +
                              rect.Bounds.Position +
                              rect.Bounds.Size * anchor.Anchor;
                _draw.Circle(pin, 6, DebugPalette.Pin);
                _draw.CircleOutline(
                    pin,
                    7,
                    DebugPalette.PinOutline,
                    1);
            }
        });

        if (selected is { } picked)
            DrawBounds(picked, DebugPalette.SelectedFill, DebugPalette.SelectedBorder, UiMetrics.Border.Medium);

        _draw.End();
    }

    private void DrawBounds(
        Rect2 bounds,
        Color fill,
        Color border,
        float width = UiMetrics.Border.Thin)
    {
        _draw.Rect(bounds, fill);
        _draw.Rect(
            new Rect2(bounds.Position, new Vector2(bounds.Size.X, width)),
            border);
        _draw.Rect(
            new Rect2(
                new Vector2(bounds.Position.X, bounds.End.Y - width),
                new Vector2(bounds.Size.X, width)),
            border);
        _draw.Rect(
            new Rect2(bounds.Position, new Vector2(width, bounds.Size.Y)),
            border);
        _draw.Rect(
            new Rect2(
                new Vector2(bounds.End.X - width, bounds.Position.Y),
                new Vector2(width, bounds.Size.Y)),
            border);
    }
}
