// xanadu.pattern: lines, checkers and tiles in pixel units, all under
// `pattern.`. Feed them canvas.centred (or ItemInput's local).
//
//     pattern.grid(p, 1.0, 40.0)   pattern.columns(p, 2.0, 24.0)   pattern.checker(p, 16.0)   pattern.tile(p, 64.0)
module pattern;
import xanadu.math;

/// Where a point falls when space is cut into tiles: its place in the tile (centred) and which tile.
public struct Tile
{
    public float2 local;
    public float2 id;
};

public namespace pattern
{
    /// Grid lines `width` pixels thick, `spacing` apart, with a line through
    /// the origin (the quad's centre, for canvas.centred): 1 on a line, 0 off it.
    public float grid(float2 position, float width, float2 spacing)
    {
        float2 d = abs(position - spacing * round(position / spacing));
        float2 aa = fwidth(position);
        float2 on = smoothstep(width * 0.5 + aa * 0.5, width * 0.5 - aa * 0.5, d);
        return max(on.x, on.y);
    }

    /// Square grid cells.
    public float grid(float2 position, float width, float spacing)
    {
        return grid(position, width, float2(spacing));
    }

    /// Vertical lines `width` thick, `spacing` apart.
    public float columns(float2 position, float width, float spacing)
    {
        float x = mod(position.x, spacing);
        float aa = fwidth(x);
        return smoothstep(width + aa, width - aa, abs(x - spacing * 0.5));
    }

    /// Horizontal lines `width` thick, `spacing` apart.
    public float rows(float2 position, float width, float spacing)
    {
        float y = mod(position.y, spacing);
        float aa = fwidth(y);
        return smoothstep(width + aa, width - aa, abs(y - spacing * 0.5));
    }

    /// 0 or 1 in squares `size` across.
    public float checker(float2 position, float size)
    {
        float2 cell = floor(position / size);
        return mod(cell.x + cell.y, 2.0);
    }

    /// A checker whose squares split again, `levels` times (0 to 3).
    public float checkerSplit(float2 position, float size, int levels)
    {
        float2 q = position / size;
        float2 inside = frac(q);
        float2 id = floor(q);
        float parity = mod(id.x + id.y, 2.0);
        for (int l = 0; l < min(levels, 3); l++)
        {
            inside *= 2.0;
            float2 quadrant = floor(inside);
            parity = mod(parity + quadrant.x + quadrant.y, 2.0);
            inside = frac(inside);
        }
        return parity;
    }

    /// Space cut into tiles `size` across: draw one shape at tile.local and it repeats.
    public Tile tile(float2 position, float size)
    {
        Tile t;
        t.id = round(position / size);
        t.local = position - size * t.id;
        return t;
    }
}
