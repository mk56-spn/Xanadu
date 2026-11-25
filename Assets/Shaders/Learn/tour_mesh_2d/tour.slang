// Library tour: 2D things made 3D (xanadu.mesh), built into the mesh pool: library shapes
// raised as reliefs, a spline spun into a goblet, a spline stood up as a wall. Shapes that
// only turn are built once ([Cached]); the turning star, the ticking digits and the swinging
// arrow change shape, so they are rebuilt each frame into plain pages.
import xanadu;

static const uint2 CELLS = uint2(48, 32);
static const uint EACH = 24;                     // pages a 48 x 32 grid takes (mesh.pagesFor)
static const uint KEPT = 6;
static const uint LIVE = 3;
static const uint KEPT_SHAPES[KEPT] = { 1, 3, 4, 5, 6, 7 };
static const uint LIVE_SHAPES[LIVE] = { 0, 2, 8 };

[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(KEPT * EACH)] RWStructuredBuffer<Meshlet> meshlets;

[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<MeshVertex> liveCorners;
[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<uint> liveTriangles;
[Buffer(LIVE * EACH)] RWStructuredBuffer<Meshlet> liveMeshlets;

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.35); }
float3 slot(uint k) { return float3((float(k % 3) - 1) * 420, (floor(float(k) / 3) - 1) * 270, 0); }

Spline goblet()
{
    // x out from the axis, y down: rim, bowl, stem, foot.
    return Spline().added(float2(70, -100)).added(float2(60, -40)).added(float2(12, 10)).added(float2(10, 60)).added(float2(60, 100));
}

Spline wall() { return Spline().added(float2(-140, 30)).added(float2(-60, -40)).added(float2(20, 40)).added(float2(140, -20)); }

/// Shape k's turn and place, set by the draw every frame.
Placement3 placed(uint k, float scale)
{
    Placement3 p;
    p.offset = slot(k) * scale;
    p.angles = k == 3 ? float3(0.25, time * 0.5, 0)
             : k == 4 ? float3(0.6, time * 0.4, 0)
             : float3(0.6, sin(time * 0.6) * 0.5, 0);
    p.scale = scale;
    return p;
}

/// Built once: these only turn.
[Pass(128, over: KEPT * EACH * 128)]
void make(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = KEPT_SHAPES[n];
    float2 size = float2(300, 200);
    switch (k)
    {
        case 1: mesh.grid(Heart(150).relief(size, 40, 30), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(goblet().lathed(), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 4: mesh.grid(wall().extruded(120), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 5: mesh.grid(Truchet(60, 14).relief(size, 18, 8), t, CELLS, first, k, corners, triangles, meshlets); break;
        case 6: mesh.grid(Circle(60).hollowed(14).or(Box(float2(20, 160), 8)).relief(size, 30, 12), t, CELLS, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(Hexagons(70, 8).relief(size, 20, 6), t, CELLS, first, k, corners, triangles, meshlets); break;
    }
}

/// Built every frame: their shapes move.
[Pass(128, over: LIVE * EACH * 128)]
void remake(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = LIVE_SHAPES[n];
    float2 size = float2(300, 200);
    switch (k)
    {
        case 0: mesh.grid(Star(5, 80, 0.5).rotated(time * 0.3).relief(size, 30, 16), t, CELLS, first, k, liveCorners, liveTriangles, liveMeshlets); break;
        case 2: mesh.grid(Digits(floor(time) % 100, 110, 22, 2).relief(size, 25, 10), t, CELLS, first, k, liveCorners, liveTriangles, liveMeshlets); break;
        default: mesh.grid(Arrow(220, 40, 70).rotated(sin(time) * 0.4).relief(size, 30, 14), t, CELLS, first, k, liveCorners, liveTriangles, liveMeshlets); break;
    }
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
    float scale = i.rest.size.y / 810;
    // Raised parts gold, the sheet underneath grey: the height above the sheet, in its own space.
    float raised = saturate(-placed(k, scale).toLocal(i.world).z / 12);
    colour base = k == 3 || k == 4 ? colours.oklch(0.7, 0.12, float(k) / 9) : colours.lightSlateGray.mixed(colours.gold, raised);
    return mesh.lit(i, camera(scale), base, 50);
}

void graph()
{
    make();
    remake();
    shown(frame);
    moving(frame);
}
