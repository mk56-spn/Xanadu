using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Writer.Systems;

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
            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
