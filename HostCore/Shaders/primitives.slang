// The host's primitive pipeline: everything plain on the canvas is one record
// in a storage buffer, and one draw pulls its corners from there by vertex id
// (six a record). Boxes, discs, rings and textured quads are shaped in the
// fragment stage by signed distance, so their edges are smooth at any turn or
// scale without being cut into triangles; free triangles carry their own corners.
//
// Depth orders it: each record has the depth of its place in the frame, so the
// opaque pre-pass (plain rects, drawn front to back with depth writes) hides
// what lies under them from everything drawn after.
//
// SDL_GPU's SPIR-V layout: vertex storage set 0, vertex uniforms set 1;
// fragment samplers set 2.

import xanadu.shape;

struct Primitive
{
    float4 a;       // box: centre xy, half axis x; triangle: corner 0, corner 1
    float4 b;       // box: half axis y, corner radius, stroke; triangle: corner 2, uv 2
    float4 uv;      // box: uv min, uv max; triangle: uv 0, uv 1
    float4 clip;    // pixels: min x, y, max x, y
    uint4 colours;  // colour 0 and 1 as half4 pairs
    uint4 more;     // colour 2 as a half4 pair; kind and flags; texture slot
    float4 extra;   // depth, then the CPU's own
};

static const uint KindRect = 0, KindDisc = 1, KindTexture = 2, KindTriangle = 3;
/// Culled or moved to the pre-pass by the expand stage: folded away to nothing.
static const uint KindSkip = 0xFF;
static const uint FlagVertical = 1 << 8;
/// Drawn exactly to its box, with no room for a smoothed edge: textures and the opaque pre-pass.
static const uint FlagExact = 1 << 9;
static const uint NoTexture = 0xFFFF;

struct Draw
{
    float2 screen;
    uint first;
    uint pad;
};

[[vk::binding(0, 0)]] StructuredBuffer<Primitive> primitives;
[[vk::binding(0, 1)]] ConstantBuffer<Draw> draw;

[[vk::binding(0, 2)]] Sampler2D texture0;
[[vk::binding(1, 2)]] Sampler2D texture1;
[[vk::binding(2, 2)]] Sampler2D texture2;
[[vk::binding(3, 2)]] Sampler2D texture3;
[[vk::binding(4, 2)]] Sampler2D texture4;
[[vk::binding(5, 2)]] Sampler2D texture5;
[[vk::binding(6, 2)]] Sampler2D texture6;
[[vk::binding(7, 2)]] Sampler2D texture7;

struct Varyings
{
    float4 position : SV_Position;
    float2 local : TEXCOORD0;                   // px from the box's centre along its own axes
    float2 uv : TEXCOORD1;
    float4 colour : TEXCOORD2;
    nointerpolation float4 shape : TEXCOORD3;   // half size x, y, corner radius, stroke
    nointerpolation uint2 kind : TEXCOORD4;     // kind and flags, texture slot
    nointerpolation float4 clip : TEXCOORD5;
};

float4 unpackColour(uint rg, uint ba)
{
    return float4(f16tof32(rg & 0xFFFF), f16tof32(rg >> 16), f16tof32(ba & 0xFFFF), f16tof32(ba >> 16));
}

static const float2 Corners[6] =
{
    float2(-1, -1), float2(1, -1), float2(1, 1),
    float2(-1, -1), float2(1, 1), float2(-1, 1)
};

[shader("vertex")]
Varyings primitiveVertex(uint vertex : SV_VertexID)
{
    Primitive p = primitives[draw.first + vertex / 6];
    uint corner = vertex % 6;
    uint kind = p.more.z & 0xFF;

    Varyings o;
    if (kind == KindSkip)
    {
        o = (Varyings)0;
        o.position = float4(2, 2, 2, 1);
        return o;
    }

    float2 pixel;
    if (kind == KindTriangle)
    {
        // Three corners and a degenerate rest, so every record is six vertices.
        uint at = corner < 3 ? corner : 0;
        pixel = at == 0 ? p.a.xy : at == 1 ? p.a.zw : p.b.xy;
        o.uv = at == 0 ? p.uv.xy : at == 1 ? p.uv.zw : p.b.zw;
        o.colour = at == 0 ? unpackColour(p.colours.x, p.colours.y)
            : at == 1 ? unpackColour(p.colours.z, p.colours.w)
            : unpackColour(p.more.x, p.more.y);
        o.local = 0;
        o.shape = 0;
    }
    else
    {
        float2 c = Corners[corner];
        float2 axisX = p.a.zw, axisY = p.b.xy;
        float2 half = float2(length(axisX), length(axisY));
        // Shaped kinds grow a pixel all round so their smoothed edge has room; a texture is exact.
        float pad = kind == KindTexture || (p.more.z & FlagExact) != 0 ? 0.0 : 1.0;
        float2 grown = half + pad;
        float2 dirX = axisX / max(half.x, 1e-6), dirY = axisY / max(half.y, 1e-6);
        pixel = p.a.xy + dirX * (c.x * grown.x) + dirY * (c.y * grown.y);
        o.local = c * grown;
        float2 t = saturate(c * grown / max(half, 1e-6) * 0.5 + 0.5);
        o.uv = lerp(p.uv.xy, p.uv.zw, t);
        // A two-colour box is a ramp along x, or along y when flagged.
        float along = (p.more.z & FlagVertical) != 0 ? t.y : t.x;
        o.colour = lerp(unpackColour(p.colours.x, p.colours.y), unpackColour(p.colours.z, p.colours.w), along);
        o.shape = float4(half, p.b.z, p.b.w);
    }

    float2 ndc = pixel / draw.screen * 2 - 1;
    o.position = float4(ndc.x, -ndc.y, p.extra.x, 1);
    o.kind = uint2(p.more.z, p.more.w);
    o.clip = p.clip;
    return o;
}

float4 sampleSlot(uint slot, float2 uv)
{
    switch (slot)
    {
    case 0: return texture0.Sample(uv);
    case 1: return texture1.Sample(uv);
    case 2: return texture2.Sample(uv);
    case 3: return texture3.Sample(uv);
    case 4: return texture4.Sample(uv);
    case 5: return texture5.Sample(uv);
    case 6: return texture6.Sample(uv);
    case 7: return texture7.Sample(uv);
    default: return float4(1);
    }
}

[shader("fragment")]
float4 primitiveShade(Varyings v) : SV_Target
{
    float2 at = v.position.xy;
    if (at.x < v.clip.x || at.y < v.clip.y || at.x >= v.clip.z || at.y >= v.clip.w)
        discard;

    uint kind = v.kind.x & 0xFF;
    float2 half = v.shape.xy;
    float radius = v.shape.z, stroke = v.shape.w;
    float cover = 1.0;
    if (kind == KindRect)
    {
        float d = Box(half * 2, radius).local(v.local);
        cover = stroke > 0 ? sdf.filled(d) - sdf.filled(d + stroke) : sdf.filled(d);
    }
    else if (kind == KindDisc)
    {
        // An ellipse measured as a circle in its own unit space, scaled back by its smaller side.
        float scale = min(half.x, half.y);
        float d = Circle(1.0).local(v.local / max(half, 1e-6)) * scale;
        cover = stroke > 0 ? sdf.filled(d) - sdf.filled(d + stroke) : sdf.filled(d);
    }

    float4 colour = v.colour;
    if (v.kind.y != NoTexture)
        colour *= sampleSlot(v.kind.y, v.uv);
    colour.a *= cover;
    if (colour.a <= 0.0)
        discard;
    return colour;
}

/// The opaque pre-pass: pixel-aligned plain rects, clipped on the CPU, so no
/// discard stands between them and early depth rejection.
[shader("fragment")]
float4 primitiveOpaque(Varyings v) : SV_Target
{
    return float4(v.colour.rgb, 1.0);
}
