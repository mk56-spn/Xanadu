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
        Rid shaped = ts.CreateShapedText();
        ts.ShapedTextAddString(shaped, text, face.GetRids(), fontSize);
        ts.ShapedTextShape(shaped);

        var atlases = new System.Collections.Generic.Dictionary<(Rid, int), ImageTexture>();
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

            int textureIndex = (int)ts.FontGetGlyphTextureIdx(font, fontKey, index);
            if (textureIndex < 0)
                continue;

            if (!atlases.TryGetValue((font, textureIndex), out ImageTexture? atlas))
            {
                atlas = ImageTexture.CreateFromImage(
                    ts.FontGetTextureImage(font, fontKey, textureIndex));
                atlases.Add((font, textureIndex), atlas);
                keepAlive.Add(atlas);
            }

            Rect2 uv = ts.FontGetGlyphUVRect(font, fontKey, index);
            Vector2 atlasSize = atlas.GetSize();
            Vector2 topLeft = at + ts.FontGetGlyphOffset(font, fontKey, index);
            var dest = new Rect2I(
                new Vector2I(Mathf.RoundToInt(topLeft.X), Mathf.RoundToInt(topLeft.Y)),
                new Vector2I(Mathf.RoundToInt(glyphSize.X), Mathf.RoundToInt(glyphSize.Y)));

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

        ts.FreeRid(shaped);
    }

}
