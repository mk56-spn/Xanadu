using System;
using SkiaSharp;
using Svg.Skia;

namespace Xanadu.Animation;

/// <summary>Straight-alpha RGBA8 pixels, rows top first.</summary>
public sealed record RasterImage(int Width, int Height, byte[] Pixels)
{
    /// <summary>The alpha of the pixel nearest <paramref name="x"/>, <paramref name="y"/> (0 to 1 across the image), 0 to 1.</summary>
    public float AlphaAt(float x, float y)
    {
        int px = Math.Clamp((int)(x * (Width - 1)), 0, Width - 1);
        int py = Math.Clamp((int)(y * (Height - 1)), 0, Height - 1);
        return Pixels[(py * Width + px) * 4 + 3] / 255f;
    }
}

/// <summary>
/// Rasterises SVG without the engine (Docs/host.md §5.7), through
/// Svg.Skia: the document's own size times a scale, as Godot's
/// <c>Image.LoadSvgFromString</c> sized it.
/// </summary>
public static class SvgRaster
{
    /// <summary>The SVG at <paramref name="scale"/> pixels per unit; throws on a document it can't read.</summary>
    public static RasterImage Render(string svg, float scale)
    {
        using var document = new SKSvg();
        SKPicture picture = document.FromSvg(svg) ?? throw new InvalidOperationException("Unreadable SVG.");
        SKRect bounds = picture.CullRect;
        int width = Math.Max(1, (int)MathF.Round(bounds.Width * scale));
        int height = Math.Max(1, (int)MathF.Round(bounds.Height * scale));

        // Skia draws premultiplied; the pixels go out straight, as the canvas expects.
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
        }

        byte[] pixels = bitmap.Bytes;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            if (alpha is 0 or 255)
                continue;
            for (int c = 0; c < 3; c++)
                pixels[i + c] = (byte)Math.Min(255, (pixels[i + c] * 255 + alpha / 2) / alpha);
        }

        return new RasterImage(width, height, pixels);
    }
}
