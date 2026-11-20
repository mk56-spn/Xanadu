using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>Draws dirty linear-gradient rectangles as one smooth ramp.</summary>
public sealed class GradientDrawSystem : QuerySystem<Rectangle, Drawable, VisualRid, LinearGradient>
{
    private readonly IRectCanvas _canvas;

    public GradientDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new DrawGradients(_canvas, CommandBuffer));
    }

    private readonly record struct DrawGradients(IRectCanvas Canvas, CommandBuffer Buffer) : IEachEntity<Rectangle, Drawable, VisualRid, LinearGradient>
    {
        public void Execute(ref Rectangle rect, ref Drawable _, ref VisualRid visual, ref LinearGradient gradient, int id)
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            Canvas.Clear(visual.Value);

            Canvas.DrawGradientRect(visual.Value, rect.Bounds, gradient.Start, gradient.End, gradient.Horizontal);

            Buffer.RemoveTag<Dirty>(id);
        }
    }
}
