// The mesh pool's culling, triangle lists and pages taken as needed. Twenty-four objects in a
// ring, each of a different size: their pages are taken one after another (pool.take), so the
// pool holds what they need, not the biggest times twenty-four. Every third object is an
// icosahedron built from a triangle list (pool.list), the rest grids. Each frame a pass keeps
// only the pages the orbiting camera sees (pool.cull into `seen`), and the draw shows those.
// `tint` colours each page; `cull` 0 draws every page taken.
import xanadu;

static const uint OBJECTS = 24;
static const uint MOST = 16;                     // pages the biggest object takes (a 32 x 32 grid)
static const uint PAGES = OBJECTS * MOST;        // room enough if every object were the biggest
static const float2 VIEW = float2(960, 540);     // half the view the camera sees: the 1920 x 1080 stage

/// Turns a second round the ring.
[Default(0.25)] uniform float spin;
/// Draw only the pages the camera sees.
[Default(1)] uniform int cull;
/// Colour each page apart.
[Default(0)] uniform int tint;

[Cached] [Size(PAGES)] Meshes pool;
[Cached] [Size(60)] List<MeshVertex> ico;
[Size(PAGES)] Gathered<uint> seen;

static const float3 CORNERS[12] = {
    float3(-1, 1.618, 0), float3(1, 1.618, 0), float3(-1, -1.618, 0), float3(1, -1.618, 0),
    float3(0, -1, 1.618), float3(0, 1, 1.618), float3(0, -1, -1.618), float3(0, 1, -1.618),
    float3(1.618, 0, -1), float3(1.618, 0, 1), float3(-1.618, 0, -1), float3(-1.618, 0, 1),
};
static const uint3 FACES[20] = {
    uint3(0, 11, 5), uint3(0, 5, 1), uint3(0, 1, 7), uint3(0, 7, 10), uint3(0, 10, 11),
    uint3(1, 5, 9), uint3(5, 11, 4), uint3(11, 10, 2), uint3(10, 7, 6), uint3(7, 1, 8),
    uint3(3, 9, 4), uint3(3, 4, 2), uint3(3, 2, 6), uint3(3, 6, 8), uint3(3, 8, 9),
    uint3(4, 9, 5), uint3(2, 4, 11), uint3(6, 2, 10), uint3(8, 6, 7), uint3(9, 8, 1),
};

Camera camera() { return Camera(900).orbited(time * spin, 0.2); }

/// Object k's grid: 8 to 32 cells across.
uint cellsOf(uint k) { return 8 + (k * 5) % 25; }
bool listed(uint k) { return k % 3 == 0; }
uint pagesOf(uint k) { return listed(k) ? mesh.pagesForTriangles(20) : mesh.pagesFor(uint2(cellsOf(k))); }

/// Object k round the ring.
Placement3 placed(uint k)
{
    float a = k * TAU / OBJECTS;
    Placement3 p;
    p.offset = float3(sin(a), 0, cos(a)) * 700;
    p.angles = float3(0.4, time * 0.6 + k, 0);
    p.scale = 1;
    return p;
}

/// The icosahedron's corners, three a face, each face flat.
[Pass(64, over: 60)]
void shape(uint i)
{
    uint3 face = FACES[i / 3];
    MeshVertex c;
    c.position = normalize(CORNERS[face[i % 3]]) * 90;
    c.normal = normalize(CORNERS[face.x] + CORNERS[face.y] + CORNERS[face.z]);
    c.u = float(i / 3u) / 20;
    c.v = float(i % 3u) / 2;
    ico[i] = c;
}

[Pass(1, over: 1)]
void restart(uint i) { pool.clear(); }

[Pass(64, over: OBJECTS)]
void claim(uint k) { pool.place(k, pool.take(pagesOf(k))); }

[Pass(128, over: OBJECTS * MOST * 128)]
void make(uint i)
{
    MeshPage p = pool.taken(i, MOST);
    if (p.thread >= pagesOf(p.object) * mesh.PAGE)
        return;
    if (listed(p.object))
        pool.list(CornerList(ico, 20), p.thread, p.first, p.object);
    else if (p.object % 3 == 1)
        pool.grid(Sphere(80), p, uint2(cellsOf(p.object)));
    else
        pool.grid(Torus(70, 26), p, uint2(cellsOf(p.object)));
}

[Pass(64, over: PAGES)]
void bounds(uint i)
{
    if (i < pool.used)
        pool.bound(i);
}

[Pass(1, over: 1)]
void forget(uint i) { seen.clear(); }

[Pass(64, over: PAGES)]
void look(uint i)
{
    if (i >= pool.used)
        return;
    if (cull == 0)
        seen.add(i);
    else
        pool.cull(i, placed(pool.objectOf(i)), camera(), VIEW, seen);
}

uint2 parts() { return pool.counts(int(seen[copy])); }

[OneSided]
[MeshDraw(128, 128, shade: paint, counts: parts, copies: seen)]
MeshPart shown(MeshPart m)
{
    uint n = seen[copy];
    return pool.pulled(m, placed(pool.objectOf(n)), camera(), int(n));
}

float4 paint(ItemInput i)
{
    uint n = seen[copy];
    colour base = tint != 0
        ? colours.oklch(0.72, 0.15, frac(float(n) * 0.618))
        : colours.oklch(0.72, 0.12, float(pool.objectOf(n)) / OBJECTS);
    return mesh.lit(i, camera(), base);
}

void graph()
{
    shape();
    restart();
    claim();
    make();
    bounds();
    forget();
    look();
    shown(frame);
}
