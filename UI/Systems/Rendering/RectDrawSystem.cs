using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

/// <summary>
/// Clears and redraws dirty rectangles into their entity canvas items.
/// Clean rectangles keep their existing canvas commands.
/// </summary>
public sealed class RectDrawSystem : QuerySystem<Rectangle, Drawable, VisualRid, FillColor>
{
    private readonly IRectCanvas _canvas;

    public RectDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref rect, ref _, ref visual, ref fillColor, entity) =>
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            _canvas.Clear(visual.Value);
            bool hasContainer = entity.TryGetComponent(out UiContainer container);
            float skew = hasContainer ? container.Skew : 0;
            float chamfer = entity.TryGetComponent(out UiChamfer cut) ? cut.Size : 0;
            bool diamond = entity.HasComponent<UiDiamond>();
            if (diamond)
            {
                _canvas.DrawPolygon(
                    visual.Value,
                    UiOutline.Diamond(rect.Bounds),
                    fillColor.Value);
            }
            else if (skew != 0 || chamfer > 0)
            {
                _canvas.DrawPolygon(
                    visual.Value,
                    UiOutline.Points(rect.Bounds, skew, chamfer),
                    fillColor.Value);
            }
            else
            {
                _canvas.DrawRect(visual.Value, rect.Bounds, fillColor.Value);
            }

            if (entity.TryGetComponent(out UiBorder border) &&
                border.Color.A > 0 &&
                border.Width > 0)
            {
                Vector2[] outline = diamond
                    ? UiOutline.Diamond(rect.Bounds)
                    : UiOutline.Points(rect.Bounds, skew, chamfer);
                _canvas.DrawPolyline(
                    visual.Value,
                    [.. outline, outline[0]],
                    border.Color,
                    border.Width);
            }

            // CanvasItemClear resets the clip flag, so re-apply it after every
            // clear, as Godot's Control does on each NOTIFICATION_DRAW.
            if (hasContainer && container.Clip)
            {
                RenderingServer.CanvasItemSetCustomRect(visual.Value, true, rect.Bounds);
                RenderingServer.CanvasItemSetClip(visual.Value, true);
            }

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
