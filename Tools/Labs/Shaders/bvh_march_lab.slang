// Xanadu Labs' fourth raymarcher: a galaxy of melting blobs over a floor and a
// ring of pillars, all marched in compute through a BVH rebuilt every frame.
// The other three (Assets/Shaders/Lab/raymarch_*) cull with a fixed tile grid
// or one quad per sphere; this one scales to a quarter of a million spheres.
//
// Every frame, all on the GPU:
//   animate     a thread per sphere: where it is now, its 30-bit Morton code
//   hist/scan/scatter ×4   an 8-bit LSD radix sort of the codes (stable)
//   build       a thread per inner node: Karras' LBVH, ties broken by index
//   refit       a thread per leaf: boxes up the tree, the second child to
//               arrive at a node joins the two (atomics, coherent memory)
//   raster      the floor and the pillars, drawn with depth (reverse z)
//   tiles       a thread per 8×8 tile: the tile's frustum, capped by the
//               farthest raster depth in it, walks the BVH and keeps the
//               spheres that can show there (up to TILE_CAP; more is overflow)
//   light*      count, scan, fill: each sphere listed in the cells of a sun-space
//               grid its outline covers, so a shadow ray reads one cell
//   march       a group per tile: the tile's list into groupshared memory, a
//               ray per pixel gathers the K spheres it meets first, marches
//               them, gathers again past them if it has to, stops at the
//               raster depth, shades with BVH soft shadows (on the floor and
//               pillars too), writes colour and depth
//   sparks      the march's depth goes back into the depth buffer (SV_Depth),
//               then glowing sparks draw depth tested against blobs and floor
//
// SDL_GPU's SPIR-V layout: compute samplers set 0, read-write textures then
// read-write buffers set 1, uniforms set 2; vertex uniforms set 1; fragment
// samplers set 2, uniforms set 3.

// ---- shared ----------------------------------------------------------------

static const uint SORT_GROUP = 256;           // elements per radix block, threads per hist/scatter group
static const uint SCAN_THREADS = 1024;
static const uint DIGITS = 256;
static const uint TILE = 8;
static const uint TILE_CAP = 256;             // spheres a tile can list; past that its pixels walk the BVH
static const uint OVERFLOW = 0xFFFFFFFFu;
static const uint NONE = 0xFFFFFFFFu;
static const uint K = 8;                      // spheres a ray marches at once, the nearest-entering
static const uint STACK = 32;
static const uint PILLARS = 10;
static const uint SPARKS = 6144;
static const float PI = 3.14159265;

// Views.
static const uint VIEW_LIT = 0;
static const uint VIEW_STEPS = 1;
static const uint VIEW_NODES = 2;
static const uint VIEW_TILES = 3;
static const uint VIEW_SHADOW = 4;
static const uint VIEW_DEPTH = 5;
static const uint VIEW_NORMALS = 6;
static const uint VIEW_ROUNDS = 7;

// Flags.
static const uint FLAG_SHADOWS = 1;
static const uint FLAG_TILES = 2;             // cull through tile lists; off walks the BVH per pixel
static const uint FLAG_LIGHT_GRID = 4;        // shadows from the light-space grid; off walks the BVH

// The light-space grid: the sun's rays are parallel, so a sphere shadows only the
// column of cells its outline (grown by the penumbra's reach) covers seen from the sun.
static const uint LIGHT = 256;                // cells across and down
static const uint LIGHT_CELLS = LIGHT * LIGHT;
static const float LIGHT_EXTENT = 18.0;       // half its width in world units: the galaxy seen from any sun
static const uint LIGHT_ENTRIES = 8u << 20;   // sphere-in-cell entries it can hold; more are dropped

struct Frame
{
    float4 eye;         // xyz, time in w
    float4 right;       // xyz, focal x in w
    float4 up;          // xyz, focal y in w
    float4 forward;     // xyz, near plane in w
    float4 size;        // width, height, tiles across, tiles down
    uint4 counts;       // spheres, radix blocks, sort pass, view
    float4 look;        // blend, sphere radius, softness, spin
    float4 sun;         // direction to the sun, flags in w (as a uint)
};

struct Node
{
    float3 lo;
    uint left;          // inner: child node; leaf: its place in sorted order
    float3 hi;
    uint right;         // inner: child node; leaf: NONE
};

/// The eye's distance along `dir` to a raster depth (reverse z, infinite far: near / view z).
float distanceAt(float depth, float3 dir, Frame f)
{
    if (depth <= 0.0)
        return 1e30;
    return f.forward.w / depth / max(dot(dir, f.forward.xyz), 1e-4);
}

/// The ray through pixel (x, y), y down.
float3 rayThrough(float2 pixel, Frame f)
{
    float2 ndc = float2(pixel.x / f.size.x * 2.0 - 1.0, 1.0 - pixel.y / f.size.y * 2.0);
    return normalize(f.forward.xyz + f.right.xyz * (ndc.x / f.right.w) + f.up.xyz * (ndc.y / f.up.w));
}

/// Clip position of a world point: reverse z with no far plane.
float4 clipOf(float3 p, Frame f)
{
    float3 d = p - f.eye.xyz;
    float3 v = float3(dot(d, f.right.xyz), dot(d, f.up.xyz), dot(d, f.forward.xyz));
    return float4(v.x * f.right.w, v.y * f.up.w, f.forward.w, v.z);
}

float3 hsv(float h, float s, float v)
{
    float3 k = saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
    return v * lerp(float3(1, 1, 1), k, s);
}

float3 heat(float x)
{
    x = saturate(x);
    return saturate(float3(1.5 * x, 2.2 * x - 0.6, 3.0 * x - 2.0) + float3(0.05, 0.04, 0.12) * (1.0 - x));
}

/// Sphere i at time t: a three-armed galaxy over the floor, inner stars faster.
float4 sphereAt(uint i, float t, float radius)
{
    float u = frac(i * 0.6180339887);
    float v = frac(i * 0.7548776662);
    float w = frac(i * 0.5698402910);
    float ring = 1.0 + 8.8 * sqrt(u);
    float arm = floor(v * 3.0) * (2.0 * PI / 3.0);
    float scatter = (w - 0.5) * 1.6 * (1.25 - u * 0.6);
    float angle = arm + ring * 0.55 + scatter + t * 1.4 / sqrt(ring);
    float y = 4.2 + (frac(i * 0.4142135) - 0.5) * 2.6 * (1.0 - u * 0.65) + sin(t * 1.3 + i * 0.37) * 0.3;
    float size = radius * (0.55 + 0.9 * frac(i * 0.3247));
    return float4(cos(angle) * ring, y, sin(angle) * ring, size);
}

/// The pillars: a ring through the galaxy's arms. Each is a box: centre xz, half width, height.
float4 pillar(uint k)
{
    float a = k * (2.0 * PI / PILLARS) + 0.3;
    float r = (k & 1) == 0 ? 6.2 : 3.6;
    return float4(cos(a) * r, sin(a) * r, 0.35, (k & 1) == 0 ? 7.5 : 5.2);
}

// ---- compute bindings --------------------------------------------------------

[[vk::binding(0, 0)]] Sampler2D rasterColour;
[[vk::binding(1, 0)]] Sampler2D rasterDepth;

[[vk::binding(0, 1)]] [[vk::image_format("rgba8")]] RWTexture2D<float4> outColour;
[[vk::binding(1, 1)]] [[vk::image_format("r32f")]] RWTexture2D<float> outDepth;
// SDL_GPU allows 8 read-write buffers a compute pipeline, so related arrays share one.
static const uint MAX_N = 262144;
static const uint HIST_WORDS = DIGITS * (MAX_N / SORT_GROUP);
[[vk::binding(2, 1)]] RWStructuredBuffer<float4> balls;          // spheres by id, then from MAX_N in sorted order
[[vk::binding(3, 1)]] RWStructuredBuffer<uint> keys;             // A, then B from MAX_N
[[vk::binding(4, 1)]] RWStructuredBuffer<uint> vals;             // A, then B from MAX_N; A after the sort: sphere id per place
[[vk::binding(5, 1)]] RWStructuredBuffer<uint> links;            // radix block histograms (digit-major), then node parents
[[vk::binding(6, 1)]] globallycoherent RWStructuredBuffer<Node> nodes;   // inner 0..N-2, leaves N-1..2N-2
[[vk::binding(7, 1)]] globallycoherent RWStructuredBuffer<uint> arrivals;
[[vk::binding(8, 1)]] RWStructuredBuffer<uint> tileData;         // per tile: count (or OVERFLOW) and nodes visited; then TILE_CAP places each
[[vk::binding(9, 1)]] RWStructuredBuffer<uint> light;            // cell starts, cell ends (counts, then fill cursors), then sphere ids

[[vk::binding(0, 2)]] ConstantBuffer<Frame> frame;

// ---- animate + Morton --------------------------------------------------------

uint spread10(uint x)
{
    x &= 0x3FF;
    x = (x | (x << 16)) & 0x030000FF;
    x = (x | (x << 8)) & 0x0300F00F;
    x = (x | (x << 4)) & 0x030C30C3;
    x = (x | (x << 2)) & 0x09249249;
    return x;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void animate(uint3 id: SV_DispatchThreadID)
{
    uint i = id.x;
    if (i < LIGHT_CELLS)
        light[LIGHT_CELLS + i] = 0;
    if (i >= frame.counts.x)
        return;
    float4 s = sphereAt(i, frame.eye.w * frame.look.w, frame.look.y);
    balls[i] = s;
    // The galaxy's box, a little over: x and z in ±11, y in 0..8.
    float3 n = saturate((s.xyz - float3(-11, 0, -11)) / float3(22, 8.5, 22));
    uint3 q = uint3(n * 1023.0);
    keys[i] = (spread10(q.x) << 2) | (spread10(q.y) << 1) | spread10(q.z);
    vals[i] = i;
}

// ---- radix sort ----------------------------------------------------------------

groupshared uint sharedHist[DIGITS];
groupshared uint sharedDigit[SORT_GROUP];

uint sortKey(uint i) { return (frame.counts.z & 1) == 0 ? keys[i] : keys[MAX_N + i]; }
uint digitOf(uint key) { return (key >> (frame.counts.z * 8)) & 0xFF; }

[shader("compute")]
[numthreads(256, 1, 1)]
void hist(uint3 id: SV_DispatchThreadID, uint3 group: SV_GroupID, uint3 local: SV_GroupThreadID)
{
    sharedHist[local.x] = 0;
    GroupMemoryBarrierWithGroupSync();
    uint ignored;
    InterlockedAdd(sharedHist[digitOf(sortKey(id.x))], 1, ignored);
    GroupMemoryBarrierWithGroupSync();
    links[local.x * frame.counts.y + group.x] = sharedHist[local.x];
}

groupshared uint scanSums[SCAN_THREADS];

/// One group: the block histograms become, in place, where each digit of each block starts.
[shader("compute")]
[numthreads(1024, 1, 1)]
void scan(uint3 local: SV_GroupThreadID)
{
    uint total = DIGITS * frame.counts.y;
    uint chunk = total / SCAN_THREADS;
    uint start = local.x * chunk;
    uint sum = 0;
    for (uint k = 0; k < chunk; k++)
        sum += links[start + k];
    scanSums[local.x] = sum;
    GroupMemoryBarrierWithGroupSync();

    // Hillis-Steele over the chunk sums, then shift to exclusive.
    for (uint step = 1; step < SCAN_THREADS; step <<= 1)
    {
        uint add = local.x >= step ? scanSums[local.x - step] : 0;
        GroupMemoryBarrierWithGroupSync();
        scanSums[local.x] += add;
        GroupMemoryBarrierWithGroupSync();
    }

    uint running = scanSums[local.x] - sum;
    for (uint k = 0; k < chunk; k++)
    {
        uint v = links[start + k];
        links[start + k] = running;
        running += v;
    }
}

[shader("compute")]
[numthreads(256, 1, 1)]
void scatter(uint3 id: SV_DispatchThreadID, uint3 group: SV_GroupID, uint3 local: SV_GroupThreadID)
{
    bool even = (frame.counts.z & 1) == 0;
    uint key = even ? keys[id.x] : keys[MAX_N + id.x];
    uint val = even ? vals[id.x] : vals[MAX_N + id.x];
    uint digit = digitOf(key);
    sharedDigit[local.x] = digit;
    GroupMemoryBarrierWithGroupSync();

    // Stable: the block's earlier elements with the same digit go first.
    uint rank = 0;
    for (uint j = 0; j < local.x; j++)
        rank += sharedDigit[j] == digit ? 1 : 0;
    uint at = links[digit * frame.counts.y + group.x] + rank;
    if (even)
    {
        keys[MAX_N + at] = key;
        vals[MAX_N + at] = val;
    }
    else
    {
        keys[at] = key;
        vals[at] = val;
    }
}

// ---- LBVH build (Karras 2012) ------------------------------------------------

/// Common prefix length of the codes at sorted places i and j; equal codes go on into the places.
int delta(int i, int j)
{
    int n = int(frame.counts.x);
    if (j < 0 || j >= n)
        return -1;
    uint a = keys[i], b = keys[j];
    if (a == b)
        return 32 + (31 - int(firstbithigh(uint(i ^ j))));
    return 31 - int(firstbithigh(a ^ b));
}

[shader("compute")]
[numthreads(256, 1, 1)]
void build(uint3 id: SV_DispatchThreadID)
{
    int n = int(frame.counts.x);
    int i = int(id.x);
    if (i >= n - 1)
        return;

    int d = delta(i, i + 1) - delta(i, i - 1) >= 0 ? 1 : -1;
    int least = delta(i, i - d);
    int most = 2;
    while (delta(i, i + most * d) > least)
        most <<= 1;
    int l = 0;
    for (int t = most >> 1; t >= 1; t >>= 1)
    {
        if (delta(i, i + (l + t) * d) > least)
            l += t;
    }
    int j = i + l * d;
    int shared = delta(i, j);

    int s = 0;
    int divider = 2;
    for (int t = (l + divider - 1) / divider; ; t = (l + divider - 1) / divider)
    {
        if (delta(i, i + (s + t) * d) > shared)
            s += t;
        if (t == 1)
            break;
        divider <<= 1;
    }
    int split = i + s * d + min(d, 0);

    uint leaves = uint(n - 1);
    uint left = min(i, j) == split ? leaves + uint(split) : uint(split);
    uint right = max(i, j) == split + 1 ? leaves + uint(split + 1) : uint(split + 1);
    nodes[i].left = left;
    nodes[i].right = right;
    links[HIST_WORDS + left] = uint(i);
    links[HIST_WORDS + right] = uint(i);
    arrivals[i] = 0;
    if (i == 0)
        links[HIST_WORDS + 0] = NONE;
}

[shader("compute")]
[numthreads(256, 1, 1)]
void refit(uint3 id: SV_DispatchThreadID)
{
    uint n = frame.counts.x;
    uint i = id.x;
    if (i >= n)
        return;
    float4 s = balls[vals[i]];
    balls[MAX_N + i] = s;
    float reach = s.w + frame.look.x;
    uint node = n - 1 + i;
    Node leaf;
    leaf.lo = s.xyz - reach;
    leaf.hi = s.xyz + reach;
    leaf.left = i;
    leaf.right = NONE;
    nodes[node] = leaf;

    // Up the tree: the first child to arrive leaves, the second joins both boxes.
    uint up = n > 1 ? links[HIST_WORDS + node] : NONE;
    while (up != NONE)
    {
        AllMemoryBarrier();
        uint before;
        InterlockedAdd(arrivals[up], 1, before);
        if (before == 0)
            return;
        AllMemoryBarrier();
        Node a = nodes[nodes[up].left];
        Node b = nodes[nodes[up].right];
        nodes[up].lo = min(a.lo, b.lo);
        nodes[up].hi = max(a.hi, b.hi);
        up = links[HIST_WORDS + up];
    }
}

// ---- light-space grid ----------------------------------------------------------

/// The sun's view: two axes across its rays.
float3x3 lightBasis()
{
    float3 toSun = frame.sun.xyz;
    float3 across = normalize(cross(toSun, float3(0, 1, 0)));
    return float3x3(across, cross(across, toSun), toSun);
}

/// How far past a sphere's outline its penumbra still counts.
float penumbra() { return frame.look.y * 1.5 + frame.look.x; }

/// The cells a sphere's grown outline covers, as low and high corners (inclusive).
int4 lightCells(float4 b)
{
    float3x3 basis = lightBasis();
    float2 at = float2(dot(b.xyz, basis[0]), dot(b.xyz, basis[1]));
    float reach = b.w + penumbra();
    float scale = LIGHT / (2.0 * LIGHT_EXTENT);
    int2 lo = int2(floor((at - reach + LIGHT_EXTENT) * scale));
    int2 hi = int2(floor((at + reach + LIGHT_EXTENT) * scale));
    return int4(max(lo, 0), min(hi, int(LIGHT) - 1));
}

[shader("compute")]
[numthreads(256, 1, 1)]
void lightCount(uint3 id: SV_DispatchThreadID)
{
    if (id.x >= frame.counts.x)
        return;
    int4 cells = lightCells(balls[id.x]);
    uint ignored;
    for (int y = cells.y; y <= cells.w; y++)
        for (int x = cells.x; x <= cells.z; x++)
            InterlockedAdd(light[LIGHT_CELLS + y * LIGHT + x], 1, ignored);
}

/// One group: the counts become where each cell's entries start, and the fill cursors start there.
[shader("compute")]
[numthreads(1024, 1, 1)]
void lightScan(uint3 local: SV_GroupThreadID)
{
    uint chunk = LIGHT_CELLS / SCAN_THREADS;
    uint start = local.x * chunk;
    uint sum = 0;
    for (uint k = 0; k < chunk; k++)
        sum += light[LIGHT_CELLS + start + k];
    scanSums[local.x] = sum;
    GroupMemoryBarrierWithGroupSync();
    for (uint step = 1; step < SCAN_THREADS; step <<= 1)
    {
        uint add = local.x >= step ? scanSums[local.x - step] : 0;
        GroupMemoryBarrierWithGroupSync();
        scanSums[local.x] += add;
        GroupMemoryBarrierWithGroupSync();
    }
    uint running = scanSums[local.x] - sum;
    for (uint k = 0; k < chunk; k++)
    {
        uint count = light[LIGHT_CELLS + start + k];
        light[start + k] = running;
        light[LIGHT_CELLS + start + k] = running;
        running += count;
    }
}

[shader("compute")]
[numthreads(256, 1, 1)]
void lightFill(uint3 id: SV_DispatchThreadID)
{
    if (id.x >= frame.counts.x)
        return;
    int4 cells = lightCells(balls[id.x]);
    for (int y = cells.y; y <= cells.w; y++)
    {
        for (int x = cells.x; x <= cells.z; x++)
        {
            uint slot;
            InterlockedAdd(light[LIGHT_CELLS + y * LIGHT + x], 1, slot);
            if (slot < LIGHT_ENTRIES)
                light[2 * LIGHT_CELLS + slot] = id.x;
        }
    }
}

/// Quilez's analytic soft shadow of one sphere on the ray from p toward the sun.
float sphereShadow(float3 p, float3 toSun, float4 b, float softness)
{
    float3 oc = p - b.xyz;
    float along = dot(oc, toSun);
    float h = along * along - dot(oc, oc) + b.w * b.w;
    float d = sqrt(max(0.0, b.w * b.w - h)) - b.w;
    float t = -along - sqrt(max(h, 0.0));
    return t > 0.0 ? smoothstep(0.0, 1.0, 2.5 * softness * d / t) : 1.0;
}

/// The pillars, hard: 0 when one stands between p and the sun.
float pillarShadow(float3 p, float3 toSun)
{
    for (uint k = 0; k < PILLARS; k++)
    {
        float4 c = pillar(k);
        float3 lo = float3(c.x - c.z, 0, c.y - c.z), hi = float3(c.x + c.z, c.w, c.y + c.z);
        float2 s = slab(p, 1.0 / toSun, lo, hi);
        if (s.y >= max(s.x, 0.0))
            return 0.0;
    }
    return 1.0;
}

// ---- traversal helpers -------------------------------------------------------

/// Where a ray is inside a box: (enter, leave); missed when leave < enter.
float2 slab(float3 origin, float3 inverse, float3 lo, float3 hi)
{
    float3 a = (lo - origin) * inverse, b = (hi - origin) * inverse;
    float3 near = min(a, b), far = max(a, b);
    return float2(max(max(near.x, near.y), near.z), min(min(far.x, far.y), far.z));
}

/// Where a ray is inside a ball: (enter, leave); missed when leave < enter.
float2 inBall(float3 origin, float3 dir, float3 centre, float radius)
{
    float3 to = centre - origin;
    float along = dot(dir, to);
    float h = along * along - dot(to, to) + radius * radius;
    if (h < 0.0)
        return float2(1, 0);
    float half = sqrt(h);
    return float2(along - half, along + half);
}

/// The spheres one ray marches at once: the K that enter first, of those still ahead of `from`.
/// Everything that enters before `cut` is in the list, so the march is exact up to there.
struct Gather
{
    float4 ball[K];
    uint id[K];
    float enter[K];
    uint count;
    float cut;          // the nearest entry of anything left out
    float leave;        // the farthest exit of anything kept
    float bound;        // the nearest plain sphere the ray hits: the melted surface is no further

    [mutating]
    void add(float4 b, uint who, float2 span, float3 origin, float3 dir, float from)
    {
        // A melt only adds to the spheres (smooth min ≤ min), so a plain hit bounds the surface.
        float2 plain = inBall(origin, dir, b.xyz, b.w);
        if (plain.y > from && plain.x <= plain.y)
            bound = min(bound, max(plain.x, from));
        if (span.x >= bound)
        {
            cut = min(cut, span.x);
            return;
        }
        if (count == K)
        {
            if (span.x >= enter[K - 1])
            {
                cut = min(cut, span.x);
                return;
            }
            cut = min(cut, enter[K - 1]);
            count--;
        }
        int at = int(count);
        for (; at > 0 && enter[at - 1] > span.x; at--)
        {
            ball[at] = ball[at - 1];
            id[at] = id[at - 1];
            enter[at] = enter[at - 1];
        }
        ball[at] = b;
        id[at] = who;
        enter[at] = span.x;
        count++;
        leave = max(leave, span.y);
    }

    /// How far entering at `t` can be and still matter: past the last kept when full.
    float horizon(float limit) { return min(bound, count == K ? min(limit, enter[K - 1]) : limit); }
};

groupshared float4 tileBalls[TILE_CAP];
groupshared uint tileIds[TILE_CAP];
groupshared float tileKeys[TILE_CAP];       // the nearest any ray from the eye can enter each, ascending
groupshared uint tileSlots[TILE_CAP];
groupshared uint tileCount;

/// Fills `g` with what the ray meets between `from` and `limit`: from the tile's list, or walking the BVH.
void gather(inout Gather g, float3 origin, float3 dir, float from, float limit, bool listed, inout uint visits)
{
    float blend = frame.look.x;
    g.count = 0;
    g.cut = limit;
    g.leave = from;
    g.bound = limit;
    if (listed)
    {
        // Sorted by nearest possible entry, so the scan stops once nothing left can come first.
        for (uint k = 0; k < tileCount; k++)
        {
            if (tileKeys[k] >= g.horizon(limit))
            {
                g.cut = min(g.cut, tileKeys[k]);
                break;
            }
            float4 b = tileBalls[k];
            float2 span = inBall(origin, dir, b.xyz, b.w + blend);
            visits++;
            if (span.y > from && span.x < limit && span.x <= span.y)
                g.add(b, tileIds[k], span, origin, dir, from);
        }
        return;
    }

    uint n = frame.counts.x;
    float3 inverse = 1.0 / dir;
    uint stack[STACK];
    uint top = 0;
    uint node = 0;
    while (true)
    {
        visits++;
        if (node >= n - 1)
        {
            uint place = nodes[node].left;
            float4 b = balls[MAX_N + place];
            float2 span = inBall(origin, dir, b.xyz, b.w + blend);
            if (span.y > from && span.x < limit && span.x <= span.y)
                g.add(b, vals[place], span, origin, dir, from);
        }
        else
        {
            uint l = nodes[node].left, r = nodes[node].right;
            float2 a = slab(origin, inverse, nodes[l].lo, nodes[l].hi);
            float2 b = slab(origin, inverse, nodes[r].lo, nodes[r].hi);
            float far = g.horizon(limit);
            bool useA = a.y >= max(a.x, from) && a.x < far;
            bool useB = b.y >= max(b.x, from) && b.x < far;
            // A box skipped only for entering past a full list still bounds the window.
            if (!useA && a.y >= max(a.x, from) && a.x < limit)
                g.cut = min(g.cut, a.x);
            if (!useB && b.y >= max(b.x, from) && b.x < limit)
                g.cut = min(g.cut, b.x);
            if (useA && useB)
            {
                // Nearer first, so the list fills with near spheres and prunes the far box.
                bool aFirst = a.x <= b.x;
                node = aFirst ? l : r;
                if (top < STACK)
                    stack[top++] = aFirst ? r : l;
                continue;
            }
            if (useA || useB)
            {
                node = useA ? l : r;
                continue;
            }
        }

        if (top == 0)
            break;
        node = stack[--top];
    }
}

/// The melted field over the gathered spheres at p: distance, normal and colour together.
struct Melted
{
    float distance;
    float3 normal;
    float3 tint;
};

/// Distance only, for stepping: the same smooth min without the normal and colour.
float distanceOf(Gather g, float3 p)
{
    float blend = max(frame.look.x, 1e-4);
    float d = length(p - g.ball[0].xyz) - g.ball[0].w;
    for (uint k = 1; k < g.count; k++)
    {
        float dk = length(p - g.ball[k].xyz) - g.ball[k].w;
        float h = saturate(0.5 + 0.5 * (dk - d) / blend);
        d = lerp(dk, d, h) - blend * h * (1.0 - h);
    }
    return d;
}

Melted field(Gather g, float3 p)
{
    float blend = max(frame.look.x, 1e-4);
    Melted m;
    m.distance = 1e5;
    m.normal = float3(0, 1, 0);
    m.tint = float3(1, 1, 1);
    for (uint k = 0; k < g.count; k++)
    {
        float3 to = p - g.ball[k].xyz;
        float far = max(length(to), 1e-6);
        float d = far - g.ball[k].w;
        float3 normal = to / far;
        float3 tint = hsv(frac(g.id[k] * 0.000137 + g.ball[k].y * 0.035 + 0.55), 0.62, 1.0);
        if (k == 0)
        {
            m.distance = d;
            m.normal = normal;
            m.tint = tint;
            continue;
        }
        float h = saturate(0.5 + 0.5 * (d - m.distance) / blend);
        m.normal = lerp(normal, m.normal, h);
        m.tint = lerp(tint, m.tint, h);
        m.distance = lerp(d, m.distance, h) - blend * h * (1.0 - h);
    }
    return m;
}

/// Soft shadow toward the sun from p: every sphere the ray passes near, from p's cell of the
/// light-space grid or walking the BVH, with Quilez's analytic penumbra; the pillars hard.
float shadow(float3 p, float3 toSun, inout uint visits)
{
    if (pillarShadow(p, toSun) == 0.0)
        return 0.0;
    float softness = frame.look.z;
    float lit = 1.0;

    if ((asuint(frame.sun.w) & FLAG_LIGHT_GRID) != 0)
    {
        // Every sphere that can shade p is listed in p's cell, seen from the sun.
        float3x3 basis = lightBasis();
        float2 at = float2(dot(p, basis[0]), dot(p, basis[1]));
        int2 cell = int2(floor((at + LIGHT_EXTENT) * (LIGHT / (2.0 * LIGHT_EXTENT))));
        if (any(cell < 0) || any(cell >= int(LIGHT)))
            return 1.0;
        uint c = uint(cell.y) * LIGHT + uint(cell.x);
        uint end = min(light[LIGHT_CELLS + c], LIGHT_ENTRIES);
        for (uint k = light[c]; k < end; k++)
        {
            visits++;
            lit = min(lit, sphereShadow(p, toSun, balls[light[2 * LIGHT_CELLS + k]], softness));
            if (lit < 0.01)
                return 0.0;
        }
        return lit;
    }

    float margin = penumbra();
    uint n = frame.counts.x;
    float3 inverse = 1.0 / toSun;
    uint stack[STACK];
    uint top = 0;
    uint node = 0;
    while (true)
    {
        visits++;
        float2 s = slab(p, inverse, nodes[node].lo - margin, nodes[node].hi + margin);
        if (s.y >= max(s.x, 0.0))
        {
            if (node >= n - 1)
            {
                lit = min(lit, sphereShadow(p, toSun, balls[MAX_N + nodes[node].left], softness));
                if (lit < 0.01)
                    return 0.0;
            }
            else
            {
                if (top < STACK)
                    stack[top++] = nodes[node].right;
                node = nodes[node].left;
                continue;
            }
        }
        if (top == 0)
            break;
        node = stack[--top];
    }
    return lit;
}

// ---- tiles -------------------------------------------------------------------

[shader("compute")]
[numthreads(8, 8, 1)]
void tiles(uint3 id: SV_DispatchThreadID)
{
    uint across = uint(frame.size.z), down = uint(frame.size.w);
    if (id.x >= across || id.y >= down)
        return;
    uint tile = id.y * across + id.x;
    float2 low = float2(id.xy * TILE), high = min(low + TILE, frame.size.xy);

    // The farthest raster depth in the tile: nothing behind it can show here.
    float far = 0.0;
    float3 centre = rayThrough((low + high) * 0.5, frame);
    for (uint y = uint(low.y); y < uint(high.y); y++)
    {
        for (uint x = uint(low.x); x < uint(high.x); x++)
        {
            float d = rasterDepth.SampleLevel((float2(x, y) + 0.5) / frame.size.xy, 0).r;
            float3 dir = rayThrough(float2(x, y) + 0.5, frame);
            far = max(far, distanceAt(d, dir, frame));
        }
    }

    // The tile's four side planes through the eye, normals inward.
    float3 a = rayThrough(float2(low.x, low.y), frame), b = rayThrough(float2(high.x, low.y), frame);
    float3 c = rayThrough(float2(high.x, high.y), frame), e = rayThrough(float2(low.x, high.y), frame);
    float3 planes[4] = { cross(a, b), cross(b, c), cross(c, e), cross(e, a) };
    for (uint k = 0; k < 4; k++)
    {
        planes[k] = normalize(planes[k]);
        if (dot(planes[k], centre) < 0)
            planes[k] = -planes[k];
    }

    float3 eye = frame.eye.xyz;
    uint n = frame.counts.x;
    uint stack[STACK];
    uint top = 0;
    uint node = 0;
    uint count = 0, visits = 0;
    while (true)
    {
        visits++;
        float3 lo = nodes[node].lo - eye, hi = nodes[node].hi - eye;
        bool inside = length(clamp(float3(0, 0, 0), lo, hi)) < far;
        for (uint k = 0; k < 4 && inside; k++)
        {
            // The box's corner furthest along the plane's normal.
            float3 p = select(planes[k] >= 0, hi, lo);
            inside = dot(planes[k], p) >= 0;
        }
        if (inside)
        {
            if (node >= n - 1)
            {
                // Past the cap the tile's pixels walk the BVH themselves: stop walking it here.
                if (count == TILE_CAP)
                {
                    count++;
                    break;
                }
                tileData[uint(frame.size.z * frame.size.w) * 2 + tile * TILE_CAP + count] = nodes[node].left;
                count++;
            }
            else
            {
                if (top < STACK)
                    stack[top++] = nodes[node].right;
                node = nodes[node].left;
                continue;
            }
        }
        if (top == 0)
            break;
        node = stack[--top];
    }

    tileData[tile * 2] = count > TILE_CAP ? OVERFLOW : count;
    tileData[tile * 2 + 1] = visits;
}

// ---- march -------------------------------------------------------------------

float3 sky(float3 dir, float3 toSun)
{
    float3 c = lerp(float3(0.09, 0.08, 0.16), float3(0.015, 0.02, 0.05), saturate(dir.y * 1.6 + 0.2));
    c += float3(1.0, 0.75, 0.5) * pow(saturate(dot(dir, toSun)), 300.0) * 6.0;
    c += float3(0.5, 0.35, 0.6) * pow(saturate(dot(dir, toSun)), 6.0) * 0.12;
    return c;
}

float3 tonemap(float3 c)
{
    c = c * (2.51 * c + 0.03) / (c * (2.43 * c + 0.59) + 0.14);
    return pow(saturate(c), 1.0 / 2.2);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void march(uint3 id: SV_DispatchThreadID, uint3 group: SV_GroupID, uint3 local: SV_GroupThreadID)
{
    uint across = uint(frame.size.z);
    uint tile = group.y * across + group.x;
    uint flags = asuint(frame.sun.w);
    uint head = tileData[tile * 2];
    bool listed = (flags & FLAG_TILES) != 0 && head != OVERFLOW;

    // The tile's spheres into groupshared memory, every thread a share, keyed by the
    // nearest any ray from the eye could enter them and bitonic sorted on that key.
    uint flat = local.y * TILE + local.x;
    if (flat == 0)
        tileCount = listed ? head : 0;
    GroupMemoryBarrierWithGroupSync();
    uint size = 1;
    while (size < tileCount)
        size <<= 1;
    for (uint k = flat; k < size; k += TILE * TILE)
    {
        if (k < tileCount)
        {
            uint place = tileData[uint(frame.size.z * frame.size.w) * 2 + tile * TILE_CAP + k];
            float4 b = balls[MAX_N + place];
            tileKeys[k] = length(b.xyz - frame.eye.xyz) - b.w - frame.look.x;
            tileSlots[k] = place;
        }
        else
            tileKeys[k] = 1e30;
    }
    GroupMemoryBarrierWithGroupSync();
    for (uint span = 2; span <= size; span <<= 1)
    {
        for (uint stride = span >> 1; stride > 0; stride >>= 1)
        {
            for (uint k = flat; k < size; k += TILE * TILE)
            {
                uint other = k ^ stride;
                if (other > k)
                {
                    bool ascending = (k & span) == 0;
                    if ((tileKeys[k] > tileKeys[other]) == ascending)
                    {
                        float key = tileKeys[k];
                        tileKeys[k] = tileKeys[other];
                        tileKeys[other] = key;
                        uint slot = tileSlots[k];
                        tileSlots[k] = tileSlots[other];
                        tileSlots[other] = slot;
                    }
                }
            }
            GroupMemoryBarrierWithGroupSync();
        }
    }
    for (uint k = flat; k < tileCount; k += TILE * TILE)
    {
        tileBalls[k] = balls[MAX_N + tileSlots[k]];
        tileIds[k] = vals[tileSlots[k]];
    }
    GroupMemoryBarrierWithGroupSync();

    if (id.x >= uint(frame.size.x) || id.y >= uint(frame.size.y))
        return;

    float2 pixel = float2(id.xy) + 0.5;
    float2 uv = pixel / frame.size.xy;
    float3 eye = frame.eye.xyz;
    float3 dir = rayThrough(pixel, frame);
    float3 toSun = frame.sun.xyz;
    float rawDepth = rasterDepth.SampleLevel(uv, 0).r;
    float limit = min(distanceAt(rawDepth, dir, frame), 200.0);
    float pixelAngle = 1.6 / (frame.size.y * frame.up.w * 0.5);

    Gather g = {};
    uint visits = 0, steps = 0, rounds = 0, shadowVisits = 0;
    bool found = false;
    float t = 0.0;
    Melted hit = {};
    float from = 0.0;
    for (; rounds < 4 && !found; rounds++)
    {
        gather(g, eye, dir, from, limit, listed, visits);
        if (g.count == 0)
            break;
        float end = min(min(g.cut, g.leave), limit);
        t = max(from, g.enter[0]);
        while (t < end && steps < 160)
        {
            steps++;
            float d = distanceOf(g, eye + dir * t);
            if (d < max(0.0004, t * pixelAngle))
            {
                found = true;
                hit = field(g, eye + dir * t);
                break;
            }
            t += d;
        }
        if (found || g.cut >= limit)
            break;
        from = g.cut;
    }

    float3 colour;
    float depth;
    bool shadows = (flags & FLAG_SHADOWS) != 0;
    if (found)
    {
        float3 p = eye + dir * t;
        float3 normal = normalize(hit.normal);
        float lit = shadows ? shadow(p + normal * 0.02, toSun, shadowVisits) : 1.0;
        float diffuse = saturate(dot(normal, toSun)) * lit;
        float3 half = normalize(toSun - dir);
        float spec = pow(saturate(dot(normal, half)), 60.0) * lit;
        float rim = pow(1.0 - saturate(-dot(normal, dir)), 4.0);
        float ambient = 0.18 + 0.12 * normal.y;
        colour = hit.tint * (float3(1.0, 0.92, 0.82) * 2.2 * diffuse + float3(0.35, 0.38, 0.62) * ambient)
               + float3(1.0, 0.95, 0.9) * spec * 1.5 + float3(0.6, 0.5, 1.0) * rim * 0.35;
        float4 clip = clipOf(p, frame);
        depth = clip.z / clip.w;
    }
    else if (rawDepth > 0.0)
    {
        // The raster surface, and the blobs' shadows on it: its point from the depth buffer.
        float3 p = eye + dir * limit;
        float3 base = rasterColour.SampleLevel(uv, 0).rgb;
        float lit = shadows ? shadow(p + float3(0, 0.02, 0) - dir * 0.02, toSun, shadowVisits) : 1.0;
        colour = base * (0.35 + 0.65 * lit);
        depth = rawDepth;
        t = limit;
    }
    else
    {
        colour = sky(dir, toSun);
        depth = 0.0;
        t = 200.0;
    }

    // Fog.
    colour = lerp(colour, sky(dir, toSun) * 0.8, saturate((t - 18.0) / 60.0));
    float3 shown = tonemap(colour);

    uint view = frame.counts.w;
    if (view == VIEW_STEPS)
        shown = heat(steps / 96.0);
    else if (view == VIEW_NODES)
        shown = heat(visits / (listed ? 256.0 : 192.0));
    else if (view == VIEW_TILES)
    {
        uint tileVisits = tileData[tile * 2 + 1];
        shown = head == OVERFLOW ? float3(0.9, 0.12, 0.2) : heat(head / float(TILE_CAP));
        shown = lerp(shown, float3(0.2, 0.9, 1.0), saturate(tileVisits / 2048.0) * 0.35);
        float2 cell = frac(pixel / TILE);
        if (min(cell.x, cell.y) < 1.0 / TILE)
            shown *= 0.6;
    }
    else if (view == VIEW_SHADOW)
        shown = heat(shadowVisits / 256.0);
    else if (view == VIEW_DEPTH)
        shown = float3(1, 1, 1) * pow(saturate(depth * 4.0), 0.5);
    else if (view == VIEW_NORMALS)
        shown = found ? normalize(hit.normal) * 0.5 + 0.5 : float3(0.05, 0.05, 0.07);
    else if (view == VIEW_ROUNDS)
        shown = rounds == 0 ? float3(0.05, 0.05, 0.07) : hsv(rounds / 4.0, 0.8, found ? 1.0 : 0.45);

    outColour[id.xy] = float4(shown, 1);
    outDepth[id.xy] = depth;
}

// ---- raster ------------------------------------------------------------------

[[vk::binding(0, 1)]] ConstantBuffer<Frame> frameV;
[[vk::binding(0, 2)]] Sampler2D marchedDepth;
[[vk::binding(0, 3)]] ConstantBuffer<Frame> frameF;

struct Lit
{
    float4 position : SV_Position;
    float3 world : WORLD;
    float3 normal : NORMAL;
    nointerpolation uint kind : KIND;   // 0 floor, 1 pillar
};

[shader("vertex")]
Lit floorVertex(uint vertex: SV_VertexID)
{
    static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };
    float2 c = corners[vertex] * 60.0;
    Lit o;
    o.world = float3(c.x, 0, c.y);
    o.normal = float3(0, 1, 0);
    o.kind = 0;
    o.position = clipOf(o.world, frameV);
    return o;
}

[shader("vertex")]
Lit pillarVertex(uint vertex: SV_VertexID, uint instance: SV_InstanceID)
{
    // 36 corners of a box: face, then two triangles of it.
    uint face = vertex / 6, corner = vertex % 6;
    static const uint2 quad[6] = { uint2(0, 0), uint2(1, 0), uint2(1, 1), uint2(0, 0), uint2(1, 1), uint2(0, 1) };
    float2 q = float2(quad[corner]) * 2.0 - 1.0;
    float3 axis = face < 2 ? float3(1, 0, 0) : face < 4 ? float3(0, 1, 0) : float3(0, 0, 1);
    float sign = (face & 1) == 0 ? 1.0 : -1.0;
    float3 u = face < 2 ? float3(0, 1, 0) : face < 4 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 v = cross(axis, u);
    float3 local = axis * sign + u * q.x + v * q.y * sign;
    float4 p = pillar(instance);
    float3 scale = float3(p.z, p.w * 0.5, p.z);
    Lit o;
    o.world = float3(p.x, p.w * 0.5, p.y) + local * scale;
    o.normal = axis * sign;
    o.kind = 1;
    o.position = clipOf(o.world, frameV);
    return o;
}

[shader("fragment")]
float4 litFragment(Lit i) : SV_Target
{
    float3 toSun = frameF.sun.xyz;
    float3 base;
    if (i.kind == 0)
    {
        float2 cell = i.world.xz;
        float2 grid = abs(frac(cell - 0.5) - 0.5) / fwidth(cell);
        float line = 1.0 - saturate(min(grid.x, grid.y));
        float checker = ((int(floor(cell.x * 0.5)) + int(floor(cell.y * 0.5))) & 1) == 0 ? 0.16 : 0.12;
        base = float3(checker, checker, checker * 1.25) + float3(0.25, 0.3, 0.55) * line * 0.5;
        base *= 1.0 - saturate(length(cell) / 55.0);
    }
    else
    {
        float band = frac(i.world.y * 0.5) < 0.08 ? 1.6 : 1.0;
        base = float3(0.55, 0.52, 0.6) * band;
    }
    float diffuse = saturate(dot(i.normal, toSun));
    float3 c = base * (float3(1.0, 0.92, 0.82) * 1.8 * diffuse + float3(0.35, 0.38, 0.62) * 0.45);
    return float4(c, 1);
}

struct Spark
{
    float4 position : SV_Position;
    float2 corner : CORNER;
    float3 tint : TINT;
};

[shader("vertex")]
Spark sparkVertex(uint vertex: SV_VertexID, uint instance: SV_InstanceID)
{
    static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };
    float t = frameV.eye.w * frameV.look.w;
    uint i = instance;
    // Sparks on tilted rings that thread through the galaxy and the pillars.
    float ring = 2.5 + 7.5 * frac(i * 0.7548776662);
    float a = frac(i * 0.6180339887) * 2.0 * PI + t * (0.6 + 1.2 / ring);
    float tilt = (frac(i * 0.5698402910) - 0.5) * 0.9;
    float3 p = float3(cos(a) * ring, 4.2 + sin(a) * ring * tilt + sin(t * 2.0 + i) * 0.15, sin(a) * ring);
    float size = 0.05 + 0.05 * frac(i * 0.3247);

    float2 c = corners[vertex];
    float3 world = p + (frameV.right.xyz * c.x + frameV.up.xyz * c.y) * size;
    Spark o;
    o.position = clipOf(world, frameV);
    o.corner = c;
    o.tint = hsv(frac(i * 0.0011 + 0.1), 0.55, 1.0);
    return o;
}

[shader("fragment")]
float4 sparkFragment(Spark i) : SV_Target
{
    float r = length(i.corner);
    float glow = saturate(1.0 - r);
    glow = glow * glow * (0.4 + 2.0 * pow(glow, 6.0));
    return float4(i.tint * glow, 0);
}

[shader("vertex")]
float4 fullVertex(uint vertex: SV_VertexID) : SV_Position
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    return float4(uv * 2.0 - 1.0, 0, 1);
}

struct DepthOut
{
    float4 colour : SV_Target;
    float depth : SV_Depth;
};

/// The march's depth back into the depth buffer, so raster drawn after it sees the blobs.
[shader("fragment")]
DepthOut writeDepth(float4 position: SV_Position)
{
    DepthOut o;
    o.colour = float4(0, 0, 0, 0);
    o.depth = marchedDepth.SampleLevel(position.xy / frameF.size.xy, 0).r;
    return o;
}
