using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Draws shaped text into a <see cref="DrawableTexture2D"/> by blitting each
/// glyph out of the font atlas, so text can feed shaders without a viewport.
/// </summary>
public static class GodotGlyphBlitter
{
    // Copies one glyph out of the atlas: MODULATE carries the glyph's UV
    // rectangle (x, y, width, height), since it is the one per-blit value that
    // is captured when the blit is queued. Only coverage is kept.
    private const string GlyphShader = """
        shader_type texture_blit;
        render_mode blend_mix;
        uniform sampler2D source_texture0 : hint_blit_source0, filter_nearest, repeat_disable;

        void blit() {
            vec4 s = texture(source_texture0, MODULATE.xy + UV * MODULATE.zw);
            COLOR0 = vec4(1.0, 1.0, 1.0, s.a);
        }
        """;

    private static ShaderMaterial? _material;

    private static ShaderMaterial Material =>
        _material ??= new ShaderMaterial { Shader = new Shader { Code = GlyphShader } };

    /// <summary>
    /// Blits <paramref name="text"/> into <paramref name="target"/> as white
    /// coverage in the alpha channel, its pen starting at <paramref name="pen"/>
    /// on the baseline. The atlas textures the queued blits read are added to
    /// <paramref name="keepAlive"/>; hold it until the frame has rendered.
    /// </summary>
    public static void Blit(
        DrawableTexture2D target,
        ICollection<ImageTexture> keepAlive,
        string text,
        int fontSize,
        Font face,
        Vector2 pen)
    {
        TextServer ts = TextServerManager.GetPrimaryInterface();
        var atlases = new System.Collections.Generic.Dictionary<(Rid, int), ImageTexture>();
        foreach (Glyph glyph in Shape(text, fontSize, face, pen))
        {
            int textureIndex = (int)ts.FontGetGlyphTextureIdx(glyph.Font, glyph.Key, glyph.Index);
            if (textureIndex < 0)
                continue;

            if (!atlases.TryGetValue((glyph.Font, textureIndex), out ImageTexture? atlas))
            {
                atlas = ImageTexture.CreateFromImage(
                    ts.FontGetTextureImage(glyph.Font, glyph.Key, textureIndex));
                atlases.Add((glyph.Font, textureIndex), atlas);
                keepAlive.Add(atlas);
            }

            Rect2 uv = ts.FontGetGlyphUVRect(glyph.Font, glyph.Key, glyph.Index);
            Vector2 atlasSize = atlas.GetSize();
            var dest = new Rect2I(
                new Vector2I(Mathf.RoundToInt(glyph.Ink.Position.X), Mathf.RoundToInt(glyph.Ink.Position.Y)),
                new Vector2I(Mathf.RoundToInt(glyph.Ink.Size.X), Mathf.RoundToInt(glyph.Ink.Size.Y)));

            target.BlitRect(
                dest,
                atlas,
                new Color(
                    uv.Position.X / atlasSize.X,
                    uv.Position.Y / atlasSize.Y,
                    uv.Size.X / atlasSize.X,
                    uv.Size.Y / atlasSize.Y),
                0,
                Material);
        }
    }

    /// <summary>
    /// The box the drawn glyphs of <paramref name="text"/> cover, relative to a
    /// pen at the origin on the baseline; empty when nothing would be drawn.
    /// </summary>
    public static Rect2 InkBounds(string text, int fontSize, Font face)
    {
        Rect2? bounds = null;
        foreach (Glyph glyph in Shape(text, fontSize, face, Vector2.Zero))
            bounds = bounds is { } sofar ? sofar.Merge(glyph.Ink) : glyph.Ink;

        return bounds ?? new Rect2();
    }

    /// <summary>One drawn glyph: its font, size key, index and the box it covers on the target.</summary>
    private readonly record struct Glyph(Rid Font, Vector2I Key, long Index, Rect2 Ink);

    /// <summary>Shapes the text and renders each glyph that draws something into its font's atlas.</summary>
    private static List<Glyph> Shape(string text, int fontSize, Font face, Vector2 pen)
    {
        TextServer ts = TextServerManager.GetPrimaryInterface();
        Rid shaped = ts.CreateShapedText();
        ts.ShapedTextAddString(shaped, text, face.GetRids(), fontSize);
        ts.ShapedTextShape(shaped);

        var glyphs = new List<Glyph>();
        foreach (Dictionary glyph in ts.ShapedTextGetGlyphs(shaped))
        {
            long index = glyph["index"].AsInt64();
            Rid font = glyph["font_rid"].AsRid();
            var fontKey = new Vector2I(glyph["font_size"].AsInt32(), 0);
            Vector2 at = pen + glyph["offset"].AsVector2();
            pen.X += glyph["advance"].AsSingle() * glyph["repeat"].AsInt32();

            if (index == 0 || !font.IsValid)
                continue;

            ts.FontRenderGlyph(font, fontKey, index);
            Vector2 glyphSize = ts.FontGetGlyphSize(font, fontKey, index);
            if (glyphSize.X <= 0 || glyphSize.Y <= 0)
                continue;

            Vector2 topLeft = at + ts.FontGetGlyphOffset(font, fontKey, index);
            glyphs.Add(new Glyph(font, fontKey, index, new Rect2(topLeft, glyphSize)));
        }

        ts.FreeRid(shaped);
        return glyphs;
    }
}
