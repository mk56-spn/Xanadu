// Library tour: every surface in xanadu.mesh, built into the mesh pool (Docs/render-graph.md,
// stage E). Shapes that only turn are built once into [Cached] pages and turned live by the
// draw; the three whose shape moves (a waving sheet, travelling terrain, a lumpy ball) are
// rebuilt each frame into plain pages. Each draw is one counted draw, a page a copy.
import xanadu;

static const uint EACH = 16;                     // pages a 32 x 32 grid takes (mesh.pagesFor)
static const uint KEPT = 9;
static const uint LIVE = 3;
static const uint KEPT_SHAPES[KEPT] = { 0, 1, 2, 3, 5, 6, 7, 10, 11 };
static const uint LIVE_SHAPES[LIVE] = { 4, 8, 9 };

[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(KEPT * EACH * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(KEPT * EACH)] RWStructuredBuffer<Meshlet> meshlets;

[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<MeshVertex> liveCorners;
[Buffer(LIVE * EACH * 128)] RWStructuredBuffer<uint> liveTriangles;
[Buffer(LIVE * EACH)] RWStructuredBuffer<Meshlet> liveMeshlets;

// Laid out for a quad 810 px tall; `scale` fits it to the real one.
Camera camera(float scale) { return Camera(1400 * scale).orbited(0, 0.25); }

// Where shape k sits, in pixels on the screen at depth 0.
float3 slot(uint k) { return float3((float(k % 4) - 1.5) * 330, (floor(float(k) / 4) - 1) * 270, 0); }

Bezier3 spine() { return Bezier3(float3(-110, 60, 0), float3(-60, -140, -80), float3(60, 140, 80), float3(110, -60, 0)); }
Bezier vase() { return Bezier(float2(40, -90), float2(130, -40), float2(10, 40), float2(70, 90)); }
Waving<Sheet> flag() { return Sheet(float2(240, 170)).waving(18, 1.5, time); }
Terrain land() { return Terrain(float2(280, 240), 70, 90, time * 60); }

/// Shape k's turn and place, set by the draw every frame: a cached shape still moves.
Placement3 placed(uint k, float scale)
{
    float3 spin = float3(0.25, time * 0.5, 0);
    float3 sway = float3(0.2, sin(time * 0.5) * 0.5, 0);
    Placement3 p;
    p.offset = (slot(k) + (k == 8 ? float3(0, 40, 0) : float3(0))) * scale;
    p.angles = k == 1 ? float3(1.1, time * 0.5, 0)
             : k == 4 ? float3(0.3, sin(time) * 0.6, 0)
             : k == 6 || k == 7 ? sway
             : k == 8 ? float3(0.45, 0, 0)
             : k == 11 ? float3(0.5, time * 0.7, time * 0.3)
             : spin;
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
        case 0: mesh.grid(Sphere(100), t, cells, first, k, corners, triangles, meshlets); break;
        case 1: mesh.grid(Torus(85, 30), t, cells, first, k, corners, triangles, meshlets); break;
        case 2: mesh.grid(Cylinder(70, 170), t, cells, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(Cylinder(85, 170, 0, 1), t, cells, first, k, corners, triangles, meshlets); break;
        case 5: mesh.grid(vase().lathed(), t, cells, first, k, corners, triangles, meshlets); break;
        case 6: mesh.grid(spine().ribbon(50, 1), t, cells, first, k, corners, triangles, meshlets); break;
        case 7: mesh.grid(spine().tube(22, 0.3), t, cells, first, k, corners, triangles, meshlets); break;
        case 10: mesh.grid(Sphere(100), t, cells, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(Torus(80, 34), t, cells, first, k, corners, triangles, meshlets); break;
    }
}

/// Built every frame: their shape moves.
[Pass(128, over: LIVE * EACH * 128)]
void remake(uint i)
{
    uint n = i / (EACH * 128), t = i % (EACH * 128), first = n * EACH, k = LIVE_SHAPES[n];
    uint2 cells = uint2(32);
    switch (k)
    {
        case 4: mesh.grid(flag(), t, cells, first, k, liveCorners, liveTriangles, liveMeshlets); break;
        case 8: mesh.grid(land(), t, cells, first, k, liveCorners, liveTriangles, liveMeshlets); break;
        default: mesh.grid(Sphere(95).lumpy(16, 60, time * 0.5), t, cells, first, k, liveCorners, liveTriangles, liveMeshlets); break;
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
    colour base = colours.oklch(0.7, 0.13, float(k) / 12);
    // Something painted on in uv, so the shaping shows.
    base.paint(Checker(1.0 / 8).filled((i.uv - 0.5) * float2(2, 1), 0.01) * 0.35, colours.white);
    if (k == 8)
        base = colours.inferno(0.3 + 0.7 * saturate(-land().local(i.uv).y / 140 + 0.3));
    if (k != 10)
        return mesh.lit(i, camera(scale), base);

    // Cel shading: the light cut into three flat bands.
    float3 toEye = normalize(camera(scale).eye - i.world);
    float3 n = normalize(i.normal);
    n = dot(n, toEye) < 0 ? -n : n;
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    return colour(base.rgb * (0.3 + 0.7 * lights.toon(n, key, 3)) + lights.rim(n, toEye, 4) * 0.4, 1);
}

void graph()
{
    make();
    remake();
    shown(frame);
    moving(frame);
}
