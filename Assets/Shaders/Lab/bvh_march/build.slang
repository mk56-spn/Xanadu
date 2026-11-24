// Raymarcher four (once Labs' BVH MARCH): a galaxy of up to 262,144 melting blobs
// over a floor and a ring of pillars. BUILD rebuilds a BVH round the blobs every frame, all
// on the GPU: animate places each one and gives it a 30-bit Morton code, four rounds of an
// 8-bit radix sort put the codes in order, link builds Karras' LBVH over them and refit
// boxes it up from the leaves. For soft shadows, a sun-space grid lists the blobs each cell
// of it sees. TRACE lists what each tile of the screen sees, then marches every pixel through it.
// DEFORM and SHADE draw sparks on rings through it all, hidden behind what the trace drew.
//
// Space is in pixels, as xanadu.march's: x right, y down, z away; the item's centre is 0.
import xanadu;

static const uint MAX = 262144;             // blobs the buffers hold: the most `spheres` picks
static const uint BLOCK = 256;              // codes a radix block, threads a hist or scatter group
static const uint DIGITS = 256;             // an 8-bit digit's values
static const uint SCAN = 1024;              // threads of the one group that scans
static const uint HIST = DIGITS * (MAX / BLOCK);
static const uint NONE = 0xFFFFFFFFu;

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

/// A node: inner ones join two children, leaves hold one blob (its sorted place and its id).
struct Node
{
    float3 low;
    uint left;              // inner: a child; leaf: its place in sorted order
    float3 high;
    uint right;             // inner: the other child; leaf: the blob's id
};

[Buffer(2 * MAX)] RWStructuredBuffer<float4> balls;     // blobs by id, then from MAX in sorted order
[Buffer(2 * MAX)] RWStructuredBuffer<uint> keys;        // Morton codes: A, then B from MAX
[Buffer(2 * MAX)] RWStructuredBuffer<uint> ids;         // the same, as blob ids; A after the sort: the blob at each place
[Buffer(HIST + 2 * MAX)] RWStructuredBuffer<uint> links;    // the radix blocks' histograms, then each node's parent
[Buffer(2 * MAX)] globallycoherent RWStructuredBuffer<Node> nodes;    // inner 0..n-2, leaves n-1..2n-2
[Buffer(MAX)] globallycoherent RWStructuredBuffer<uint> arrivals;    // children arrived at each inner node
[Buffer(2 * CELLS + ENTRIES)] RWStructuredBuffer<uint> light;   // cell starts, cell ends, then blob ids
[Draws] RWStructuredBuffer<uint> drawn;                         // the sparks: a copy of the quad each

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

/// Ten bits spread three apart, for a Morton code.
uint spread(uint x)
{
    x &= 0x3FF;
    x = (x | (x << 16)) & 0x030000FF;
    x = (x | (x << 8)) & 0x0300F00F;
    x = (x | (x << 4)) & 0x030C30C3;
    x = (x | (x << 2)) & 0x09249249;
    return x;
}

[Pass(256, over: 1024 << 2 * spheres)]
void animate(uint i)
{
    float4 b = blobAt(i, time);
    balls[i] = b;
    // The galaxy's box, a little over: x and z in ±11 units, y from the floor to 8.5 up.
    float3 low = float3(-11, LIFT - 8.5, -11) * U, high = float3(11, LIFT, 11) * U;
    uint3 q = uint3(saturate((b.xyz - low) / (high - low)) * 1023.0);
    keys[i] = (spread(q.x) << 2) | (spread(q.y) << 1) | spread(q.z);
    ids[i] = i;
}

// ---- radix sort: four 8-bit rounds, A → B → A → B → A -------------------------

groupshared uint blockCounts[DIGITS];
groupshared uint blockDigits[BLOCK];
groupshared uint scanSums[SCAN];

uint keyAt(uint i, uint round) { return (round & 1) == 0 ? keys[i] : keys[MAX + i]; }
uint digitOf(uint key, uint round) { return (key >> (round * 8)) & 0xFF; }

/// Each block's count of each digit, digit-major.
[Pass(BLOCK, groups: (1024 << 2 * spheres) / BLOCK)]
void hist(uint i, uint round)
{
    blockCounts[dispatch.thread] = 0;
    GroupMemoryBarrierWithGroupSync();
    uint ignored;
    InterlockedAdd(blockCounts[digitOf(keyAt(i, round), round)], 1, ignored);
    GroupMemoryBarrierWithGroupSync();
    links[dispatch.thread * (count() / BLOCK) + dispatch.group] = blockCounts[dispatch.thread];
}

/// One group: the counts become, in place, where each digit of each block starts.
[Pass(SCAN, groups: 1)]
void scan(uint i)
{
    uint chunk = DIGITS * (count() / BLOCK) / SCAN;
    uint start = i * chunk;
    uint sum = 0;
    for (uint k = 0; k < chunk; k++)
        sum += links[start + k];
    scanSums[i] = sum;
    GroupMemoryBarrierWithGroupSync();
    for (uint step = 1; step < SCAN; step <<= 1)
    {
        uint add = i >= step ? scanSums[i - step] : 0;
        GroupMemoryBarrierWithGroupSync();
        scanSums[i] += add;
        GroupMemoryBarrierWithGroupSync();
    }

    uint running = scanSums[i] - sum;
    for (uint k = 0; k < chunk; k++)
    {
        uint n = links[start + k];
        links[start + k] = running;
        running += n;
    }
}

/// Each code to its place for this digit; stable, so earlier rounds' order holds.
[Pass(BLOCK, groups: (1024 << 2 * spheres) / BLOCK)]
void scatter(uint i, uint round)
{
    bool even = (round & 1) == 0;
    uint key = keyAt(i, round);
    uint id = even ? ids[i] : ids[MAX + i];
    uint digit = digitOf(key, round);
    blockDigits[dispatch.thread] = digit;
    GroupMemoryBarrierWithGroupSync();
    uint rank = 0;
    for (uint j = 0; j < dispatch.thread; j++)
        rank += blockDigits[j] == digit ? 1 : 0;
    uint at = links[digit * (count() / BLOCK) + dispatch.group] + rank + (even ? MAX : 0);
    keys[at] = key;
    ids[at] = id;
}

// ---- the tree: Karras 2012, then boxes up from the leaves ----------------------

/// The common prefix of the codes at sorted places a and b; equal codes go on into the places.
int prefix(int a, int b)
{
    if (b < 0 || b >= int(count()))
        return -1;
    uint x = keys[a], y = keys[b];
    if (x == y)
        return 32 + (31 - int(firstbithigh(uint(a ^ b))));
    return 31 - int(firstbithigh(x ^ y));
}

/// A thread an inner node: the range of leaves it covers, and where that range splits.
[Pass(256, over: (1024 << 2 * spheres) - 1)]
void link(uint at)
{
    int i = int(at);
    int d = prefix(i, i + 1) - prefix(i, i - 1) >= 0 ? 1 : -1;
    int least = prefix(i, i - d);
    int most = 2;
    while (prefix(i, i + most * d) > least)
        most <<= 1;
    int l = 0;
    for (int t = most >> 1; t >= 1; t >>= 1)
        if (prefix(i, i + (l + t) * d) > least)
            l += t;
    int j = i + l * d;
    int common = prefix(i, j);

    int s = 0;
    int divider = 2;
    for (int t = (l + divider - 1) / divider; ; t = (l + divider - 1) / divider)
    {
        if (prefix(i, i + (s + t) * d) > common)
            s += t;
        if (t == 1)
            break;
        divider <<= 1;
    }
    int split = i + s * d + min(d, 0);

    uint leaves = count() - 1;
    uint left = min(i, j) == split ? leaves + uint(split) : uint(split);
    uint right = max(i, j) == split + 1 ? leaves + uint(split + 1) : uint(split + 1);
    nodes[i].left = left;
    nodes[i].right = right;
    links[HIST + left] = uint(i);
    links[HIST + right] = uint(i);
    arrivals[i] = 0;
    if (i == 0)
        links[HIST] = NONE;
}

/// A thread a leaf: its box, then up the tree; the second child to arrive joins both boxes.
[Pass(256, over: 1024 << 2 * spheres)]
void refit(uint i)
{
    uint n = count();
    uint id = ids[i];
    float4 b = balls[id];
    balls[MAX + i] = b;
    float reach = b.w + blend();
    uint node = n - 1 + i;
    Node leaf;
    leaf.low = b.xyz - reach;
    leaf.high = b.xyz + reach;
    leaf.left = i;
    leaf.right = id;
    nodes[node] = leaf;

    uint up = links[HIST + node];
    while (up != NONE)
    {
        AllMemoryBarrier();
        uint before;
        InterlockedAdd(arrivals[up], 1, before);
        if (before == 0)
            return;
        AllMemoryBarrier();
        Node a = nodes[nodes[up].left];
        Node c = nodes[nodes[up].right];
        nodes[up].low = min(a.low, c.low);
        nodes[up].high = max(a.high, c.high);
        up = links[HIST + up];
    }
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
void clearLight(uint i) { light[CELLS + i] = 0; }

[Pass(256, over: 1024 << 2 * spheres)]
void lightCount(uint i)
{
    int4 cells = cellsOf(balls[i]);
    uint ignored;
    for (int y = cells.y; y <= cells.w; y++)
        for (int x = cells.x; x <= cells.z; x++)
            InterlockedAdd(light[CELLS + y * LIGHT + x], 1, ignored);
}

/// One group: the counts become where each cell's entries start, and the fill cursors start there.
[Pass(SCAN, groups: 1)]
void lightScan(uint i)
{
    uint chunk = CELLS / SCAN;
    uint start = i * chunk;
    uint sum = 0;
    for (uint k = 0; k < chunk; k++)
        sum += light[CELLS + start + k];
    scanSums[i] = sum;
    GroupMemoryBarrierWithGroupSync();
    for (uint step = 1; step < SCAN; step <<= 1)
    {
        uint add = i >= step ? scanSums[i - step] : 0;
        GroupMemoryBarrierWithGroupSync();
        scanSums[i] += add;
        GroupMemoryBarrierWithGroupSync();
    }
    uint running = scanSums[i] - sum;
    for (uint k = 0; k < chunk; k++)
    {
        uint n = light[CELLS + start + k];
        light[start + k] = running;
        light[CELLS + start + k] = running;
        running += n;
    }
}

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

[Pass(1, over: 1)]
void countSparks(uint i) { drawn[0] = sparks ? SPARKS : 0; }

void build()
{
    countSparks();
    animate();
    for (uint round = 0; round < 4; round++)
    {
        hist(round);
        scan();
        scatter(round);
    }
    link();
    refit();
    if (shadows && sunGrid)
    {
        clearLight();
        lightCount();
        lightScan();
        lightFill();
    }
}
