// The mesh core (xanadu.mesh's pool): seven surfaces built once into [Cached] pages and drawn
// by one counted draw, a page a copy, each turned live by its placement; an ocean whose shape
// moves every frame, rebuilt each frame into plain buffers and drawn the same way. Change a
// value and the cached seven are built again; otherwise their [Pass] never runs.
import xanadu;

static const uint EACH = 16;                     // pages a 32 x 32 grid takes (mesh.pagesFor)
static const uint SHAPES = 7;
static const uint PAGES = SHAPES * EACH;
static const uint SEA = 36;                      // pages the ocean's 48 x 48 grid takes

/// Turns a second, for every shape.
[Default(0.5)] uniform float spin;
/// How lumpy the rock is: change it and the cached shapes are built again.
[Default(16)] uniform float lumps;

[Cached] [Buffer(PAGES * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(PAGES * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(PAGES)] RWStructuredBuffer<Meshlet> meshlets;

[Buffer(SEA * 128)] RWStructuredBuffer<MeshVertex> seaCorners;
[Buffer(SEA * 128)] RWStructuredBuffer<uint> seaTriangles;
[Buffer(SEA)] RWStructuredBuffer<Meshlet> seaMeshlets;

Camera camera() { return Camera(1400).orbited(0, 0.25); }

/// Where shape k sits, laid out for a quad 810 px tall.
float3 slot(uint k) { return float3((float(k % 4) - 1.5) * 330, (floor(float(k) / 4) - 0.5) * 300, 0); }

Bezier3 spine() { return Bezier3(float3(-110, 60, 0), float3(-60, -140, -80), float3(60, 140, 80), float3(110, -60, 0)); }
Bezier vase() { return Bezier(float2(40, -90), float2(130, -40), float2(10, 40), float2(70, 90)); }

/// Shape k, placed by the draw: scaled to the quad, turning.
Placement3 placed(uint k, float scale)
{
    Placement3 p;
    p.offset = slot(k) * scale;
    p.angles = float3(k == 1 ? 1.1 : 0.25, time * spin * (1 + 0.15 * k), 0);
    p.scale = scale;
    return p;
}

[Pass(128, over: PAGES * 128)]
void make(uint i)
{
    uint k = i / (EACH * 128), t = i % (EACH * 128), first = k * EACH;
    uint2 cells = uint2(32);
    switch (k)
    {
        case 0: mesh.grid(Sphere(100), t, cells, first, k, corners, triangles, meshlets); break;
        case 1: mesh.grid(Torus(85, 30), t, cells, first, k, corners, triangles, meshlets); break;
        case 2: mesh.grid(Cylinder(85, 170, 0, 1), t, cells, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(vase().lathed(), t, cells, first, k, corners, triangles, meshlets); break;
        case 4: mesh.grid(spine().tube(22, 0.3), t, cells, first, k, corners, triangles, meshlets); break;
        case 5: mesh.grid(Sphere(95).lumpy(lumps, 60, 0), t, cells, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(Klein(70, 26), t, cells, first, k, corners, triangles, meshlets); break;
    }
}

/// The ocean moves, so it is built every frame.
[Pass(128, over: SEA * 128)]
void sea(uint i)
{
    mesh.grid(Ocean(float2(280, 240), 18, 120, 0.6, 0.3), i, uint2(48), 0, 0, seaCorners, seaTriangles, seaMeshlets);
}

[Draw(64, 1, shade: look, copies: PAGES)]
ItemVertex shown(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    return mesh.pulled(v, placed(meshlets[copy].object, scale), camera(), corners, triangles, meshlets);
}

float4 look(ItemInput i)
{
    uint k = meshlets[copy].object;
    colour base = colours.oklch(0.72, 0.13, float(k) / 8);
    base.paint(Checker(1.0 / 8).filled((i.uv - 0.5) * float2(2, 1), 0.01) * 0.35, colours.white);
    return mesh.lit(i, camera(), base);
}

[Draw(64, 1, shade: water, copies: SEA)]
ItemVertex waves(ItemVertex v)
{
    float scale = v.rest.size.y / 810;
    Placement3 p;
    p.offset = slot(7) * scale + float3(0, 30, 0) * scale;
    p.angles = float3(0.45, 0, 0);
    p.scale = scale;
    return mesh.pulled(v, p, camera(), seaCorners, seaTriangles, seaMeshlets);
}

float4 water(ItemInput i) { return mesh.lit(i, camera(), colours.oklch(0.55, 0.12, 0.62), 80); }

void graph()
{
    make();
    sea();
    shown(frame);
    waves(frame);
}
