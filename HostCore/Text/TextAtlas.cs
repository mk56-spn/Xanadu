using System;
using System.Collections.Generic;
using MoonWorks.Graphics;

namespace Xanadu.Host.Text;

/// <summary>Where one cached glyph image lives: its page and pixel rect, and its offset from the pen.</summary>
internal readonly record struct AtlasGlyph(int Page, int X, int Y, int Width, int Height, int Left, int Top)
{
    public bool IsEmpty => Width == 0 || Height == 0;
}

/// <summary>
/// Glyph images packed on shelves into 1024² pages: white with coverage in
/// alpha, so the canvas draws them with its one pipeline, tinted by the vertex
/// colour. Pages are never evicted (retained canvas items keep their UVs), and
/// a new page opens when one fills. Changed rows upload once per frame.
/// </summary>
internal sealed class TextAtlas : IDisposable
{
    public const int Size = 1024;
    private const int Gap = 1;

    private sealed class Page(Texture texture, ulong id)
    {
        public readonly Texture Texture = texture;
        public readonly ulong Id = id;
        public readonly uint[] Pixels = new uint[Size * Size];
        public readonly List<(int Y, int Height, int X)> Shelves = new();
        public int Bottom;
        public int DirtyTop = int.MaxValue, DirtyBottom;
    }

    private readonly GraphicsDevice _device;
    private readonly Func<Texture, ulong> _register;
    private readonly List<Page> _pages = new();

    /// <param name="register">Gives a new page texture an id the canvas can draw it by.</param>
    public TextAtlas(GraphicsDevice device, Func<Texture, ulong> register)
    {
        _device = device;
        _register = register;
    }

    public int PageCount => _pages.Count;

    /// <summary>The canvas texture id of a page.</summary>
    public ulong TextureId(int page) => _pages[page].Id;

    /// <summary>Copies a coverage bitmap in and returns where it went; an empty bitmap takes no room.</summary>
    public AtlasGlyph Add(in GlyphBitmap bitmap)
    {
        if (bitmap.Width == 0 || bitmap.Height == 0)
            return new AtlasGlyph(0, 0, 0, 0, 0, bitmap.Left, bitmap.Top);

        int w = Math.Min(bitmap.Width, Size - Gap), h = Math.Min(bitmap.Height, Size - Gap);
        (int page, int x, int y) = Place(w + Gap, h + Gap);
        Page target = _pages[page];
        for (int row = 0; row < h; row++)
        {
            ReadOnlySpan<byte> source = bitmap.Coverage.AsSpan(row * bitmap.Width, w);
            Span<uint> line = target.Pixels.AsSpan((y + row) * Size + x, w);
            for (int i = 0; i < w; i++)
                line[i] = 0x00FFFFFFu | (uint)source[i] << 24;
        }

        target.DirtyTop = Math.Min(target.DirtyTop, y);
        target.DirtyBottom = Math.Max(target.DirtyBottom, y + h);
        return new AtlasGlyph(page, x, y, w, h, bitmap.Left, bitmap.Top);
    }

    private (int Page, int X, int Y) Place(int w, int h)
    {
        if (_pages.Count > 0 && TryPlace(_pages[^1], w, h, out int x, out int y))
            return (_pages.Count - 1, x, y);

        _pages.Add(NewPage());
        TryPlace(_pages[^1], w, h, out x, out y);
        return (_pages.Count - 1, x, y);
    }

    private static bool TryPlace(Page page, int w, int h, out int x, out int y)
    {
        // The first shelf tall enough (but not wastefully so) with room left.
        for (int i = 0; i < page.Shelves.Count; i++)
        {
            (int shelfY, int height, int shelfX) = page.Shelves[i];
            if (h <= height && h * 2 >= height && shelfX + w <= Size)
            {
                page.Shelves[i] = (shelfY, height, shelfX + w);
                (x, y) = (shelfX, shelfY);
                return true;
            }
        }

        if (page.Bottom + h > Size)
        {
            (x, y) = (0, 0);
            return false;
        }

        page.Shelves.Add((page.Bottom, h, w));
        (x, y) = (0, page.Bottom);
        page.Bottom += h;
        return true;
    }

    private Page NewPage()
    {
        Texture texture = Texture.Create2D(_device, "text atlas", Size, Size,
            TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);
        var page = new Page(texture, _register(texture));
        Array.Fill(page.Pixels, 0x00FFFFFFu);
        page.DirtyTop = 0;
        page.DirtyBottom = Size;
        return page;
    }

    /// <summary>Uploads every page's changed rows; call once a frame before drawing.</summary>
    public void Flush()
    {
        ResourceUploader? uploader = null;
        foreach (Page page in _pages)
        {
            if (page.DirtyTop >= page.DirtyBottom)
                continue;

            uploader ??= new ResourceUploader(_device);
            int top = page.DirtyTop, rows = page.DirtyBottom - top;
            uploader.SetTextureData(
                new TextureRegion(page.Texture) { Y = (uint)top, H = (uint)rows },
                (ReadOnlySpan<uint>)page.Pixels.AsSpan(top * Size, rows * Size),
                cycle: false);
            page.DirtyTop = int.MaxValue;
            page.DirtyBottom = 0;
        }

        if (uploader is null)
            return;

        uploader.Upload();
        uploader.Dispose();
    }

    public void Dispose()
    {
        foreach (Page page in _pages)
            page.Texture.Dispose();
        _pages.Clear();
    }
}
