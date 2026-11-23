// The canvas's compute stage: places every visible recording's primitives for
// the frame. Recordings live in a pool in their own space and only go up when
// they change; the CPU sends one instance a recording drawn (its transform,
// tint, clip, depth and where its primitives go), and this writes each
// primitive in pixels, in order, ready for primitives.slang to draw.
//
// One workgroup an instance. A primitive outside its clip becomes a skip (the
// vertex stage folds it away) so order never needs a scan. Plain opaque rects
// leave the ordered list for the pre-pass list instead. That list has a slot
// per placed primitive, holding its opaque cut or a skip, and is drawn whole:
// an atomic count feeding an indirect draw raced this pass and left the
// pre-pass empty on most frames.
//
// SDL_GPU's compute layout: read-only storage set 0, read-write set 1, uniforms set 2.

struct Primitive
{
    float4 a;
    float4 b;
    float4 uv;
    float4 clip;
    uint4 colours;
    uint4 more;     // colour 2; kind and flags; texture slot (the recording's own in the pool)
    float4 extra;   // depth
};

struct Instance
{
    float4 basis;   // x axis, y axis
    float4 origin;  // origin xy, scale (for radii and strokes), unused
    float4 tint;
    float4 clip;    // pixels: min x, y, max x, y
    uint4 range;    // first in the pool, count, first out, first texture slot
    uint4 more;     // flags, first pre-pass slot, unused, depth base
};

struct Expand
{
    uint instances;
    float depthStep;
    uint pad0, pad1;
};

static const uint KindRect = 0, KindTexture = 2, KindTriangle = 3, KindSkip = 0xFF;
static const uint FlagExact = 1 << 9;
static const uint NoTexture = 0xFFFF;
static const uint AllowOpaque = 1;

[[vk::binding(0, 0)]] StructuredBuffer<Primitive> pool;
[[vk::binding(1, 0)]] StructuredBuffer<Instance> instances;
[[vk::binding(2, 0)]] StructuredBuffer<uint> slots;
[[vk::binding(0, 1)]] RWStructuredBuffer<Primitive> placed;
[[vk::binding(1, 1)]] RWStructuredBuffer<Primitive> opaque;
[[vk::binding(0, 2)]] ConstantBuffer<Expand> expand;

float4 unpackColour(uint rg, uint ba)
{
    return float4(f16tof32(rg & 0xFFFF), f16tof32(rg >> 16), f16tof32(ba & 0xFFFF), f16tof32(ba >> 16));
}

uint2 packColour(float4 c)
{
    return uint2(f32tof16(c.r) | (f32tof16(c.g) << 16), f32tof16(c.b) | (f32tof16(c.a) << 16));
}

float2 place(Instance at, float2 p)
{
    return at.origin.xy + at.basis.xy * p.x + at.basis.zw * p.y;
}

float2 turn(Instance at, float2 v)
{
    return at.basis.xy * v.x + at.basis.zw * v.y;
}

bool whole(float v) { return abs(v - round(v)) < 1e-3; }

[shader("compute")]
[numthreads(64, 1, 1)]
void expandPrimitives(uint3 group : SV_GroupID, uint3 thread : SV_GroupThreadID)
{
    if (group.x >= expand.instances)
        return;

    Instance at = instances[group.x];
    uint count = at.range.y;
    for (uint i = thread.x; i < count; i += 64)
    {
        Primitive p = pool[at.range.x + i];
        uint kind = p.more.z & 0xFF;
        float2 lo, hi;
        if (kind == KindTriangle)
        {
            float2 a = place(at, p.a.xy), b = place(at, p.a.zw), c = place(at, p.b.xy);
            p.a = float4(a, b);
            p.b = float4(c, p.b.zw);
            lo = min(a, min(b, c));
            hi = max(a, max(b, c));
        }
        else
        {
            float2 centre = place(at, p.a.xy);
            float2 x = turn(at, p.a.zw), y = turn(at, p.b.xy);
            p.a = float4(centre, x);
            p.b = float4(y, p.b.zw * at.origin.z);
            float2 reach = abs(x) + abs(y);
            lo = centre - reach;
            hi = centre + reach;
        }

        p.colours.xy = packColour(unpackColour(p.colours.x, p.colours.y) * at.tint);
        p.colours.zw = packColour(unpackColour(p.colours.z, p.colours.w) * at.tint);
        p.more.xy = packColour(unpackColour(p.more.x, p.more.y) * at.tint);
        p.clip = at.clip;
        p.extra = float4(1.0 - float(at.more.w + i + 1) * expand.depthStep, 0, 0, 0);
        uint local = p.more.w;
        p.more.w = local == NoTexture ? NoTexture : slots[at.range.w + local];

        uint out = at.range.z + i;
        // A pixel of slack for a smoothed edge.
        bool outside = hi.x + 1 <= at.clip.x || hi.y + 1 <= at.clip.y || lo.x - 1 >= at.clip.z || lo.y - 1 >= at.clip.w;
        float4 c0 = unpackColour(p.colours.x, p.colours.y);
        bool solid = kind == KindRect && local == NoTexture && p.b.z == 0 && p.b.w == 0
            && p.a.w == 0 && p.b.x == 0 && c0.a >= 1.0 && all(p.colours.xy == p.colours.zw)
            && whole(lo.x) && whole(lo.y) && whole(hi.x) && whole(hi.y);

        Primitive cut = p;
        cut.more.z = KindSkip;
        if (!outside && solid && (at.more.x & AllowOpaque) != 0)
        {
            // Cut to the clip here, so the pre-pass never needs to discard.
            float2 cutLo = max(lo, at.clip.xy), cutHi = min(hi, at.clip.zw);
            if (all(cutHi > cutLo))
            {
                cut.a = float4((cutLo + cutHi) * 0.5, (cutHi.x - cutLo.x) * 0.5, 0);
                cut.b = float4(0, (cutHi.y - cutLo.y) * 0.5, 0, 0);
                cut.more.z = KindRect | FlagExact;
            }

            outside = true;
        }

        opaque[at.more.y + i] = cut;
        if (outside)
            p.more.z = KindSkip;
        placed[out] = p;
    }
}
