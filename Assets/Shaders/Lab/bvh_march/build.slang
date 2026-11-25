// Raymarcher four (once Labs' BVH MARCH): a galaxy of up to 262,144 melting blobs
// over a floor and a ring of pillars. BUILD rebuilds a BVH round the blobs every frame, all
// on the GPU, from the library: animate places each one and gives it a Morton code
// (bvh.morton), sort.radix puts the codes in order, leaves boxes each blob and bvh.tree links
// Karras' LBVH over them and boxes it up from the leaves. For soft shadows, a sun-space grid
// lists the blobs each cell of it sees. `march` (march.slang) lists what each tile of the screen
// sees, then marches every pixel through it; present shows it with its depth. `spark`
// (sparks.slang) draws sparks on rings through it all, hidden behind what the march drew.
//
// Space is in pixels, as xanadu.march's: x right, y down, z away; the item's centre is 0.
import xanadu;

static const uint MAX = 262144;             // blobs the buffers hold: the most `spheres` picks
static const float U = 28;                  // pixels a unit of the galaxy
static const float LIFT = 3.6;              // the galaxy's height over the floor, in units, at the view's centre
static const uint LIGHT = 256;              // the sun's grid: cells across and down
static const uint CELLS = LIGHT * LIGHT;
static const float REACH = 18 * U;          // half the grid's width: the galaxy from any sun
static const uint ENTRIES = 8u << 20;       // blob-in-cell entries the grid holds; more are dropped
static const uint SPARKS = 6144;

[Options("1,024|4,096|16,384|65,536|262,144")] [Default(2)] uniform int spheres;
/// How far blobs melt into each other, in blob radii.
[Default(0.6)] uniform float melt;
/// Soft shadows from the blobs (and hard ones from the pillars).
[Default(1)] uniform bool shadows;
/// Soft shadows from the sun's grid; off, each lit point walks the BVH toward the sun (slow, to check it).
[Default(1)] uniform bool sunGrid;
/// Sparks on rings through the galaxy: drawn after the march, behind the blobs where they pass behind.
[Default(1)] uniform bool sparks;

/// How many blobs `spheres` picks.
uint count() { return 1024u << (2 * uint(spheres)); }

/// A blob's mean radius, in pixels: smaller as there are more, so the galaxy fills alike.
float radius() { return 0.32 * U * pow(4096.0 / count(), 1.0 / 3.0); }

/// How far the melt reaches, in pixels.
float blend() { return melt * radius(); }

/// The way to the sun.
float3 sun() { return normalize(float3(-0.45, -0.8, 0.35)); }

[Buffer(MAX)] RWStructuredBuffer<float4> balls;                 // blobs by id
[Buffer(MAX)] RWStructuredBuffer<float4> sorted;                // the same, in sorted order
[Buffer(MAX)] RWStructuredBuffer<uint> keys;                    // Morton codes, sorted in place
[Buffer(MAX)] RWStructuredBuffer<uint> ids;                     // the blob at each place, once sorted
[Buffer(MAX)] RWStructuredBuffer<uint> keysB;                   // the sort's other half
[Buffer(MAX)] RWStructuredBuffer<uint> idsB;
[Buffer(MAX)] RWStructuredBuffer<uint> counts;                  // the sort's histograms: 256 a block of 256
[Buffer(2 * MAX)] globallycoherent RWStructuredBuffer<bvh.Node> nodes;  // inner 0..n-2, leaves n-1..2n-2
[Buffer(2 * MAX)] RWStructuredBuffer<uint> parents;             // each node's parent
[Buffer(MAX)] globallycoherent RWStructuredBuffer<uint> arrivals;   // children arrived at each inner node
[Buffer(2 * CELLS + ENTRIES)] RWStructuredBuffer<uint> light;   // cell starts, cell ends, then blob ids
[Image(rgba16f)] RWTexture2D<float4> marched;                   // what the march sees, a pixel each
[Image(r32f)] RWTexture2D<float> marchedDepth;                   // and how far into the screen

// ---- animate -----------------------------------------------------------------

/// Blob i at time t, in pixels: a three-armed galaxy, inner stars faster.
float4 blobAt(uint i, float t)
{
    float u = frac(i * 0.6180339887);
    float v = frac(i * 0.7548776662);
    float w = frac(i * 0.5698402910);
    float ring = 1.0 + 8.8 * sqrt(u);
    float arm = floor(v * 3.0) * (2.0 * PI / 3.0);
    float scatter = (w - 0.5) * 1.6 * (1.25 - u * 0.6);
    float angle = arm + ring * 0.55 + scatter + t * 1.4 / sqrt(ring);
    float y = 4.2 + (frac(i * 0.4142135) - 0.5) * 2.6 * (1.0 - u * 0.65) + sin(t * 1.3 + i * 0.37) * 0.3;
    float size = radius() * (0.55 + 0.9 * frac(i * 0.3247));
    return float4(cos(angle) * ring * U, (LIFT - y) * U, sin(angle) * ring * U, size);
}

[Pass(256, over: 1024 << 2 * spheres)]
void animate(uint i)
{
    float4 b = blobAt(i, time);
    balls[i] = b;
    // The galaxy's box, a little over: x and z in ±11 units, y from the floor to 8.5 up.
    keys[i] = bvh.morton(b.xyz, Bounds(float3(-11, LIFT - 8.5, -11) * U, float3(11, LIFT, 11) * U));
    ids[i] = i;
}

/// A leaf each blob, at its sorted place: its box grown by the melt, and its sorted copy.
[Pass(256, over: 1024 << 2 * spheres)]
void leaves(uint i)
{
    uint id = ids[i];
    float4 b = balls[id];
    sorted[i] = b;
    nodes[count() - 1 + i] = bvh.leaf(Ball(b).grown(blend()).box, i, id);
}

// ---- the sun's grid: count, scan, fill ------------------------------------------

/// The sun's view: two axes across its rays.
float3x3 sunBasis()
{
    float3 across = normalize(cross(sun(), float3(0, 1, 0)));
    return float3x3(across, cross(across, sun()), sun());
}

/// How far past a blob's outline its penumbra still counts.
float penumbra() { return radius() * 1.5 + blend(); }

/// The cell a point falls in, seen from the sun.
int2 cellOf(float3 p)
{
    float3x3 basis = sunBasis();
    float2 at = float2(dot(p, basis[0]), dot(p, basis[1]));
    return int2(floor((at + REACH) * (LIGHT / (2.0 * REACH))));
}

/// The cells a blob's grown outline covers: low and high corners, inclusive.
int4 cellsOf(float4 b)
{
    float3x3 basis = sunBasis();
    float2 at = float2(dot(b.xyz, basis[0]), dot(b.xyz, basis[1]));
    float reach = b.w + penumbra();
    float scale = LIGHT / (2.0 * REACH);
    int2 low = int2(floor((at - reach + REACH) * scale)), high = int2(floor((at + reach + REACH) * scale));
    return int4(max(low, 0), min(high, int(LIGHT) - 1));
}

[Pass(256, over: CELLS)]
void clearLight(uint i) { light[i] = 0; }

[Pass(256, over: 1024 << 2 * spheres)]
void lightCount(uint i)
{
    int4 cells = cellsOf(balls[i]);
    uint ignored;
    for (int y = cells.y; y <= cells.w; y++)
        for (int x = cells.x; x <= cells.z; x++)
            InterlockedAdd(light[y * LIGHT + x], 1, ignored);
}

/// The fill cursors start where each cell's entries do (the counts, scanned in place).
[Pass(256, over: CELLS)]
void lightCursors(uint i) { light[CELLS + i] = light[i]; }

[Pass(256, over: 1024 << 2 * spheres)]
void lightFill(uint i)
{
    int4 cells = cellsOf(balls[i]);
    for (int y = cells.y; y <= cells.w; y++)
    {
        for (int x = cells.x; x <= cells.z; x++)
        {
            uint slot;
            InterlockedAdd(light[CELLS + y * LIGHT + x], 1, slot);
            if (slot < ENTRIES)
                light[2 * CELLS + slot] = i;
        }
    }
}

void graph()
{
    uint n = 1024 << 2 * spheres;
    animate();
    sort.radix(n, keys, ids, keysB, idsB, counts);
    leaves();
    bvh.tree(n, keys, nodes, parents, arrivals);
    if (shadows && sunGrid)
    {
        clearLight();
        lightCount();
        sort.scan(CELLS, 0, light);
        lightCursors();
        lightFill();
    }
    march();
    present(marched, marchedDepth);
    if (sparks)
        spark(frame);
}
