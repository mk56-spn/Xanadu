using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Rendering;

/// <summary>Draws dirty polygon and polyline shapes.</summary>
public sealed class ShapeDrawSystem : QuerySystem<Drawable, VisualRid, UiShape>
{
    private readonly IRectCanvas _canvas;

    public ShapeDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref _, ref visual, ref shape, entity) =>
        {
            _canvas.Clear(visual.Value);

            if (shape.Fill.A > 0)
                _canvas.DrawPolygon(visual.Value, shape.Points, shape.Fill);

            if (shape.Stroke.A > 0 && shape.StrokeWidth > 0)
            {
                ReadOnlySpan<Godot.Vector2> outline = shape.Points;
                if (shape.Closed)
                {
                    var closed = new Godot.Vector2[outline.Length + 1];
                    outline.CopyTo(closed);
                    closed[^1] = outline[0];
                    outline = closed;
                }

                _canvas.DrawPolyline(
                    visual.Value,
                    outline,
                    shape.Stroke,
                    shape.StrokeWidth);
            }

            buffer.RemoveTag<Dirty>(entity.Id);
        });
    }
}
