using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>Draws dirty ECS image entities.</summary>
public sealed class ImageDrawSystem : QuerySystem<Rectangle, Drawable, VisualItem, UiImage>
{
    private readonly IRectCanvas _canvas;

    public ImageDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new DrawImages(_canvas, CommandBuffer));
    }

    private readonly record struct DrawImages(IRectCanvas Canvas, CommandBuffer Buffer) : IEachEntity<Rectangle, Drawable, VisualItem, UiImage>
    {
        public void Execute(ref Rectangle rect, ref Drawable _, ref VisualItem visual, ref UiImage image, int id)
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            Canvas.Clear(visual.Value);
            Canvas.DrawTexture(visual.Value, rect.Bounds, image.Texture);
            Buffer.RemoveTag<Dirty>(id);
        }
    }
}
