// Xanadu Labs' pointer history lab. The pointer's path lives on the GPU as a
// ring of keys (x, y, time, unused): the CPU stores a key only when a straight
// line from the last one would miss a frame by more than the tolerance, so a
// still pointer costs nothing and a fast curve gets keys where it bends. The
// newest frame rides along as a uniform (the head), so the path always reaches
// the pointer now.
//
// Everything that reads the path does it here: a one-thread compute pass finds
// the ghost (binary search by time) and the smoothed follower, and steps a chain
// of chasers whose state stays in a GPU buffer; instanced quads draw the trail
// and the keys; a full-stage pass draws the markers.
//
// SDL_GPU's SPIR-V layout: vertex storage buffers in set 0, uniforms in set 1;
// fragment storage buffers in set 2, uniforms in set 3; compute reads storage
// buffers from set 0, writes them in set 1, uniforms in set 2.

static const uint RingSize = 1024;
static const uint MaxChain = 48;
/// Pieces each trail segment is cut into, so curves draw as curves.
static const uint Pieces = 8;

// Slots in the state buffer, one float4 each: then the chain (position, velocity).
static const uint GhostSlot = 0;
static const uint SmoothSlot = 1;
static const uint ChainStart = 2;

struct Sim
{
    float4 head;   // pointer now: x, y, time, unused
    float4 ring;   // newest key's slot, keys stored, delta (s), curves (1) or lines (0)
    float4 chase;  // chase rate (/s), spring (0 = plain lerp), ghost delay (s), smoothing half-life (s)
    float4 chain;  // links, link gap (px), reset (1 on the first frame), trail (s)
};

struct Look
{
    float4 head;   // as Sim
    float4 ring;   // newest key's slot, keys stored, unused, curves
    float4 view;   // trail (s), links, stage width, stage height
};

[[vk::binding(0, 0)]] StructuredBuffer<float4> computeKeys;
[[vk::binding(0, 1)]] RWStructuredBuffer<float4> state;
/// Indirect draw commands the compute pass sizes (vertices, instances, first
/// vertex, first instance): [0] the trail's pieces, [1] the key dots.
[[vk::binding(1, 1)]] RWStructuredBuffer<uint4> draws;
[[vk::binding(0, 2)]] ConstantBuffer<Sim> sim;

[[vk::binding(0, 0)]] StructuredBuffer<float4> vertexKeys;
[[vk::binding(0, 1)]] ConstantBuffer<Look> lookVertex;

[[vk::binding(0, 2)]] StructuredBuffer<float4> fragmentKeys;
[[vk::binding(1, 2)]] StructuredBuffer<float4> stateIn;
[[vk::binding(0, 3)]] ConstantBuffer<Look> look;

// ---- the path ---------------------------------------------------------------

/// The path as points, newest first: point 0 is the head (the pointer now),
/// point k ≥ 1 is the k-th newest key. Times fall as k rises.
struct Track
{
    float4 head;
    uint newest;
    uint stored;
    float curves;

    uint count() { return stored + 1; }

    float4 point(StructuredBuffer<float4> keys, uint k)
    {
        if (k == 0)
            return head;
        return keys[(newest + RingSize - (k - 1)) % RingSize];
    }

    /// Velocity through point k: its neighbours' gap over their time apart
    /// (Catmull-Rom with real times), so a long still stretch next to it pulls
    /// the tangent towards zero instead of overshooting into the stillness.
    float2 tangent(StructuredBuffer<float4> keys, uint k)
    {
        uint n = count();
        float4 later = point(keys, k > 0 ? k - 1 : 0);
        float4 earlier = point(keys, min(k + 1, n - 1));
        return (later.xy - earlier.xy) / max(later.z - earlier.z, 1e-5);
    }

    /// The path at u (0..1) along the segment from point k+1 (earlier) to point k.
    float2 along(StructuredBuffer<float4> keys, uint k, float u)
    {
        float4 a = point(keys, k + 1);
        float4 b = point(keys, k);
        if (curves < 0.5)
            return lerp(a.xy, b.xy, u);

        // Cubic Hermite through both ends with time-aware tangents.
        float dt = max(b.z - a.z, 1e-5);
        float u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * a.xy + (u3 - 2 * u2 + u) * dt * tangent(keys, k + 1)
             + (-2 * u3 + 3 * u2) * b.xy + (u3 - u2) * dt * tangent(keys, k);
    }

    /// Where the pointer was at `time`: a binary search for the two points
    /// either side, then the path between them.
    float2 at(StructuredBuffer<float4> keys, float time)
    {
        uint n = count();
        if (n == 1 || time >= head.z)
            return head.xy;
        float4 oldest = point(keys, n - 1);
        if (time <= oldest.z)
            return oldest.xy;

        // The newest point at or before `time`: the predicate flips once as k rises.
        uint lo = 1, hi = n - 1;
        while (lo < hi)
        {
            uint mid = (lo + hi) / 2;
            if (point(keys, mid).z <= time)
                hi = mid;
            else
                lo = mid + 1;
        }

        float4 a = point(keys, lo), b = point(keys, lo - 1);
        return along(keys, lo - 1, saturate((time - a.z) / max(b.z - a.z, 1e-5)));
    }
};

Track track(float4 head, float4 ring)
{
    Track t;
    t.head = head;
    t.newest = uint(ring.x);
    t.stored = uint(ring.y);
    t.curves = ring.w;
    return t;
}

// ---- compute: everything that moves -----------------------------------------

[shader("compute")]
[numthreads(1, 1, 1)]
void simulate()
{
    Track path = track(sim.head, sim.ring);
    float now = sim.head.z;
    float dt = sim.ring.z;

    state[GhostSlot] = float4(path.at(computeKeys, now - sim.chase.z), 0, 0);

    // Indirect draws: how much of the path is inside the trail, found by a
    // binary search for the first point older than it. Points are newest
    // first, so the instances worth drawing are exactly the first ones.
    uint n = path.count();
    float cutoff = now - sim.chain.w;
    uint lo = 0, hi = n;
    while (lo < hi)
    {
        uint mid = (lo + hi) / 2;
        if (path.point(computeKeys, mid).z < cutoff)
            hi = mid;
        else
            lo = mid + 1;
    }
    // Points 0..lo-1 are inside. Segment k joins points k+1 and k, so it shows
    // while point k does (the older end fades out in the vertex shader).
    uint segments = min(lo, n - 1);
    uint keys = lo > 0 ? lo - 1 : 0;
    draws[0] = uint4(6, segments * Pieces, 0, 0);
    draws[1] = uint4(6, keys, 0, 0);

    // An exponential moving average of the path itself, not of its samples
    // (keys are uneven, so weighting keys would be wrong): the path read at
    // even steps back in time, each step weighted half as much per half-life.
    float halfLife = max(sim.chase.w, 1e-3);
    const uint steps = 128;
    float stride = halfLife * 8 / steps;
    float2 sum = 0;
    float total = 0;
    for (uint i = 0; i < steps; i++)
    {
        float w = exp2(-(i * stride) / halfLife);
        sum += path.at(computeKeys, now - i * stride) * w;
        total += w;
    }
    state[SmoothSlot] = float4(sum / total, 0, 0);

    uint links = min(uint(sim.chain.x), MaxChain);
    bool reset = sim.chain.z > 0.5;
    float rate = sim.chase.x;
    float spring = sim.chase.y;
    float spacing = sim.chain.y;

    // One thread walks the chain head to tail, so each link chases where the
    // one in front of it is this frame: a rope, not a jelly.
    float2 target = path.head.xy;
    for (uint i = 0; i < links; i++)
    {
        float4 link = reset ? float4(target, 0, 0) : state[ChainStart + i];
        float2 position = link.xy;
        float2 velocity = link.zw;

        if (spring <= 0)
        {
            // Frame-rate independent lerp: the same feel at 30 or 240 fps.
            position = lerp(position, target, 1 - exp(-rate * dt));
            velocity = 0;
        }
        else
        {
            // A damped spring: stiffness from the rate, less damping as spring rises.
            float damping = 2 * rate * (1 - 0.9 * saturate(spring));
            velocity += (target - position) * rate * rate * dt;
            velocity *= exp(-damping * dt);
            position += velocity * dt;
        }

        if (i > 0 && spacing > 0)
        {
            float2 gap = position - target;
            float distance = max(length(gap), 1e-4);
            if (distance > spacing)
                position = target + gap / distance * spacing;
        }

        state[ChainStart + i] = float4(position, velocity);
        target = position;
    }
}

// ---- trail and keys: instanced quads -----------------------------------------

struct StrokeVaryings
{
    float4 position : SV_Position;
    float2 pixel : TEXCOORD0;
    nointerpolation float4 ends : TEXCOORD1;   // the piece's two ends, stage pixels
    nointerpolation float4 style : TEXCOORD2;  // fade, half-width, kind (0 trail, 1 key), unused
};

static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };

StrokeVaryings stroke(float2 a, float2 b, float halfWidth, float fade, float kind, uint vertex)
{
    float2 size = lookVertex.view.zw;
    float extent = halfWidth + 1.5;
    float2 span = b - a;
    float2 along = dot(span, span) > 1e-8 ? normalize(span) : float2(1, 0);
    float2 across = float2(-along.y, along.x);
    float2 corner = Corners[vertex];
    float2 pixel = lerp(a - along * extent, b + along * extent, corner.x) + across * extent * (corner.y * 2 - 1);

    StrokeVaryings o;
    float2 ndc = pixel / size * 2 - 1;
    o.position = float4(ndc.x, -ndc.y, 0, 1);
    o.pixel = pixel;
    o.ends = float4(a, b);
    o.style = float4(fade, halfWidth, kind, 0);
    return o;
}

StrokeVaryings nothing()
{
    StrokeVaryings o = (StrokeVaryings)0;
    o.position = float4(2, 2, 0, 1); // off the target: clipped
    return o;
}

/// One piece of one segment: instance / Pieces picks the segment, the rest the piece.
[shader("vertex")]
StrokeVaryings trailVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Track path = track(lookVertex.head, lookVertex.ring);
    uint k = instance / Pieces;
    if (k + 1 >= path.count())
        return nothing();

    float now = path.head.z, trail = max(lookVertex.view.x, 1e-3);
    float4 earlier = path.point(vertexKeys, k + 1), later = path.point(vertexKeys, k);
    float u0 = float(instance % Pieces) / Pieces, u1 = u0 + 1.0 / Pieces;
    float age = (now - lerp(earlier.z, later.z, u0)) / trail;
    if (age > 1)
        return nothing();

    return stroke(path.along(vertexKeys, k, u0), path.along(vertexKeys, k, u1), 1.5, 1 - age, 0, vertex);
}

/// A dot on every stored key: the gaps between them are the compression.
[shader("vertex")]
StrokeVaryings keyVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Track path = track(lookVertex.head, lookVertex.ring);
    uint k = instance + 1;
    if (k >= path.count())
        return nothing();

    float4 key = path.point(vertexKeys, k);
    float age = (path.head.z - key.z) / max(lookVertex.view.x, 1e-3);
    if (age > 1)
        return nothing();
    return stroke(key.xy, key.xy, 3.5, 1 - age, 1, vertex);
}

float segment(float2 p, float2 a, float2 b)
{
    float2 pa = p - a, ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    return length(pa - ba * h);
}

/// Premultiplied: blend One, OneMinusSrcAlpha.
[shader("fragment")]
float4 strokeShade(StrokeVaryings v) : SV_Target
{
    float d = segment(v.pixel, v.ends.xy, v.ends.zw) - v.style.y;
    float alpha = saturate(0.5 - d) * v.style.x;
    float3 colour = v.style.z > 0.5 ? float3(1.0, 0.95, 0.85) : float3(0.4, 0.55, 0.95);
    return float4(colour * alpha, alpha);
}

// ---- full-stage passes ---------------------------------------------------------

struct FullVaryings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
FullVaryings full(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    FullVaryings o;
    o.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    o.uv = uv;
    return o;
}

/// The backdrop: a faint grid. Opaque.
[shader("fragment")]
float4 back(FullVaryings v) : SV_Target
{
    float2 p = v.uv * look.view.zw;
    float2 grid = abs(frac(p / 40 + 0.5) - 0.5) * 40;
    return float4(float3(0.035, 0.04, 0.07) + 0.03 * step(min(grid.x, grid.y), 0.75), 1);
}

float3 hue(float h)
{
    return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
}

/// Premultiplied "over".
void over(inout float4 into, float3 colour, float alpha)
{
    into = float4(colour * alpha, alpha) + into * (1 - alpha);
}

/// The chain, the ghost, the smoothed follower and the pointer, over the trail.
[shader("fragment")]
float4 markers(FullVaryings v) : SV_Target
{
    float2 p = v.uv * look.view.zw;
    float now = look.head.z;
    float4 result = 0;

    uint links = min(uint(look.view.y), MaxChain);
    for (uint i = links; i-- > 0;)
    {
        float t = links > 1 ? float(i) / float(links - 1) : 0;
        float d = length(p - stateIn[ChainStart + i].xy) - lerp(16, 4, t);
        float3 tint = hue(0.55 + t * 0.45 + now * 0.05);
        result.rgb += tint * 0.2 * exp(-max(d, 0) * 0.12); // glow: added light, no cover
        over(result, tint, saturate(0.5 - d));
    }

    float ring = abs(length(p - stateIn[GhostSlot].xy) - 22) - 2;
    over(result, float3(1.0, 0.8, 0.3), saturate(0.5 - ring));

    float2 q = abs(p - stateIn[SmoothSlot].xy);
    over(result, float3(0.4, 1.0, 0.6), saturate(2.0 - abs(q.x + q.y - 18)));

    float2 cross = abs(p - look.head.xy);
    float pointer = min(max(cross.x, cross.y - 10), max(cross.y, cross.x - 10)) - 1;
    over(result, float3(1, 1, 1), saturate(0.5 - pointer));

    return result;
}
