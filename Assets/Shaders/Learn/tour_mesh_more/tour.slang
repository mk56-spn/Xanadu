// Library tour: more 3D from xanadu.mesh, built into the mesh pool: tubes along a spline and a
// helix, superquadrics from cube to star, a Bézier patch, two morphs, and a 2D spline on a
// flat sheet. Shapes that only turn are built once ([Cached]); the morphs change shape every
// frame, so they are rebuilt each frame into plain pages.
import xanadu;

static const uint EACH = 16;                     // pages a 32 x 32 grid takes (mesh.pagesFor)
static const uint KEPT = 10;
static const uint LIVE = 2;
static const uint KEPT_SHAPES[KEPT] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 11 };
static const uint LIVE_SHAPES[LIVE] = { 9, 10 };

[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(KEPT * EACH)] RWStructuredBuffer<Meshlet> meshlets;

[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<MeshVertex> liveCorners;
[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<uint> liveTriangles;
[Buffer(LIVE * EACH)] RWStructuredBuffer<Meshlet> liveMeshlets;

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.3); }
float3 slot(uint k) { return float3((float(k % 4) - 1.5) * 330, (floor(float(k) / 4) - 1) * 270, 0); }

Spline3 path()
{
    Spline3 p = {};
    p.add(float3(-120, 40, 0)); p.add(float3(-60, -60, 60)); p.add(float3(0, 50, -40));
    p.add(float3(60, -50, 40)); p.add(float3(120, 30, 0));
    return p;
}

/// Shape k's turn and place, set by the draw every frame.
Placement3 placed(uint k, float scale)
{
    float3 sway = float3(0.2, sin(time * 0.5) * 0.4, 0);
    Placement3 p;
    p.offset = slot(k) * scale;
    p.angles = k == 0 || k == 2 ? sway
             : k == 8 ? float3(0.5, sin(time * 0.5) * 0.6, 0)
             : k == 9 ? float3(0.2, time * 0.3, 0)
             : k == 11 ? float3(0)
             : float3(0.3, time * 0.5, 0);
    p.scale = scale;
    return p;
}

/// Built once: these only turn.
[Pass(128, over: KEPT * EACH * 128)]
void make(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = KEPT_SHAPES[n];
    uint2 cells = uint2(32);
    switch (k)
    {
        case 0: mesh.grid(path().tube(16, 0.4), t, cells, first, k, corners, triangles, meshlets); break;
        case 1: mesh.grid(Helix(60, 200, 3).tube(12), t, cells, first, k, corners, triangles, meshlets); break;
        case 2: mesh.grid(path().ribbon(40, 1.5), t, cells, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(Helix(70, 160, 1.5).ribbon(30, 3), t, cells, first, k, corners, triangles, meshlets); break;
        case 4: mesh.grid(Superquadric(100, 0.15, 0.15), t, cells, first, k, corners, triangles, meshlets); break;
        case 5: mesh.grid(Superquadric(100, 1, 0.3), t, cells, first, k, corners, triangles, meshlets); break;
        case 6: mesh.grid(Superquadric(100, 2, 2), t, cells, first, k, corners, triangles, meshlets); break;
        case 7: mesh.grid(Superquadric(95, 3, 3), t, cells, first, k, corners, triangles, meshlets); break;
        case 8: mesh.grid(Patch(float2(240, 200)).raised(1, 1, float3(0, 0, -150)).raised(2, 2, float3(0, 0, 120)).raised(3, 0, float3(0, 0, -80)),
                    t, cells, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(Sheet(float2(260, 200)), t, cells, first, k, corners, triangles, meshlets); break;
    }
}

/// Built every frame: a sheet folding into a ball, a cylinder into a ring.
[Pass(128, over: LIVE * EACH * 128)]
void remake(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = LIVE_SHAPES[n];
    uint2 cells = uint2(32);
    if (k == 9)
        mesh.grid(Sheet(float2(260, 200)).morphed(Sphere(100), sin01(time * 1.5)), t, cells, first, k, liveCorners, liveTriangles, liveMeshlets);
    else
        mesh.grid(Cylinder(70, 180).morphed(Torus(80, 30).turned(float3(PI / 2, 0, 0)), sin01(time)), t, cells, first, k, liveCorners, liveTriangles, liveMeshlets);
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
    // The flat sheet stays 2D: a spline drawn through five points, with its points.
    if (k == 11)
    {
        float2 p = (i.uv - 0.5) * float2(260, 200);
        colour c = colours.midnightBlue.withAlpha(0.9);
        let s = Spline(4).added(float2(-110, 60)).added(float2(-50, -70)).added(float2(10, 40)).added(float2(60, -60)).added(float2(110, 20));
        c.paint(s.filled(p), colours.gold);
        for (int n = 0; n < 5; n++)
            c.paint(Circle(6).moved(s.points[n]).stroked(p, 2), colours.white);
        return c;
    }

    float scale = i.rest.size.y / 810;
    colour base = colours.oklch(0.72, 0.13, float(k) / 12);
    base.paint(Checker(1.0 / 8).filled((i.uv - 0.5) * float2(2, 1), 0.01) * 0.3, colours.white);
    return mesh.lit(i, camera(scale), base);
}

void graph()
{
    make();
    remake();
    shown(frame);
    moving(frame);
}
