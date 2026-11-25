// Library tour: shapes that pass in front of themselves (xanadu.mesh), built into the mesh
// pool. Each corner's depth sorts the triangles per pixel, so a knot's crossings, a Möbius
// strip's turn, a Klein bottle running through itself and a shell's whorls all come out right.
// Top row: torus knots, a Möbius strip and its 1.5-turn cousin. Middle: a Klein bottle, a
// seashell, three linked rings, a twisted box. Bottom: a capsule bent into a horseshoe, a
// twisted taper, a knotted ribbon, a (3, 7) knot. The twisting box and the bending capsule
// change shape, so they are rebuilt each frame; the rest are built once ([Cached]).
import xanadu;

static const uint2 CELLS = uint2(64, 32);
static const uint EACH = 32;                     // pages a 64 x 32 grid takes (mesh.pagesFor)
static const uint RING = 8;                      // pages each of the three rings' 32 x 16 grids takes
static const uint KEPT = 10;
static const uint LIVE = 2;
static const uint KEPT_SHAPES[KEPT] = { 0, 1, 2, 3, 4, 5, 6, 9, 10, 11 };
static const uint LIVE_SHAPES[LIVE] = { 7, 8 };

[Cached] [Pages(KEPT * EACH)] Meshes pool;

[Pages(LIVE * EACH)] Meshes livePool;

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.35); }
float3 slot(uint k) { return float3((float(k % 4) - 1.5) * 330, (floor(float(k) / 4) - 1) * 270, 0); }

/// Shape k's turn and place, set by the draw every frame.
Placement3 placed(uint k, float scale)
{
    Placement3 p;
    p.offset = slot(k) * scale;
    p.angles = k == 5 ? float3(0.1, time * 0.4, 0)
             : k == 6 ? float3(0.3, sin(time * 0.5) * 0.6, 0)
             : k == 7 ? float3(0.3, 0, 0)
             : k == 8 ? float3(0.2, sin(time * 0.4) * 0.5, 0)
             : k == 9 ? float3(0.2, time * 0.5, 0)
             : float3(0.25, time * 0.4, 0);
    p.scale = scale;
    return p;
}

/// Built once: these only turn.
[Pass(128, over: KEPT * EACH * 128)]
void make(uint i)
{
    MeshPage p = pool.page(i, EACH);
    uint k = KEPT_SHAPES[p.object];
    p.object = k;
    switch (k)
    {
        case 0: pool.grid(TorusKnot(80, 32, 2, 3).tube(13), p, CELLS); break;
        case 1: pool.grid(TorusKnot(80, 30, 2, 5).tube(10), p, CELLS); break;
        case 2: pool.grid(Mobius(85, 60), p, CELLS); break;
        case 3: pool.grid(Mobius(85, 50, 1.5), p, CELLS); break;
        case 4: pool.grid(Klein(80, 30), p, CELLS); break;
        case 5: pool.grid(Shell(70, 150, 4, 0.6), p, CELLS); break;
        case 6:
        {
            // Three rings, one object: three grids, each ring turned a quarter from the last.
            uint r = p.thread / (RING * 128);
            float side = float(r) - 1;
            float3 turn = r == 1 ? float3(PI / 2, 0, 0) : float3(0);
            if (r < 3)
                pool.grid(Torus(48, 11).turned(turn).moved(float3(side * 72, 0, 0)), p.thread % (RING * 128), uint2(32, 16), p.first + r * RING, k);
            break;
        }
        case 9: pool.grid(Capsule(70, 120).tapered(0.15, 1, 260).twisted(3, 260), p, CELLS); break;
        case 10: pool.grid(TorusKnot(80, 28, 2, 3).ribbon(26, 3), p, CELLS); break;
        default: pool.grid(TorusKnot(85, 22, 3, 7).tube(7), p, CELLS); break;
    }
}

/// Built every frame: a box twisting back and forth, a capsule bending into a horseshoe.
[Pass(128, over: LIVE * EACH * 128)]
void remake(uint i)
{
    MeshPage p = livePool.page(i, EACH);
    uint k = LIVE_SHAPES[p.object];
    p.object = k;
    if (k == 7)
        livePool.grid(Superquadric(70, 0.15, 0.15).twisted(sin(time) * 2, 150), p, CELLS);
    else
        livePool.grid(Capsule(24, 220).turned(float3(0, 0, PI / 2)).bent(PI * (0.4 + 0.9 * sin01(time)), 280), p, CELLS);
}

[Draw(64, 1, shade: kept, copies: KEPT * EACH)]
ItemVertex shown(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return pool.pulled(v, placed(pool.objectOf(copy), scale), camera(scale));
}

[Draw(64, 1, shade: live, copies: LIVE * EACH)]
ItemVertex moving(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return livePool.pulled(v, placed(livePool.objectOf(copy), scale), camera(scale));
}

float4 kept(ItemInput i) { return look(i, pool.objectOf(copy)); }
float4 live(ItemInput i) { return look(i, livePool.objectOf(copy)); }

float4 look(ItemInput i, uint k)
{
    colour base = colours.oklch(0.72, 0.14, float(k) / 12 + 0.05);
    // Bands along u, so you can follow each shape through its crossings.
    base.paint(Checker(1.0 / 12).filled(float2(i.uv.x, 0.01), 0.004) * 0.35, colours.white);
    return mesh.lit(i, camera(i.rest.size.y / 810), base);
}

void graph()
{
    make();
    remake();
    shown(frame);
    moving(frame);
}
