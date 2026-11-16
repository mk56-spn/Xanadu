using System;
using System.Collections.Generic;
using Friflo.Engine.ECS.Systems;
using Xanadu.Host.Text;

namespace Xanadu.Labs.Experiments.EcsRender;

/// <summary>
/// Lays out every label whose text or size is no longer what its glyphs were
/// made from: shaped and found in the atlas once, from the label's own
/// top-left corner, and kept until the label changes.
/// </summary>
internal sealed class LabelLayoutSystem(HostText text, DrawList list) : QuerySystem<Label, LabelGlyphs>
{
    private readonly List<HostText.Placed> _placed = new();

    protected override void OnUpdate()
    {
        list.LaidOut = 0;
        foreach (var (labels, glyphs, entities) in Query.Chunks)
        {
            Span<Label> label = labels.Span;
            Span<LabelGlyphs> made = glyphs.Span;
            for (int i = 0; i < entities.Length; i++)
            {
                if (ReferenceEquals(made[i].From, label[i].Text) && made[i].Size == label[i].Size)
                    continue;

                made[i] = LayOut(label[i]);
                list.LaidOut++;
            }
        }
    }

    private LabelGlyphs LayOut(in Label label)
    {
        FontFace face = text.Face(FontKind.Mono);
        text.Place(label.Text, FontKind.Mono, label.Size, new Vector2(0, face.Ascent(label.Size)), _placed);
        var quads = new GlyphQuad[_placed.Count];
        const float texel = 1f / TextAtlas.Size;
        float width = 0;
        for (int i = 0; i < quads.Length; i++)
        {
            HostText.Placed p = _placed[i];
            AtlasGlyph g = p.Glyph;
            quads[i] = new GlyphQuad(p.X, p.Y, g.Width, g.Height,
                new Vector4(g.X, g.Y, g.X + g.Width, g.Y + g.Height) * texel, (short)g.Page);
            width = MathF.Max(width, p.X + g.Width);
        }

        return new LabelGlyphs { From = label.Text, Size = label.Size, Quads = quads, Width = width, Height = face.Height(label.Size) };
    }
}
