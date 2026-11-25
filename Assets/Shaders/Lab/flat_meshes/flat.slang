// The mesh core's 2D door (mesh.flat): the same pool as Lab/meshes, read as the plane. x and y
// are pixels on the item, z the layer: nearer layers win wherever they overlap, whatever order
// the pages draw in. Built once into [Cached] pages, placed and turned live by the draw, unlit.
import xanadu;

static const uint EACH = 4;                      // pages a 16 x 16 grid takes (mesh.pagesFor)
static const uint THINGS = 6;
static const uint PAGES = THINGS * EACH;

[Cached] [Buffer(PAGES * 128)] RWStructuredBuffer<MeshVertex> corners;
[Cached] [Buffer(PAGES * 128)] RWStructuredBuffer<uint> triangles;
[Cached] [Buffer(PAGES)] RWStructuredBuffer<Meshlet> meshlets;

Bezier swirl() { return Bezier(float2(-260, 80), float2(-120, -220), float2(120, 220), float2(260, -80)); }
Bezier hook() { return Bezier(float2(-60, 200), float2(-60, -120), float2(160, -160), float2(160, 0)); }

/// Thing k's layer: more is farther. The swirl lies over every card, the hook between two.
float layer(uint k) { return float[6](30, 20, 10, 0, 15, 25)[k]; }

[Pass(128, over: PAGES * 128)]
void make(uint i)
{
    uint k = i / (EACH * 128), t = i % (EACH * 128), first = k * EACH;
    float3 at = float3(0, 0, layer(k));
    uint2 cells = uint2(16);
    switch (k)
    {
        case 0: mesh.grid(Sheet(float2(260, 180)).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
        case 1: mesh.grid(Sheet(float2(220, 220)).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
        case 2: mesh.grid(Sheet(float2(180, 260)).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
        case 3: mesh.grid(swirl().stroked(46, 0.2).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
        case 4: mesh.grid(hook().stroked(30, 1.8).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
        default: mesh.grid(Sheet(float2(140, 140)).moved(at), t, cells, first, k, corners, triangles, meshlets); break;
    }
}

[Draw(64, 1, shade: look, copies: PAGES)]
ItemVertex shown(ItemVertex v)
{
    uint k = meshlets[copy].object;
    float scale = v.rest.size.y / 810;
    // The cards fan out and turn; the strokes sway.
    float2 offset = k < 3 || k == 5 ? float2((float(k % 3) - 1) * 220 + (k == 5 ? 380 : 0), (k == 5 ? -180 : 0)) : float2(0);
    float angle = k < 3 || k == 5 ? (float(k) - 1) * 0.3 + sin(time + k) * 0.2 : sin(time * 0.7 + k) * 0.25;
    return mesh.flat(v, offset * scale, angle, scale, corners, triangles, meshlets);
}

float4 look(ItemInput i)
{
    uint k = meshlets[copy].object;
    colour c = colours.oklch(0.7, 0.15, float(k) / 6);
    if (k == 3 || k == 4)
        return colours.oklch(0.8, 0.12, 0.1 + 0.3 * i.uv.x).mixed(colours.white, 0.4 * (1 - abs(i.uv.y - 0.5) * 2));
    c.paint(Checker(1.0 / 4).filled(i.uv - 0.5, 0.01) * 0.25, colours.white);
    return c;
}

void graph()
{
    make();
    shown(frame);
}
