using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Systems.Canvas;

namespace Writer.Systems.Rendering;

/// <summary>Draws dirty ECS image entities.</summary>
public sealed class ImageDrawSystem : QuerySystem<Rectangle, Drawable, VisualRid, UiImage>
{
    private readonly IRectCanvas _canvas;

    public ImageDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref rect, ref _, ref visual, ref image, entity) =>
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            _canvas.Clear(visual.Value);
            _canvas.DrawTexture(visual.Value, rect.Bounds, image.Texture);
            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
