using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Xanadu.Ecs;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Draws text with a blurred glow behind it, entirely on drawable textures.
/// The text is shaped by the text server and each glyph is blitted from the
/// font atlas into a drawable texture, which two blur blits then spread into
/// the glow. No viewport is involved, so the glow is ready the frame the text
/// is drawn.
/// </summary>
internal sealed class GodotTextGlow
{
    private const int Taps = 16;
    private const float TextPadding = 4;

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

    // One axis of a separable gaussian. Taps are dense so the falloff is
    // smooth rather than showing stamped copies of the glyphs.
    private const string BlurShader = """
        shader_type texture_blit;
        render_mode blend_disabled;
        uniform sampler2D source_texture0 : hint_blit_source0, filter_linear, repeat_disable;
        uniform vec2 texel = vec2(0.01);
        uniform vec2 direction = vec2(1.0, 0.0);
        uniform float step_px = 1.5;
        uniform float intensity = 1.0;
        uniform vec4 glow_color : source_color = vec4(1.0);
        uniform bool final_pass = false;

        void blit() {
            vec2 step_uv = direction * step_px * texel;
            float sum = 0.0;
            float total = 0.0;
            for (int i = -16; i <= 16; i++) {
                float f = float(i) / 5.0;
                float w = exp(-0.5 * f * f);
                sum += texture(source_texture0, UV + step_uv * float(i)).a * w;
                total += w;
            }
            float a = sum / total;
            if (final_pass) {
                COLOR0 = vec4(glow_color.rgb, clamp(a * intensity, 0.0, 1.0) * glow_color.a);
            } else {
                COLOR0 = vec4(1.0, 1.0, 1.0, a);
            }
        }
        """;

    private static Shader? _glyphShader;
    private static Shader? _blurShader;

    private static Shader GlyphShaderResource => _glyphShader ??= new Shader { Code = GlyphShader };
    private static Shader BlurShaderResource => _blurShader ??= new Shader { Code = BlurShader };

    private sealed class State
    {
        public Rid GlowItem, CrispItem;
        public readonly DrawableTexture2D Glyphs = new();
        public readonly DrawableTexture2D Horizontal = new();
        public readonly DrawableTexture2D Vertical = new();
        public readonly ShaderMaterial GlyphMaterial = new() { Shader = GlyphShaderResource };
        public readonly ShaderMaterial HorizontalMaterial = new() { Shader = BlurShaderResource };
        public readonly ShaderMaterial VerticalMaterial = new() { Shader = BlurShaderResource };

        // Queued blits read these textures when the frame renders.
        public readonly List<ImageTexture> Atlases = new();
    }

    private readonly System.Collections.Generic.Dictionary<Rid, State> _states = new();

    public void Clear(Rid canvas)
    {
        if (!_states.TryGetValue(canvas, out State? state))
            return;

        RenderingServer.CanvasItemClear(state.GlowItem);
        RenderingServer.CanvasItemClear(state.CrispItem);
    }

    public void Draw(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        HorizontalAlignment alignment,
        Font face,
        TextGlow glow)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float stepPx = Mathf.Max(1f, glow.Radius / 8f);
        float pad = Mathf.Ceil(Taps * stepPx) + 2;
        var size = new Vector2I(
            Mathf.CeilToInt(bounds.Size.X + pad * 2),
            Mathf.CeilToInt(bounds.Size.Y + pad * 2));

        State state = StateFor(canvas);
        state.Atlases.Clear();

        Vector2 origin = TextOrigin(bounds, text, fontSize, alignment, face);
        state.Glyphs.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        BlitGlyphs(state, text, fontSize, face, origin - bounds.Position + new Vector2(pad, pad));

        var area = new Rect2I(Vector2I.Zero, size);
        SetParams(state.HorizontalMaterial, new Vector2(1, 0), stepPx, glow, false, size);
        state.Horizontal.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        state.Horizontal.BlitRect(area, state.Glyphs, null, 0, state.HorizontalMaterial);

        SetParams(state.VerticalMaterial, new Vector2(0, 1), stepPx, glow, true, size);
        state.Vertical.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        state.Vertical.BlitRect(area, state.Horizontal, null, 0, state.VerticalMaterial);

        // Setup can replace the texture's RID, so the quad is added afterwards.
        RenderingServer.CanvasItemClear(state.GlowItem);
        RenderingServer.CanvasItemAddTextureRect(
            state.GlowItem,
            new Rect2(bounds.Position - new Vector2(pad, pad), size),
            state.Vertical.GetRid());

        RenderingServer.CanvasItemClear(state.CrispItem);
        GodotRectCanvas.DrawTextAligned(
            state.CrispItem, bounds, text, color, fontSize, alignment, face);
    }

    /// <summary>Where the text's pen starts, on the baseline, in canvas space.</summary>
    private static Vector2 TextOrigin(
        Rect2 bounds, string text, int fontSize, HorizontalAlignment alignment, Font face)
    {
        float baseline = bounds.Position.Y +
                         (bounds.Size.Y - face.GetHeight(fontSize)) / 2 +
                         face.GetAscent(fontSize);
        float width = face.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        float room = bounds.Size.X - TextPadding * 2;
        float x = alignment switch
        {
            HorizontalAlignment.Right => room - width,
            HorizontalAlignment.Center => (room - width) / 2,
            _ => 0,
        };
        return new Vector2(bounds.Position.X + TextPadding + Mathf.Max(0, x), baseline);
    }

    private static void BlitGlyphs(
        State state, string text, int fontSize, Font face, Vector2 pen)
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
                state.Atlases.Add(atlas);
            }

            Rect2 uv = ts.FontGetGlyphUVRect(font, fontKey, index);
            Vector2 atlasSize = atlas.GetSize();
            Vector2 topLeft = at + ts.FontGetGlyphOffset(font, fontKey, index);
            var dest = new Rect2I(
                new Vector2I(Mathf.RoundToInt(topLeft.X), Mathf.RoundToInt(topLeft.Y)),
                new Vector2I(Mathf.RoundToInt(glyphSize.X), Mathf.RoundToInt(glyphSize.Y)));

            state.Glyphs.BlitRect(
                dest,
                atlas,
                new Color(
                    uv.Position.X / atlasSize.X,
                    uv.Position.Y / atlasSize.Y,
                    uv.Size.X / atlasSize.X,
                    uv.Size.Y / atlasSize.Y),
                0,
                state.GlyphMaterial);
        }

        ts.FreeRid(shaped);
    }

    private static void SetParams(
        ShaderMaterial material, Vector2 direction, float stepPx, TextGlow glow, bool final,
        Vector2I size)
    {
        material.SetShaderParameter("texel", new Vector2(1f / size.X, 1f / size.Y));
        material.SetShaderParameter("direction", direction);
        material.SetShaderParameter("step_px", stepPx);
        material.SetShaderParameter("intensity", glow.Intensity);
        material.SetShaderParameter("glow_color", glow.Color);
        material.SetShaderParameter("final_pass", final);
    }

    private State StateFor(Rid canvas)
    {
        if (_states.TryGetValue(canvas, out State? existing))
            return existing;

        // Children draw after the parent in creation order: glow, then text.
        var state = new State
        {
            GlowItem = CreateChild(canvas),
            CrispItem = CreateChild(canvas),
        };
        _states.Add(canvas, state);
        return state;
    }

    private static Rid CreateChild(Rid parent)
    {
        Rid item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, parent);
        RenderingServer.CanvasItemSetDefaultTextureFilter(
            item, RenderingServer.CanvasItemTextureFilter.Linear);
        RenderingServer.CanvasItemSetDefaultTextureRepeat(
            item, RenderingServer.CanvasItemTextureRepeat.Disabled);
        return item;
    }
}
