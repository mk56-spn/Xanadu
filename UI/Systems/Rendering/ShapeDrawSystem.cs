using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Systems.Rendering;

/// <summary>Draws dirty polygon and polyline shapes.</summary>
public sealed class ShapeDrawSystem : QuerySystem<Drawable, VisualItem, UiShape>
{
    private readonly IRectCanvas _canvas;

    public ShapeDrawSystem(IRectCanvas canvas)
    {
        _canvas = canvas;
        Filter.AllTags(Tags.Get<Dirty>());
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new DrawShapes(_canvas, Query.Store, CommandBuffer));
    }

    private readonly record struct DrawShapes(IRectCanvas Canvas, EntityStore Store, CommandBuffer Buffer) : IEachEntity<Drawable, VisualItem, UiShape>
    {
        public void Execute(ref Drawable _, ref VisualItem visual, ref UiShape shape, int id)
        {
            Canvas.Clear(visual.Value);

            if (Store.GetEntityById(id).TryGetComponent(out Glow glow))
            {
                Canvas.DrawShapeGlow(
                    visual.Value,
                    shape.Points,
                    shape.Closed,
                    shape.Fill.A > 0,
                    shape.Stroke.A > 0 ? shape.StrokeWidth : 0,
                    glow);
            }

            if (shape.Fill.A > 0)
                Canvas.DrawPolygon(visual.Value, shape.Points, shape.Fill);

            if (shape.Stroke.A > 0 && shape.StrokeWidth > 0)
            {
                ReadOnlySpan<Vector2> outline = shape.Points;
                if (shape.Closed)
                {
                    var closed = new Vector2[outline.Length + 1];
                    outline.CopyTo(closed);
                    closed[^1] = outline[0];
                    outline = closed;
                }

                Canvas.DrawPolyline(
                    visual.Value,
                    outline,
                    shape.Stroke,
                    shape.StrokeWidth);
            }

            Buffer.RemoveTag<Dirty>(id);
        }
    }
}
