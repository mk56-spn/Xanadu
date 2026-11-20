using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

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
        Query.EachEntity(new DrawRects(_canvas, Query.Store, CommandBuffer));
    }

    private readonly record struct DrawRects(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<Rectangle, Drawable, VisualRid, FillColor>
    {
        public void Execute(ref Rectangle rect, ref Drawable _, ref VisualRid visual, ref FillColor fillColor, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            Canvas.Clear(visual.Value);
            bool hasContainer = entity.TryGetComponent(out UiContainer container);
            float skew = hasContainer ? container.Skew : 0;
            float chamfer = entity.TryGetComponent(out UiChamfer cut) ? cut.Size : 0;
            // An invisible fill (most text and layout boxes) draws nothing: an empty quad
            // between two glyphs breaks the renderer's batching on the texture change.
            // A material may still paint over a clear fill, so those keep it.
            if (fillColor.Value.A <= 0f && !entity.HasComponent<UiMaterial>())
            {
            }
            else if (skew != 0 || chamfer > 0)
            {
                Canvas.DrawPolygon(
                    visual.Value,
                    UiOutline.Points(rect.Bounds, skew, chamfer),
                    fillColor.Value);
            }
            else
            {
                Canvas.DrawRect(visual.Value, rect.Bounds, fillColor.Value);
            }

            if (entity.TryGetComponent(out UiBorder border) &&
                border.Color.A > 0 &&
                border.Width > 0)
            {
                Vector2[] outline = UiOutline.Points(rect.Bounds, skew, chamfer);
                Canvas.DrawPolyline(
                    visual.Value,
                    [.. outline, outline[0]],
                    border.Color,
                    border.Width);
            }

            // CanvasItemClear resets the clip flag, so re-apply it after every
            // clear, as Godot's Control does on each NOTIFICATION_DRAW.
            if (hasContainer && container.Clip)
            {
                CanvasServer.Current.SetCustomRect(visual.Value, true, rect.Bounds);
                CanvasServer.Current.SetClip(visual.Value, true);
            }

            Buffer.RemoveTag<Dirty>(id);
        }
    }
}
