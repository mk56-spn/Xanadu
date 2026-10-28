using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Draws a blurred glow behind a polygon or polyline. The shape is rasterised
/// into a drawable texture by a blit shader that evaluates its outline
/// analytically, then blurred like text, so any shape can glow.
/// </summary>
internal sealed class GodotShapeGlow
{
    /// <summary>Most outline points the mask shader reads.</summary>
    public const int MaxPoints = 64;

    // Coverage of a stroked and/or filled outline: distance to each segment for
    // the stroke, an even-odd crossing count for the fill.
    private const string MaskCode = """
        shader_type texture_blit;
        render_mode blend_disabled;
        uniform sampler2D source_texture0 : hint_blit_source0, filter_linear, repeat_disable;
        uniform vec2 size = vec2(64.0);
        uniform vec2 origin = vec2(0.0);
        uniform vec2 points[64];
        uniform int count = 0;
        uniform float half_width = 0.0;
        uniform bool closed = true;
        uniform bool filled = false;

        void blit() {
            vec2 p = UV * size + origin;
            float dist = 1e9;
            bool inside = false;
            for (int i = 0; i < 64; i++) {
                if (i >= count) {
                    break;
                }
                int j = i + 1;
                if (j >= count) {
                    if (!closed) {
                        break;
                    }
                    j = 0;
                }
                vec2 a = points[i];
                vec2 b = points[j];
                vec2 ab = b - a;
                float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
                dist = min(dist, length(p - a - ab * t));
                if ((a.y > p.y) != (b.y > p.y) &&
                        p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) {
                    inside = !inside;
                }
            }
            float stroke = half_width > 0.0
                ? 1.0 - smoothstep(half_width - 0.75, half_width + 0.75, dist)
                : 0.0;
            float fill = (filled && closed && inside) ? 1.0 : 0.0;
            COLOR0 = vec4(1.0, 1.0, 1.0, max(stroke, fill));
        }
        """;

    private static Shader? _maskShader;
    private static ImageTexture? _blank;

    private sealed class State
    {
        public Rid GlowItem;
        public readonly DrawableTexture2D Mask = new();
        public readonly ShaderMaterial MaskMaterial = new() { Shader = MaskShader };
        public readonly GodotBlur Blur = new();
    }

    private static Shader MaskShader => _maskShader ??= new Shader { Code = MaskCode };

    // A blit needs a source texture even when the shader ignores it.
    private static ImageTexture Blank =>
        _blank ??= ImageTexture.CreateFromImage(
            Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));

    private readonly Dictionary<Rid, State> _states = new();

    public void Clear(Rid canvas)
    {
        if (_states.TryGetValue(canvas, out State? state))
            RenderingServer.CanvasItemClear(state.GlowItem);
    }

    public void Draw(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        bool closed,
        bool filled,
        float strokeWidth,
        ShapeGlow glow)
    {
        if (points.Length < 2 && !(filled && points.Length >= 3))
            return;

        int count = Math.Min(points.Length, MaxPoints);
        Vector2 min = points[0];
        Vector2 max = points[0];
        var padded = new Vector2[MaxPoints];
        for (int i = 0; i < count; i++)
        {
            padded[i] = points[i];
            min = min.Min(points[i]);
            max = max.Max(points[i]);
        }

        float pad = GodotBlur.Padding(glow.Radius) + strokeWidth / 2;
        Vector2 origin = (min - new Vector2(pad, pad)).Floor();
        var size = new Vector2I(
            Mathf.CeilToInt(max.X - origin.X + pad),
            Mathf.CeilToInt(max.Y - origin.Y + pad));

        State state = StateFor(canvas);
        ShaderMaterial mask = state.MaskMaterial;
        mask.SetShaderParameter("size", new Vector2(size.X, size.Y));
        mask.SetShaderParameter("origin", origin);
        mask.SetShaderParameter("points", padded);
        mask.SetShaderParameter("count", count);
        mask.SetShaderParameter("half_width", strokeWidth / 2);
        mask.SetShaderParameter("closed", closed);
        mask.SetShaderParameter("filled", filled);

        state.Mask.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        state.Mask.BlitRect(new Rect2I(Vector2I.Zero, size), Blank, null, 0, mask);
        state.Blur.Run(state.Mask, size, glow.Radius, glow.Color, glow.Intensity);

        // Setup can replace the texture's RID, so the quad is added afterwards.
        RenderingServer.CanvasItemClear(state.GlowItem);
        RenderingServer.CanvasItemAddTextureRect(
            state.GlowItem,
            new Rect2(origin, size),
            state.Blur.Result.GetRid());
    }

    private State StateFor(Rid canvas)
    {
        if (_states.TryGetValue(canvas, out State? existing))
            return existing;

        // Drawn behind the shape's own item, so the glow sits under the fill.
        Rid item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, canvas);
        RenderingServer.CanvasItemSetDrawBehindParent(item, true);
        RenderingServer.CanvasItemSetDefaultTextureFilter(
            item, RenderingServer.CanvasItemTextureFilter.Linear);
        RenderingServer.CanvasItemSetDefaultTextureRepeat(
            item, RenderingServer.CanvasItemTextureRepeat.Disabled);

        var state = new State { GlowItem = item };
        _states.Add(canvas, state);
        return state;
    }
}
