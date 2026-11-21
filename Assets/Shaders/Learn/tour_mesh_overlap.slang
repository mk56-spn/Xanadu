// Library tour: shapes that pass in front of themselves (xanadu.mesh). Each corner's depth
// (set by place) sorts the triangles per pixel, so a knot's crossings, a Möbius strip's turn,
// a Klein bottle running through itself and a shell's whorls all come out right with no
// facing() trick. Top row: torus knots, a Möbius strip and its 1.5-turn cousin. Middle: a
// Klein bottle, a seashell, three linked rings, a twisted box. Bottom: a capsule bent into a
// horseshoe, a twisted taper, a knotted ribbon, a (3, 7) knot.
import xanadu;

static const int2 COUNT = int2(4, 3);
static const float2 CELLS = float2(192, 108);        // the [Grid] below
static const float2 PIECE = float2(48, 36);          // one piece's share of it

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.35); }
float3 slot(int k)
{
    int row = k / 4;
    return float3((float(k % 4) - 1.5) * 330, float(row - 1) * 270, 0);
}

/// Piece k's shape at uv, before the scale.
float3 shaped(int k, float2 uv)
{
    float3 spin = float3(0.25, time * 0.4, 0);
    float3 at = slot(k);
    switch (k)
    {
        case 0: return TorusKnot(80, 32, 2, 3).tube(13).turned(spin).moved(at).at(uv);
        case 1: return TorusKnot(80, 30, 2, 5).tube(10).turned(spin).moved(at).at(uv);
        case 2: return Mobius(85, 60).turned(spin).moved(at).at(uv);
        case 3: return Mobius(85, 50, 1.5).turned(spin).moved(at).at(uv);
        case 4: return Klein(80, 30).turned(spin).moved(at).at(uv);
        case 5: return Shell(70, 150, 4, 0.6).turned(float3(0.1, time * 0.4, 0)).moved(at).at(uv);
        case 6:
        {
            // Three rings from one piece: it is split again, each ring turned a quarter from the last.
            Piece ring = mesh.split(uv, int2(3, 1), PIECE);
            float side = float(ring.index) - 1;
            float3 turn = ring.index == 1 ? float3(PI / 2, 0, 0) : float3(0);
            return Torus(48, 11).turned(turn).moved(float3(side * 72, 0, 0)).turned(float3(0.3, sin(time * 0.5) * 0.6, 0)).moved(at).at(ring.uv);
        }
        case 7: return Superquadric(70, 0.15, 0.15).twisted(sin(time) * 2, 150).turned(float3(0.3, 0, 0)).moved(at).at(uv);
        case 8: return Capsule(24, 220).turned(float3(0, 0, PI / 2)).bent(PI * (0.4 + 0.9 * sin01(time)), 280).turned(float3(0.2, sin(time * 0.4) * 0.5, 0)).moved(at).at(uv);
        case 9: return Capsule(70, 120).tapered(0.15, 1, 260).twisted(3, 260).turned(float3(0.2, time * 0.5, 0)).moved(at).at(uv);
        case 10: return TorusKnot(80, 28, 2, 3).ribbon(26, 3).turned(spin).moved(at).at(uv);
        default: return TorusKnot(85, 22, 3, 7).tube(7).turned(spin).moved(at).at(uv);
    }
}

/// Every piece as a surface of its own: implementing ISurface gives normalAt and lit for free.
struct Shown : ISurface
{
    Placement3 placement3 = Placement3();
    int k;
    float scale;
    float3 local(float2 uv) { return shaped(k, uv) * scale; }
}

[Grid(192, 108)]
ItemVertex deform(ItemVertex v)
{
    Piece piece = mesh.split(v.uv, COUNT, CELLS);
    float scale = v.rest.size.y / 810;
    v.place(shaped(piece.index, piece.uv) * scale, camera(scale));
    return v;
}

float4 shade(ItemInput i)
{
    Piece piece = mesh.split(i.uv, COUNT, CELLS);
    int k = piece.index;
    if (piece.seam || k == 6 && mesh.split(piece.uv, int2(3, 1), PIECE).seam)
        return colours.transparent;

    float2 uv = piece.uv;
    colour base = colours.oklch(0.72, 0.14, float(k) / 12 + 0.05);
    // Bands along u, so you can follow each shape through its crossings.
    base.paint(Checker(1.0 / 12).filled(float2(uv.x, 0.01), 0.004) * 0.35, colours.white);

    Shown shown;
    shown.k = k;
    shown.scale = i.rest.size.y / 810;
    return shown.lit(uv, camera(shown.scale), base);
}
