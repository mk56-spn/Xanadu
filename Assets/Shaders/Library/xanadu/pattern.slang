// xanadu.pattern: grids, lines, stripes and checkers as shapes, in pixels. Like any
// shape they sit on the quad's centre until moved, and draw with filled,
// stroked or c.paint:
//
//     c.paint(Grid(1, 40), colours.white);                 // 1px lines every 40px, one through the centre
//     c.paint(Checker(32).rotated(time * 0.2), colours.gold);
//     c.paint(Stripes(24), colours.gold);                   // diagonal bands, half and half
//     Tile cell = pattern.tile(90);                         // repeat one shape in tiles
module pattern;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;

/// Where a point falls when space is cut into tiles: its place in the tile (centred) and which tile.
public struct Tile
{
    public float2 local;
    public float2 id;
};

/// How far p is from the nearest multiple of `spacing`, on each axis.
float2 offGrid(float2 p, float2 spacing) { return abs(p - spacing * round(p / spacing)); }

/// Graph paper: lines across and down `width` thick and `spacing` apart, one
/// of each through the centre. `spacing` can be a float2 for oblong cells.
/// It is a shape: move, rotate or scale it like one, and draw it with filled
/// or c.paint.
///
///     c.paint(Grid(1, 40), colours.white);                           // 1px white lines every 40px
///     c.paint(Grid(2, float2(80, 40)).moved(float2(20, 0)), colours.gold);
///     c.paint(Grid(1, 40).moved(float2(time * 20)));                 // scrolls diagonally
public struct Grid : IShape
{
    public Placement placement = Placement();
    public float width;
    public float2 spacing;

    public __init(float width, float2 spacing) { this.width = width; this.spacing = spacing; }
    public __init(float width, float spacing) { this.width = width; this.spacing = float2(spacing); }

    public float local(float2 p)
    {
        float2 d = offGrid(p, spacing);
        return min(d.x, d.y) - width * 0.5;
    }
}

/// Upright lines `width` thick and `spacing` apart, one through the centre. A shape.
///
///     c.paint(Columns(2, 24), colours.white);
public struct Columns : IShape
{
    public Placement placement = Placement();
    public float width;
    public float spacing;

    public __init(float width, float spacing) { this.width = width; this.spacing = spacing; }

    public float local(float2 p) { return offGrid(p, float2(spacing)).x - width * 0.5; }
}

/// Level lines `width` thick and `spacing` apart, one through the centre. A shape.
///
///     c.paint(Rows(2, 24), colours.white);
public struct Rows : IShape
{
    public Placement placement = Placement();
    public float width;
    public float spacing;

    public __init(float width, float spacing) { this.width = width; this.spacing = spacing; }

    public float local(float2 p) { return offGrid(p, float2(spacing)).y - width * 0.5; }
}

/// Diagonal bands repeating every `size` px across them, each band `fill` of that
/// (0.5: band and gap alike), one band through the centre. rotated turns them
/// further (-0.785 for upright, 0.785 for level). A shape.
///
///     c.paint(Stripes(24), colours.gold);                              // even stripes
///     c.paint(Stripes(40, 0.25).moved(float2(time * 30, 0)), colours.white);  // thin, sliding
public struct Stripes : IShape
{
    public Placement placement = Placement();
    public float size;
    public float fill;

    public __init(float size, float fill = 0.5) { this.size = size; this.fill = fill; }

    public float local(float2 p)
    {
        // Distance across the bands: along the diagonal.
        float across = (p.x + p.y) * 0.70710678;
        return abs(across - size * round(across / size)) - size * saturate(fill) * 0.5;
    }
}

/// A checkerboard of squares `size` across, with corners meeting at the
/// centre; filled is 1 on every other square. With `levels` (0 to 3) each
/// square splits into a smaller checker again. A shape.
///
///     c.paint(Checker(32), colours.white.withAlpha(0.1));
///     c.paint(Checker(64, 2).rotated(0.3), colours.gold);
public struct Checker : IShape
{
    public Placement placement = Placement();
    public float size;
    public int levels;

    public __init(float size, int levels = 0) { this.size = size; this.levels = levels; }

    public float local(float2 p)
    {
        float2 q = p / size;
        float2 inside = frac(q);
        float2 id = floor(q);
        float parity = mod(id.x + id.y, 2.0);
        float cell = size;
        for (int l = 0; l < min(levels, 3); l++)
        {
            inside *= 2.0;
            float2 quadrant = floor(inside);
            parity = mod(parity + quadrant.x + quadrant.y, 2.0);
            inside = frac(inside);
            cell *= 0.5;
        }

        // Distance to the nearest edge of the smallest square, inside on the odd ones.
        float2 edge = min(inside, 1.0 - inside) * cell;
        float d = min(edge.x, edge.y);
        return parity > 0.5 ? -d : d;
    }
}

public namespace pattern
{
    /// Space cut into tiles `size` across, from the quad's centre: draw one
    /// shape at tile.local and it repeats in every tile; seed randomness with tile.id.
    public Tile tile(float size) { return tile(shading.canvas.centred, size); }

    /// Space cut into tiles `size` across, at `position`: for tiles that move, scale or layer.
    public Tile tile(float2 position, float size)
    {
        Tile t;
        t.id = round(position / size);
        t.local = position - size * t.id;
        return t;
    }
}
