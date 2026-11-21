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
    /// The point in its tile, from the tile's middle.
    public float2 local;
    /// Which tile: whole numbers, (0, 0) for the one the origin sits in.
    public float2 id;
    /// Where the tile's middle is, from the origin: length(tile.centre) is how far out the tile is.
    public float2 centre;
};

/// Where a point falls when space is folded into slices round a point (pattern.slice).
public struct Slice
{
    /// The point turned into the first slice: one thing drawn here repeats in every slice.
    public float2 local;
    /// Which slice, 0 to count - 1, clockwise (y down) from the first.
    public float index;
    /// How far this slice is turned from the first, in radians: rotate(v, slice.turn) takes
    /// something from the first slice's space (a direction, a normal) back out.
    public float turn;
};

/// Where a point falls among scattered points, each owning the space nearest it (Voronoi
/// cells, Worley's cellular noise; pattern.cells). Distances are in the units of the position.
public struct Cell
{
    /// The point from its cell's own scattered point: length(cell.local) is cell.nearest.
    public float2 local;
    /// Which cell: whole numbers, the tile its point was scattered in, so random(cell.id) seeds
    /// anything per cell (a colour, a size).
    public float2 id;
    /// Where the cell's point is, from the origin.
    public float2 centre;
    /// How far the nearest point is (Worley's F1): 0 on a point.
    public float nearest;
    /// How far the second nearest point is (F2): second - nearest is 0 on a border.
    public float second;
    /// How far the cell's border is, measured square to it (Quilez's exact edge distance):
    /// 0 on the border, so sdf.stroked(cell.edge, 2) draws the walls with an even width.
    public float edge;
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
/// @cost 2
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
/// @cost 1
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
/// @cost 1
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
/// @cost 1
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
/// @cost 2
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

/// The walls between Voronoi cells about `size` across (pattern.cells): lines `width` thick of
/// even width, the points scattered by `jitter` (0 a square grid, 1 fully) and moving round
/// their homes as `phase` grows (a whole turn every TAU). A shape.
///
///     c.paint(Cells(60, 2), colours.white);                          // cracked tiles
///     c.paint(Cells(80, 3, 1, time).filled(), colours.cyan);           // cells that wobble
/// @cost 6
public struct Cells : IShape
{
    public Placement placement = Placement();
    public float size;
    public float width;
    public float jitter;
    public float phase;

    public __init(float size, float width, float jitter = 1.0, float phase = 0.0)
    {
        this.size = size; this.width = width; this.jitter = jitter; this.phase = phase;
    }

    public float local(float2 p) { return pattern.cells(p, size, jitter, phase).edge - width * 0.5; }
}

/// Hexagon walls: a honeycomb of hexagons `size` across (flat side to flat side), pointing up,
/// lines `width` thick. A shape; pattern.hex gives each cell's id and middle.
///
///     c.paint(Hexagons(60, 2), colours.gold);
/// @cost 3
public struct Hexagons : IShape
{
    public Placement placement = Placement();
    public float size;
    public float width;

    public __init(float size, float width) { this.size = size; this.width = width; }

    public float local(float2 p)
    {
        Tile t = pattern.hex(p, size);
        return size * 0.5 - pattern.hexDistance(t.local) - width * 0.5;
    }
}

/// Truchet tiles: each tile `size` across holds two quarter circles joining the middles of its
/// sides, turned one way or the other at random, so they link into endless winding paths.
/// Lines `width` thick. `seed` picks another layout. A shape.
///
///     c.paint(Truchet(40, 4), colours.white);
/// @cost 3
public struct Truchet : IShape
{
    public Placement placement = Placement();
    public float size;
    public float width;
    public float seed = 0.0;

    public __init(float size, float width, float seed = 0.0) { this.size = size; this.width = width; this.seed = seed; }

    public float local(float2 p)
    {
        Tile t = pattern.tile(p, size);
        float2 q = t.local / size;
        if (random(t.id + seed * 17.0) < 0.5)
            q.x = -q.x;
        float a = abs(length(q - 0.5) - 0.5);
        float b = abs(length(q + 0.5) - 0.5);
        return min(a, b) * size - width * 0.5;
    }
}

/// Round dots `radius` across their middle, one every `spacing`, every other row shifted half a
/// step when `staggered` (a halftone screen). A shape.
///
///     c.paint(Dots(20, 4), colours.white);
/// @cost 2
public struct Dots : IShape
{
    public Placement placement = Placement();
    public float spacing;
    public float radius;
    public bool staggered;

    public __init(float spacing, float radius, bool staggered = false) { this.spacing = spacing; this.radius = radius; this.staggered = staggered; }

    public float local(float2 p)
    {
        if (staggered)
            p.x += spacing * 0.5 * mod(round(p.y / spacing), 2.0);
        Tile t = pattern.tile(p, spacing);
        return length(t.local) - radius;
    }
}

/// Wavy lines across: each a sine `wavelength` long and `amplitude` high (crest to middle),
/// `width` thick, one every `spacing` (0 for a single line through the centre). A shape.
///
///     c.paint(Waves(120, 12, 2, 30).moved(float2(time * 40, 0)), colours.cyan);
/// @cost 2
public struct Waves : IShape
{
    public Placement placement = Placement();
    public float wavelength;
    public float amplitude;
    public float width;
    public float spacing;

    public __init(float wavelength, float amplitude, float width, float spacing = 0.0)
    {
        this.wavelength = wavelength; this.amplitude = amplitude; this.width = width; this.spacing = spacing;
    }

    public float local(float2 p)
    {
        float k = TAU / max(wavelength, 1e-3);
        float y = p.y - amplitude * sin(p.x * k);
        if (spacing > 0.0)
            y -= spacing * round(y / spacing);
        // Divided by the slope, so the line keeps its width on the steep parts.
        float slope = amplitude * k * cos(p.x * k);
        return abs(y) / sqrt(1.0 + slope * slope) - width * 0.5;
    }
}

/// Zigzag lines across: each zag `wavelength` long and `amplitude` high, `width` thick, one every
/// `spacing` (0 for a single line). A shape.
/// @cost 2
public struct Zigzag : IShape
{
    public Placement placement = Placement();
    public float wavelength;
    public float amplitude;
    public float width;
    public float spacing;

    public __init(float wavelength, float amplitude, float width, float spacing = 0.0)
    {
        this.wavelength = wavelength; this.amplitude = amplitude; this.width = width; this.spacing = spacing;
    }

    public float local(float2 p)
    {
        float y = p.y - amplitude * (2.0 * triangle(p.x + wavelength * 0.25, wavelength) - 1.0);
        if (spacing > 0.0)
            y -= spacing * round(y / spacing);
        float slope = 4.0 * amplitude / max(wavelength, 1e-3);
        return abs(y) / sqrt(1.0 + slope * slope) - width * 0.5;
    }
}

/// A brick wall's mortar: bricks `size` wide and high, every other row shifted `offset` of a
/// brick (0.5 a stretcher bond), joints `width` thick. A shape; pattern.bricks gives each brick's id.
///
///     c.paint(Bricks(float2(60, 24), 3), colours.lightGray);
/// @cost 2
public struct Bricks : IShape
{
    public Placement placement = Placement();
    public float2 size;
    public float width;
    public float offset;

    public __init(float2 size, float width, float offset = 0.5) { this.size = size; this.width = width; this.offset = offset; }

    public float local(float2 p)
    {
        Tile t = pattern.bricks(p, size, offset);
        float2 edge = size * 0.5 - abs(t.local);
        return min(edge.x, edge.y) - width * 0.5;
    }
}

public namespace pattern
{
    /// Space cut into tiles `size` across, from the quad's centre: draw one
    /// shape at tile.local and it repeats in every tile; seed randomness with tile.id.
    public Tile tile(float size) { return tile(shading.canvas.centred, size); }

    /// Space cut into tiles `size` across, at `position`: for tiles that move, scale or layer.
    public Tile tile(float2 position, float size) { return tile(position, float2(size)); }

    /// Oblong tiles `size` wide and high, from the quad's centre.
    public Tile tile(float2 size) { return tile(shading.canvas.centred, size); }

    /// Oblong tiles `size` wide and high, at `position`.
    public Tile tile(float2 position, float2 size)
    {
        Tile t;
        t.id = round(position / size);
        t.centre = size * t.id;
        t.local = position - t.centre;
        return t;
    }

    /// Oblong tiles `size` wide and high with `place` of a tile (anchor.topLeft, …) on the origin, at `position`.
    public Tile tile(float2 position, float2 size, float2 place)
    {
        Tile t = tile(position - place * size, size);
        t.centre += place * size;
        return t;
    }

    /// Tiles `size` across with `place` of a tile on the quad's centre (anchor.topLeft: a corner
    /// there, as GLSL's fract(uv * n) cuts them); .local is still from the tile's own centre.
    public Tile tile(float size, float2 place) { return tile(shading.canvas.centred, size, place); }

    /// Tiles `size` across with `place` of a tile (anchor.topLeft, …) on the origin, at `position`.
    public Tile tile(float2 position, float size, float2 place) { return tile(position, float2(size), place); }

    /// Space folded into `count` equal slices round the quad's centre, like a kaleidoscope: draw
    /// one thing at slice.local and it repeats in every slice. The first slice is centred on
    /// `facing` (straight up by default); slice.index counts them clockwise (y down).
    public Slice slice(float count, float2 facing = float2(0.0, -1.0)) { return slice(shading.canvas.centred, count, facing); }

    /// Space folded into `count` slices round the origin, at `position` (see slice(count)).
    public Slice slice(float2 position, float count, float2 facing = float2(0.0, -1.0))
    {
        float2 f = normalize(facing);
        float split = TAU / count;
        // The angle from facing to the point, then which slice that falls in, the first centred on facing.
        float angle = atan2(f.x * position.y - f.y * position.x, dot(f, position));
        float index = floor(angle / split + 0.5);
        Slice s;
        s.turn = index * split;
        s.index = mod(index, count);
        s.local = rotate(position, -s.turn);
        return s;
    }
    /// Space cut into hexagons `size` across (flat side to flat side), pointing up, from the quad's
    /// centre: tile.local is from the hexagon's middle, tile.id names it (whole numbers), tile.centre is its middle.
    public Tile hex(float size) { return hex(shading.canvas.centred, size); }

    /// Hexagons `size` across at `position` (see hex(size)).
    public Tile hex(float2 position, float size)
    {
        // Two offset rectangular lattices; the nearer centre wins (Shane's hexagonal tiling).
        const float2 s = float2(1.0, 1.7320508);
        float2 p = position / size;
        float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
        float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
        bool first = dot(h.xy, h.xy) < dot(h.zw, h.zw);
        Tile t;
        t.local = (first ? h.xy : h.zw) * size;
        float2 id = first ? hc.xy : hc.zw + 0.5;
        t.id = id * 2.0;
        t.centre = id * s * size;
        return t;
    }

    /// How far a point is from a pointed-up hexagon's middle, measured as hexagons are: equal all
    /// round a hexagon (size / 2 on the edge of one `size` across). For hex tiles' rings and edges.
    public float hexDistance(float2 local)
    {
        float2 p = abs(local);
        return max(dot(p, float2(0.5, 0.8660254)), p.x);
    }

    /// Space cut into bricks `size` wide and high from the quad's centre, every other row
    /// shifted `offset` of a brick.
    public Tile bricks(float2 size, float offset = 0.5) { return bricks(shading.canvas.centred, size, offset); }

    /// Bricks `size` wide and high at `position`, every other row shifted `offset` of a brick.
    public Tile bricks(float2 position, float2 size, float offset = 0.5)
    {
        float row = round(position.y / size.y);
        float shift = mod(row, 2.0) * offset * size.x;
        Tile t = tile(position - float2(shift, 0.0), size);
        t.centre.x += shift;
        return t;
    }

    /// Space cut into equilateral triangles with sides `size` long, from the quad's centre:
    /// tile.local is from the triangle's middle; tile.id.x is even for one pointing down, odd for
    /// one pointing up, and tile.id.y is its row.
    public Tile triangles(float size) { return triangles(shading.canvas.centred, size); }

    /// Triangles with sides `size` long at `position` (see triangles(size)).
    public Tile triangles(float2 position, float size)
    {
        // On the lattice (size, 0) and (size / 2, h): each cell splits on its diagonal into a
        // triangle pointing down (a + b < 1) and one pointing up.
        float h = size * 0.8660254;
        float b = position.y / h;
        float a = position.x / size - b * 0.5;
        float2 cell = floor(float2(a, b));
        float2 f = float2(a, b) - cell;
        bool up = f.x + f.y >= 1.0;
        float2 mid = cell + (up ? float2(2.0 / 3.0) : float2(1.0 / 3.0));
        Tile t;
        t.centre = float2(mid.x * size + mid.y * size * 0.5, mid.y * h);
        t.local = position - t.centre;
        t.id = float2(cell.x * 2.0 + (up ? 1.0 : 0.0), cell.y);
        return t;
    }

    /// Space split among points scattered one per tile `size` across, each owning what is nearest
    /// it (Voronoi; Worley's cellular noise), from the quad's centre. `jitter` scatters them (0
    /// leaves them on a square grid, 1 anywhere in their tile); `phase` moves each round its home
    /// (a whole turn every TAU: pass time for cells that wobble).
    public Cell cells(float size, float jitter = 1.0, float phase = 0.0) { return cells(shading.canvas.centred, size, jitter, phase); }

    /// Voronoi cells about `size` across at `position` (see cells(size)).
    /// @cost 6
    public Cell cells(float2 position, float size, float jitter = 1.0, float phase = 0.0)
    {
        float2 x = position / size;
        float2 n = floor(x), f = x - n;

        // The nearest point and the second nearest, over the tile and its eight neighbours.
        float2 nearestTile = float2(0.0), nearestTo = float2(0.0);
        float best = 8.0, next = 8.0;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                float2 g = float2(i, j);
                float2 to = g + scattered(n + g, jitter, phase) - f;
                float d = dot(to, to);
                if (d < best) { next = best; best = d; nearestTo = to; nearestTile = g; }
                else if (d < next) next = d;
            }

        // The border: the nearest of the bisectors between the nearest point and its neighbours
        // (Quilez, "Voronoi edges"); two tiles out so no wall is missed.
        float edge = 8.0;
        for (int j = -2; j <= 2; j++)
            for (int i = -2; i <= 2; i++)
            {
                float2 g = nearestTile + float2(i, j);
                float2 to = g + scattered(n + g, jitter, phase) - f;
                float2 between = to - nearestTo;
                if (dot(between, between) > 1e-5)
                    edge = min(edge, dot(0.5 * (nearestTo + to), normalize(between)));
            }

        Cell c;
        c.id = n + nearestTile;
        c.centre = (x + nearestTo) * size;
        c.local = -nearestTo * size;
        c.nearest = sqrt(best) * size;
        c.second = sqrt(next) * size;
        c.edge = edge * size;
        return c;
    }

    /// Where tile `id`'s point sits in it, 0..1: its home in the middle, scattered by `jitter`
    /// and moved round by `phase`.
    /// @hidden
    public float2 scattered(float2 id, float jitter, float phase)
    {
        float2 r = random2(id);
        return 0.5 + 0.5 * jitter * sin(phase + TAU * r);
    }
}
