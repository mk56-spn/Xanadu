using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

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
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref rect, ref _, ref visual, ref gradient, entity) =>
        {
            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            _canvas.Clear(visual.Value);

            _canvas.DrawGradientRect(visual.Value, rect.Bounds, gradient.Start, gradient.End, gradient.Horizontal);

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
