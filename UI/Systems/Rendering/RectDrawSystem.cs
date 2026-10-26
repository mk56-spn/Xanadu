using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Rendering;

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
            _canvas.DrawRect(visual.Value, rect.Bounds, fillColor.Value);

            // CanvasItemClear resets the clip flag, so re-apply it after every
            // clear, as Godot's Control does on each NOTIFICATION_DRAW.
            if (entity.TryGetComponent(out UiContainer container) && container.Clip)
            {
                RenderingServer.CanvasItemSetCustomRect(visual.Value, true, rect.Bounds);
                RenderingServer.CanvasItemSetClip(visual.Value, true);
            }

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
