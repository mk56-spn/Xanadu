// xanadu.pattern: lines, checkers and tiles in pixel units, all under
// `pattern.`. Feed them ItemInput.centred or .local.
//
//     pattern.columns(p, 2.0, 24.0)   pattern.checker(p, 16.0)   pattern.tile(p, 64.0)
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
    /// Vertical lines `width` thick, `spacing` apart.
    public float columns(float2 p, float width, float spacing)
    {
        float x = mod(p.x, spacing);
        float aa = fwidth(x);
        return smoothstep(width + aa, width - aa, abs(x - spacing * 0.5));
    }

    /// Horizontal lines `width` thick, `spacing` apart.
    public float rows(float2 p, float width, float spacing)
    {
        float y = mod(p.y, spacing);
        float aa = fwidth(y);
        return smoothstep(width + aa, width - aa, abs(y - spacing * 0.5));
    }

    /// 0 or 1 in squares `size` across.
    public float checker(float2 p, float size)
    {
        float2 cell = floor(p / size);
        return mod(cell.x + cell.y, 2.0);
    }

    /// A checker whose squares split again, `levels` times (0 to 3).
    public float checkerSplit(float2 p, float size, int levels)
    {
        float2 q = p / size;
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
    public Tile tile(float2 p, float size)
    {
        Tile t;
        t.id = round(p / size);
        t.local = p - size * t.id;
        return t;
    }
}
