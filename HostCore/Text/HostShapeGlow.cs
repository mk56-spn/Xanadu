using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xanadu.Ecs;
using Xanadu.Host.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Host.Text;

/// <summary>
/// The glow behind a polygon or polyline (docs/host.md §5.5), as
/// <c>GodotShapeGlow</c> drew it: the outline's coverage (distance to each
/// segment for the stroke, an even-odd crossing count for the fill) blurred
/// with text's <see cref="GlowBlur"/>, scaled by the intensity and tinted by
/// the glow colour. It is worked out on the CPU and kept as a texture per
/// shape, so redrawing an unchanged shape costs nothing.
/// </summary>
internal sealed class HostShapeGlow(ICanvasServer server)
{
    private sealed class Baked(Handle texture, Vector2 offset, Vector2 size)
    {
        public readonly Handle Texture = texture;

        /// <summary>Where the texture's corner sits from the shape's floored top-left point.</summary>
        public readonly Vector2 Offset = offset;

        public readonly Vector2 Size = size;
        public int Users;
    }

    private readonly Dictionary<string, Baked> _baked = new();

    /// <summary>The bake each canvas item draws now, so one no item uses is freed.</summary>
    private readonly Dictionary<Handle, string> _drawing = new();

    public void Draw(Handle canvas, ReadOnlySpan<Vector2> points, bool closed, bool filled, float strokeWidth, ShapeGlow glow)
    {
        if (points.Length < 2 && !(filled && points.Length >= 3))
            return;
        if (glow.Color.A <= 0f || (strokeWidth <= 0f && !(filled && closed)))
            return;

        // The bake is kept relative to the shape's floored corner, so moving a
        // shape by whole pixels finds the same texture.
        Vector2 min = points[0];
        foreach (Vector2 point in points)
            min = min.Min(point);
        Vector2 anchor = min.Floor();

        string key = Key(points, anchor, closed, filled, strokeWidth, glow);
        if (!_baked.TryGetValue(key, out Baked? baked))
        {
            baked = Bake(points, anchor, closed, filled, strokeWidth, glow);
            _baked[key] = baked;
        }

        Use(canvas, key, baked);

        Vector2 at = anchor + baked.Offset;
        Vector2 end = at + baked.Size;
        server.AddTriangleArray(
            canvas,
            [0, 1, 2, 0, 2, 3],
            [at, new Vector2(end.X, at.Y), end, new Vector2(at.X, end.Y)],
            [glow.Color],
            [new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)],
            baked.Texture);
    }

    private void Use(Handle canvas, string key, Baked baked)
    {
        if (_drawing.TryGetValue(canvas, out string? old))
        {
            if (old == key)
                return;

            Baked previous = _baked[old];
            if (--previous.Users == 0)
            {
                TextureServer.Current.Free(previous.Texture);
                _baked.Remove(old);
            }
        }

        _drawing[canvas] = key;
        baked.Users++;
    }

    private static Baked Bake(
        ReadOnlySpan<Vector2> points, Vector2 anchor, bool closed, bool filled, float strokeWidth, ShapeGlow glow)
    {
        Vector2 min = points[0], max = points[0];
        foreach (Vector2 point in points)
        {
            min = min.Min(point);
            max = max.Max(point);
        }

        float pad = GlowBlur.Padding(glow.Radius) + strokeWidth / 2;
        Vector2 origin = (min - new Vector2(pad, pad)).Floor();
        int w = Mathf.CeilToInt(max.X - origin.X + pad);
        int h = Mathf.CeilToInt(max.Y - origin.Y + pad);

        float half = strokeWidth / 2;
        bool fill = filled && closed;
        int count = points.Length;
        var mask = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // The pixel's centre, as the blit shader's UV * size + origin.
                var p = new Vector2(origin.X + x + 0.5f, origin.Y + y + 0.5f);
                float dist = float.MaxValue;
                bool inside = false;
                for (int i = 0; i < count; i++)
                {
                    int j = i + 1;
                    if (j >= count)
                    {
                        if (!closed)
                            break;
                        j = 0;
                    }

                    Vector2 a = points[i], b = points[j], ab = b - a;
                    float t = Math.Clamp((p - a).Dot(ab) / MathF.Max(ab.Dot(ab), 1e-6f), 0f, 1f);
                    dist = MathF.Min(dist, (p - a - ab * t).Length());
                    if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                        inside = !inside;
                }

                float stroke = half > 0f ? 1f - SmoothStep(half - 0.75f, half + 0.75f, dist) : 0f;
                mask[y * w + x] = MathF.Max(stroke, fill && inside ? 1f : 0f);
            }
        }

        byte[] alpha = GlowBlur.Blur(mask, w, h, glow.Radius, glow.Intensity);
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < alpha.Length; i++)
        {
            pixels[i * 4] = 255;
            pixels[i * 4 + 1] = 255;
            pixels[i * 4 + 2] = 255;
            pixels[i * 4 + 3] = alpha[i];
        }

        Handle texture = TextureServer.Current.Create(w, h, pixels, mipmaps: false);
        return new Baked(texture, origin - anchor, new Vector2(w, h));
    }

    private static float SmoothStep(float from, float to, float x)
    {
        float t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>The shape relative to its corner, to an eighth of a pixel, and what the glow looks like.</summary>
    private static string Key(
        ReadOnlySpan<Vector2> points, Vector2 anchor, bool closed, bool filled, float strokeWidth, ShapeGlow glow)
    {
        var key = new StringBuilder(points.Length * 12 + 32);
        CultureInfo inv = CultureInfo.InvariantCulture;
        key.Append(closed ? 'c' : 'o').Append(filled ? 'f' : 'e')
            .Append(strokeWidth.ToString("0.###", inv)).Append('|')
            .Append(glow.Radius.ToString("0.###", inv)).Append('|')
            .Append(glow.Intensity.ToString("0.###", inv));
        foreach (Vector2 point in points)
        {
            Vector2 p = point - anchor;
            key.Append(';').Append((int)MathF.Round(p.X * 8f)).Append(',').Append((int)MathF.Round(p.Y * 8f));
        }

        return key.ToString();
    }
}
