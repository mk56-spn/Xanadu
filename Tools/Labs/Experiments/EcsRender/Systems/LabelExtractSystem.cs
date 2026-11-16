using System;
using Friflo.Engine.ECS.Systems;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>One instance per glyph of every label on the stage, from the glyphs <see cref="LabelLayoutSystem"/> keeps.</summary>
internal sealed class LabelExtractSystem(DrawList list) : QuerySystem<Placed, Depth, Tint, LabelGlyphs>
{
    protected override void OnUpdate()
    {
        Vector2 stage = list.Stage;
        foreach (var (placed, depths, tints, glyphs, entities) in Query.Chunks)
        {
            Span<Placed> place = placed.Span;
            Span<Depth> depth = depths.Span;
            Span<Tint> tint = tints.Span;
            Span<LabelGlyphs> made = glyphs.Span;
            for (int i = 0; i < entities.Length; i++)
            {
                ref readonly LabelGlyphs run = ref made[i];
                if (run.Quads is not { } quads)
                    continue;

                // Whole pixels, so glyph images land on the pixels they were drawn for.
                float x = MathF.Round(place[i].Position.X), y = MathF.Round(place[i].Position.Y);
                if (x + run.Width < 0 || y + run.Height < 0 || x > stage.X || y > stage.Y)
                {
                    list.Culled++;
                    continue;
                }

                Color c = tint[i].Value;
                var colour = new Vector4(c.R, c.G, c.B, c.A);
                list.Reserve(quads.Length);
                foreach (ref readonly GlyphQuad q in quads.AsSpan())
                {
                    list.Add(depth[i], q.Page, new DrawInstance
                    {
                        Box = new Vector4(x + q.X + q.Width / 2, y + q.Y + q.Height / 2, q.Width, q.Height),
                        Uv = q.Uv,
                        Colour = colour,
                        Look = new Vector4(0, DrawInstance.KindGlyph, 0, 0),
                    });
                }
            }
        }
    }
}
