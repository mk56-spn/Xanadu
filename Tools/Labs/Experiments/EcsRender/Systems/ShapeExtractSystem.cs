using System;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>One instance per shape on the stage.</summary>
internal sealed class ShapeExtractSystem(DrawList list) : QuerySystem<Placed, Depth, Tint, Shape>
{
    protected override void OnUpdate()
    {
        Vector2 stage = list.Stage;
        foreach (var (placed, depths, tints, shapes, entities) in Query.Chunks)
        {
            Span<Placed> place = placed.Span;
            Span<Depth> depth = depths.Span;
            Span<Tint> tint = tints.Span;
            Span<Shape> shape = shapes.Span;
            list.Reserve(entities.Length);
            for (int i = 0; i < entities.Length; i++)
            {
                Vector2 at = place[i].Position, size = shape[i].Size;
                // Turned any way, a shape stays inside a circle through its corners.
                float reach = MathF.Max(size.X, size.Y) * 0.7072f + 1;
                if (at.X + reach < 0 || at.Y + reach < 0 || at.X - reach > stage.X || at.Y - reach > stage.Y)
                {
                    list.Culled++;
                    continue;
                }

                Color c = tint[i].Value;
                list.Add(depth[i], DrawList.NoPage, new DrawInstance
                {
                    Box = new Vector4(at.X, at.Y, size.X, size.Y),
                    Colour = new Vector4(c.R, c.G, c.B, c.A),
                    Look = new Vector4(place[i].Turn, (float)shape[i].Kind, shape[i].Edge, 0),
                });
            }
        }
    }
}
