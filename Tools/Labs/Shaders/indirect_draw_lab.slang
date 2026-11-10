// Xanadu Labs' indirect draw lab. A field of shapes that only the GPU knows
// about: each one is made from a hash of its index (no buffer of shapes, no
// upload), drifts on its own orbit, and is culled against a frame on the GPU.
//
//   1. reset:  one thread zeroes the draw command's instance count.
//   2. cull:   a thread per shape; survivors take a slot with an atomic add
//              and write their index into the visible list. The atomic's
//              total *is* the draw command's instance count.
//   3. draw:   DrawPrimitivesIndirect reads that command. Each instance looks
//              up its shape through the visible list.
//   4. readout: a full-stage pass prints the count the GPU drew, read straight
//              from the same buffer, so the CPU never has to ask for it.
//
// SDL_GPU's SPIR-V layout: compute reads storage buffers from set 0, writes them
// in set 1, uniforms in set 2; vertex storage set 0, uniforms set 1; fragment
// storage set 2, uniforms set 3.

struct Frame
{
    float4 clock;   // time (s), shape count, world units per pixel, mode (1 indirect, 0 direct)
    float4 camera;  // centre x, y (world), stage width, height (px)
    float4 cull;    // the frame shapes must touch: min x, min y, max x, max y (stage px)
    float4 look;    // shape size ×, orbit ×, unused, unused
};

static const float2 World = float2(12000, 8000);

/// The draw command, as SDL_GPU reads it: vertices, instances, first vertex, first instance.
[[vk::binding(0, 1)]] RWStructuredBuffer<uint> args;
[[vk::binding(1, 1)]] RWStructuredBuffer<uint> visibleOut;
[[vk::binding(0, 2)]] ConstantBuffer<Frame> computeFrame;

[[vk::binding(0, 0)]] StructuredBuffer<uint> visible;
[[vk::binding(0, 1)]] ConstantBuffer<Frame> vertexFrame;

[[vk::binding(0, 2)]] StructuredBuffer<uint> argsIn;
[[vk::binding(0, 3)]] ConstantBuffer<Frame> fragmentFrame;

// ---- the shapes: all from the index -------------------------------------------

uint pcg(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float random(uint index, uint salt)
{
    return float(pcg(index ^ pcg(salt))) / 4294967295.0;
}

struct Shape
{
    float2 centre;  // stage pixels
    float radius;   // stage pixels
    float3 colour;
    float kind;     // 0 disc, 1 ring, 2 diamond
};

float3 hue(float h)
{
    return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
}

Shape place(uint index, Frame frame)
{
    float time = frame.clock.x;
    float2 home = float2(random(index, 1), random(index, 2)) * World;
    float orbit = lerp(20, 160, random(index, 3)) * frame.look.y;
    float speed = lerp(-1.5, 1.5, random(index, 4));
    float angle = random(index, 5) * 6.2831853 + time * speed;
    float2 world = home + orbit * float2(cos(angle), sin(angle));

    float zoom = frame.clock.z;
    Shape s;
    s.centre = (world - frame.camera.xy) / zoom + frame.camera.zw * 0.5;
    s.radius = lerp(4, 18, pow(random(index, 6), 3)) * frame.look.x / zoom;
    // Hues drift across the world, so panning shows where you are.
    s.colour = hue(home.x / World.x * 0.7 + home.y / World.y * 0.3 + random(index, 7) * 0.08);
    s.kind = floor(random(index, 8) * 3);
    return s;
}

/// Whether a shape touches the cull frame at all.
bool inside(Shape s, Frame frame)
{
    float4 c = frame.cull;
    float reach = s.radius + 1.5;
    return s.centre.x + reach >= c.x && s.centre.x - reach <= c.z
        && s.centre.y + reach >= c.y && s.centre.y - reach <= c.w;
}

// ---- compute -------------------------------------------------------------------

[shader("compute")]
[numthreads(1, 1, 1)]
void reset()
{
    args[0] = 6;
    args[1] = 0;
    args[2] = 0;
    args[3] = 0;
}

[shader("compute")]
[numthreads(64, 1, 1)]
void cull(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x;
    if (index >= uint(computeFrame.clock.y))
        return;
    if (!inside(place(index, computeFrame), computeFrame))
        return;

    // The slot this shape gets is the count so far; the final count is the draw's instances.
    uint slot;
    InterlockedAdd(args[1], 1u, slot);
    visibleOut[slot] = index;
}

// ---- draw ------------------------------------------------------------------------

struct Varyings
{
    float4 position : SV_Position;
    float2 local : TEXCOORD0;                  // -1..1 across the quad, plus a margin
    nointerpolation float4 style : TEXCOORD1;  // rgb, kind
    nointerpolation float radius : TEXCOORD2;  // stage pixels
};

static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

[shader("vertex")]
Varyings shapeVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Frame frame = vertexFrame;
    bool indirect = frame.clock.w > 0.5;
    // Indirect: the instance is a slot in the visible list. Direct: it is the shape itself,
    // drawn whether or not it survives, so the test happens here instead.
    uint index = indirect ? visible[instance] : instance;
    Shape s = place(index, frame);

    Varyings o = (Varyings)0;
    if (!indirect && !inside(s, frame))
    {
        o.position = float4(2, 2, 0, 1); // off the target: clipped, after the work was done
        return o;
    }

    float extent = s.radius + 1.5;
    float2 corner = Corners[vertex];
    float2 pixel = s.centre + corner * extent;
    float2 ndc = pixel / frame.camera.zw * 2 - 1;
    o.position = float4(ndc.x, -ndc.y, 0, 1);
    o.local = corner * extent;
    o.style = float4(s.colour, s.kind);
    o.radius = s.radius;
    return o;
}

/// Additive (blend One, One): the compacted list comes out in whatever order the
/// atomics ran, so anything order-dependent would flicker.
[shader("fragment")]
float4 shapeShade(Varyings v) : SV_Target
{
    float r = v.radius;
    float d;
    switch (int(v.style.w))
    {
    case 1: d = abs(length(v.local) - r * 0.75) - r * 0.18; break;  // ring
    case 2: d = (abs(v.local.x) + abs(v.local.y)) * 0.7071 - r * 0.75; break; // diamond
    default: d = length(v.local) - r; break;                          // disc
    }
    float alpha = saturate(0.5 - d) * 0.8;
    return float4(v.style.rgb * alpha, alpha);
}

// ---- readout ---------------------------------------------------------------------

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

/// 3×5 pixel digits, top row in the high bits.
static const uint Digits[10] = { 0x7B6F, 0x2C97, 0x73E7, 0x73CF, 0x5BC9, 0x79CF, 0x79EF, 0x7249, 0x7BEF, 0x7BCF };

/// Whether p (stage px) is on a lit cell of `value` printed right-aligned to `right`, cells `cell` px square.
bool number(float2 p, float2 right, float cell, uint value)
{
    float2 q = (p - right) / cell;
    if (q.y < 0 || q.y >= 5 || q.x >= 0)
        return false;
    int column = int(floor(-q.x));        // cells left of the right edge
    int position = column / 4;            // digit position from the right; a blank column between
    int x = 2 - (column % 4);
    if (x < 0 || position > 8)
        return false;
    uint power = 1;
    for (int i = 0; i < position; i++)
        power *= 10;
    if (position > 0 && value < power)
        return false;                     // no leading zeros
    uint digit = (value / power) % 10;
    uint bit = 14 - (uint(floor(q.y)) * 3 + uint(x));
    return ((Digits[digit] >> bit) & 1) != 0;
}

/// The cull frame's outline, and the GPU's own count of what it drew.
[shader("fragment")]
float4 readout(FullVaryings v) : SV_Target
{
    Frame frame = fragmentFrame;
    float2 p = v.uv * frame.camera.zw;
    float4 c = frame.cull;
    float4 result = 0;

    float2 inner = max(c.xy - p, p - c.zw);
    float edge = abs(max(inner.x, inner.y));
    if (edge < 1)
        result = float4(1, 0.8, 0.3, 1);

    // Top left: shapes drawn, as the GPU counted them; under it, a bar of drawn / total.
    bool indirect = frame.clock.w > 0.5;
    uint total = uint(frame.clock.y);
    uint drawn = indirect ? argsIn[1] : total;
    float2 corner = float2(24, 20);
    float2 box = p - corner;
    if (all(box >= -10) && box.x < 330 && box.y < 72)
        result = float4(0, 0, 0, 0.6);
    if (number(p, corner + float2(300, 0), 6, drawn))
        result = float4(1, 1, 1, 1);
    if (box.y >= 44 && box.y < 54 && box.x >= 0 && box.x < 300)
        result = box.x < 300 * float(drawn) / max(float(total), 1) ? float4(0.35, 0.65, 1, 1) : float4(0.15, 0.17, 0.22, 1);

    return result;
}
