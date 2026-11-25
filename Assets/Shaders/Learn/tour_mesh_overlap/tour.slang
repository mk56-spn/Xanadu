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

[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(KEPT * EACH)] RWStructuredBuffer<Meshlet> meshlets;

[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<MeshVertex> liveCorners;
[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<uint> liveTriangles;
[Buffer(LIVE * EACH)] RWStructuredBuffer<Meshlet> liveMeshlets;

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
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = KEPT_SHAPES[n];
    switch (k)
    {
        case 0: mesh.grid(TorusKnot(80, 32, 2, 3).tube(13), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 1: mesh.grid(TorusKnot(80, 30, 2, 5).tube(10), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 2: mesh.grid(Mobius(85, 60), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(Mobius(85, 50, 1.5), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 4: mesh.grid(Klein(80, 30), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 5: mesh.grid(Shell(70, 150, 4, 0.6), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 6:
        {
            // Three rings, one object: three grids, each ring turned a quarter from the last.
            uint r = t / (RING * 128);
            float side = float(r) - 1;
            float3 turn = r == 1 ? float3(PI / 2, 0, 0) : float3(0);
            if (r < 3)
                mesh.grid(Torus(48, 11).turned(turn).moved(float3(side * 72, 0, 0)), t % (RING * 128), uint2(32, 16), first + r * RING, k, corners, triangles, meshlets);
            break;
        }
        case 9: mesh.grid(Capsule(70, 120).tapered(0.15, 1, 260).twisted(3, 260), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 10: mesh.grid(TorusKnot(80, 28, 2, 3).ribbon(26, 3), t, CELLS, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(TorusKnot(85, 22, 3, 7).tube(7), t, CELLS, first, k, corners, triangles, meshlets); break;
    }
}

/// Built every frame: a box twisting back and forth, a capsule bending into a horseshoe.
[Pass(128, over: LIVE * EACH * 128)]
void remake(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = LIVE_SHAPES[n];
    if (k == 7)
        mesh.grid(Superquadric(70, 0.15, 0.15).twisted(sin(time) * 2, 150), t, CELLS, first, k, liveCorners, liveTriangles, liveMeshlets);
    else
        mesh.grid(Capsule(24, 220).turned(float3(0, 0, PI / 2)).bent(PI * (0.4 + 0.9 * sin01(time)), 280), t, CELLS, first, k, liveCorners, liveTriangles, liveMeshlets);
}

[Draw(64, 1, shade: kept, copies: KEPT * EACH)]
ItemVertex shown(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return mesh.pulled(v, placed(meshlets[copy].object, scale), camera(scale), corners, triangles, meshlets);
}

[Draw(64, 1, shade: live, copies: LIVE * EACH)]
ItemVertex moving(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return mesh.pulled(v, placed(liveMeshlets[copy].object, scale), camera(scale), liveCorners, liveTriangles, liveMeshlets);
}

float4 kept(ItemInput i) { return look(i, meshlets[copy].object); }
float4 live(ItemInput i) { return look(i, liveMeshlets[copy].object); }

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
