// The mesh core's 2D door (mesh.flat): the same pool as Lab/meshes, read as the plane. x and y
// are pixels on the item, z the layer: nearer layers win wherever they overlap, whatever order
// the pages draw in. Built once into [Cached] pages, placed and turned live by the draw, unlit.
import xanadu;

static const uint EACH = 4;                      // pages a 16 x 16 grid takes (mesh.pagesFor)
static const uint THINGS = 6;
static const uint PAGES = THINGS * EACH;

[Cached] [Size(PAGES)] Meshes pool;

Bezier swirl() { return Bezier(float2(-260, 80), float2(-120, -220), float2(120, 220), float2(260, -80)); }
Bezier hook() { return Bezier(float2(-60, 200), float2(-60, -120), float2(160, -160), float2(160, 0)); }

/// Thing k's layer: more is farther. The swirl lies over every card, the hook between two.
float layer(uint k) { return float[6](30, 20, 10, 0, 15, 25)[k]; }

[Pass(128, over: PAGES * 128)]
void make(uint i)
{
    MeshPage p = pool.page(i, EACH);
    uint k = p.object;
    float3 at = float3(0, 0, layer(k));
    uint2 cells = uint2(16);
    switch (k)
    {
        case 0: pool.grid(Sheet(float2(260, 180)).moved(at), p, cells); break;
        case 1: pool.grid(Sheet(float2(220, 220)).moved(at), p, cells); break;
        case 2: pool.grid(Sheet(float2(180, 260)).moved(at), p, cells); break;
        case 3: pool.grid(swirl().stroked(46, 0.2).moved(at), p, cells); break;
        case 4: pool.grid(hook().stroked(30, 1.8).moved(at), p, cells); break;
        default: pool.grid(Sheet(float2(140, 140)).moved(at), p, cells); break;
    }
}

uint2 parts() { return pool.counts(); }

[MeshDraw(128, 128, shade: look, counts: parts, copies: PAGES)]
MeshPart shown(MeshPart m)
{
    uint k = pool.objectOf(copy);
    float scale = m.corner.rest.size.y / 810;
    // The cards fan out and turn; the strokes sway.
    float2 offset = k < 3 || k == 5 ? float2((float(k % 3) - 1) * 220 + (k == 5 ? 380 : 0), (k == 5 ? -180 : 0)) : float2(0);
    float angle = k < 3 || k == 5 ? (float(k) - 1) * 0.3 + sin(time + k) * 0.2 : sin(time * 0.7 + k) * 0.25;
    return pool.flat(m, offset * scale, angle, scale);
}

float4 look(ItemInput i)
{
    uint k = pool.objectOf(copy);
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
