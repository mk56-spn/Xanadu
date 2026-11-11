using System;
using System.Collections.Generic;

namespace Xanadu.Animation;

/// <summary>
/// Gives every fully transparent pixel of straight-alpha art the colour of the
/// nearest visible pixel, keeping its alpha at zero.
/// </summary>
/// <remarks>
/// Rasterised SVGs leave transparent pixels black. Linear filtering and mipmaps
/// average colour without weighting it by alpha, so that black bleeds into the
/// edges and the art gets a dark rim, worse the smaller it is drawn. Godot's
/// import fixes this with "fix alpha border", which only reaches a few pixels;
/// art drawn at a fraction of its raster size reads deep mip levels, so this
/// fills the whole transparent area.
/// </remarks>
public static class ArtAlphaBleed
{
    /// <summary>Bleeds colour into the transparent pixels of tightly packed RGBA8 data, in place.</summary>
    public static void Apply(Span<byte> rgba, int width, int height)
    {
        int count = width * height;
        if (width <= 0 || height <= 0 || rgba.Length < count * 4)
            throw new ArgumentException("The data does not hold width × height RGBA8 pixels.");

        // Breadth first from every visible pixel at once, so each transparent
        // pixel takes the colour of the nearest one.
        var filled = new bool[count];
        var queue = new Queue<int>();
        for (int i = 0; i < count; i++)
        {
            if (rgba[i * 4 + 3] == 0)
                continue;

            filled[i] = true;
            queue.Enqueue(i);
        }

        if (queue.Count == 0)
            return;

        while (queue.Count > 0)
        {
            int from = queue.Dequeue();
            int x = from % width;
            int y = from / width;
            if (x > 0) Fill(rgba, filled, queue, from, from - 1);
            if (x < width - 1) Fill(rgba, filled, queue, from, from + 1);
            if (y > 0) Fill(rgba, filled, queue, from, from - width);
            if (y < height - 1) Fill(rgba, filled, queue, from, from + width);
        }
    }

    private static void Fill(Span<byte> rgba, bool[] filled, Queue<int> queue, int from, int to)
    {
        if (filled[to])
            return;

        filled[to] = true;
        rgba[to * 4] = rgba[from * 4];
        rgba[to * 4 + 1] = rgba[from * 4 + 1];
        rgba[to * 4 + 2] = rgba[from * 4 + 2];
        queue.Enqueue(to);
    }
}
