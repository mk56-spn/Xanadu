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

static const uint KindRect = 0, KindDisc = 1, KindTexture = 2, KindTriangle = 3, KindHalo = 4;
/// Culled or moved to the pre-pass by the expand stage: folded away to nothing.
static const uint KindSkip = 0xFF;
static const uint FlagVertical = 1 << 8;
/// Drawn exactly to its box, with no room for a smoothed edge: textures and the opaque pre-pass.
static const uint FlagExact = 1 << 9;
/// A triangle whose corners are a quadratic Bézier's start, control and end,
/// trimmed to the curve here (Loop–Blinn: u² − v); the CPU never samples it.
static const uint FlagCurve = 1 << 10;
/// With FlagCurve: keep the side towards the control, not towards the chord.
static const uint FlagConcave = 1 << 11;
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
    nointerpolation float4 cell : TEXCOORD6;    // a halo's texture region: uv min, uv max
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
        // A curve triangle's canonical coordinates ride in local: start (0, 0),
        // control (½, 0), end (1, 1), so the curve is where u² = v.
        o.local = at == 0 ? float2(0, 0) : at == 1 ? float2(0.5, 0) : float2(1, 1);
        o.shape = 0;
        o.cell = 0;
    }
    else
    {
        float2 c = Corners[corner];
        float2 axisX = p.a.zw, axisY = p.b.xy;
        float2 half = float2(length(axisX), length(axisY));
        // Shaped kinds grow a pixel all round so their smoothed edge has room; a texture is exact.
        float pad = kind == KindTexture || kind == KindHalo || (p.more.z & FlagExact) != 0 ? 0.0 : 1.0;
        float2 grown = half + pad;
        float2 dirX = axisX / max(half.x, 1e-6), dirY = axisY / max(half.y, 1e-6);
        pixel = p.a.xy + dirX * (c.x * grown.x) + dirY * (c.y * grown.y);
        o.local = c * grown;
        float2 t = saturate(c * grown / max(half, 1e-6) * 0.5 + 0.5);
        o.uv = lerp(p.uv.xy, p.uv.zw, t);
        o.cell = 0;
        if (kind == KindHalo)
        {
            // The uv runs past the region over the pad; the region is kept to clamp the blur's reads.
            float2 inner = max(half - p.b.z, 1e-6);
            o.uv = lerp(p.uv.xy, p.uv.zw, c * half / inner * 0.5 + 0.5);
            o.cell = p.uv;
        }
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

/// A halo: the region's alpha under a gaussian reaching three σ each way
/// (reads outside the region are clear), times the intensity, clamped.
float4 shadeHalo(Varyings v)
{
    float sigma = max(v.shape.w, 0.5);
    float pad = v.shape.z;
    float2 inner = max(v.shape.xy - pad, 1e-6);
    float2 uvPerPixel = (v.cell.zw - v.cell.xy) / (inner * 2.0);
    // Taps at most two pixels apart, so the bilinear reads between them leave
    // no gaps (wider spacing shows as a grid on thin strokes), out to three σ.
    float spacing = min(0.6 * sigma, 2.0);
    int reach = min((int)ceil(3.0 * sigma / spacing), 12);
    float sum = 0.0, weights = 0.0;
    for (int y = -reach; y <= reach; y++)
    {
        for (int x = -reach; x <= reach; x++)
        {
            float2 offset = float2(x, y) * spacing;
            float w = exp(-0.5 * dot(offset, offset) / (sigma * sigma));
            float2 uv = v.uv + offset * uvPerPixel;
            bool inside = all(uv >= v.cell.xy) && all(uv <= v.cell.zw);
            sum += w * (inside ? sampleSlot(v.kind.y, uv).a : 0.0);
            weights += w;
        }
    }

    float intensity = float(v.kind.x >> 16) / 1000.0;
    float4 colour = v.colour;
    colour.a *= saturate(sum / weights * intensity);
    if (colour.a <= 0.0)
        discard;
    return colour;
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

    // The curve's distance in pixels, from u² − v over its screen gradient;
    // taken before any branch so the derivatives are defined everywhere.
    float2 dx = ddx(v.local), dy = ddy(v.local);
    float curve = v.local.x * v.local.x - v.local.y;
    float2 slope = float2(2.0 * v.local.x * dx.x - dx.y, 2.0 * v.local.x * dy.x - dy.y);
    float across = curve / max(length(slope), 1e-6);
    if (kind == KindTriangle && (v.kind.x & FlagCurve) != 0)
        cover = (v.kind.x & FlagConcave) != 0 ? saturate(0.5 + across) : saturate(0.5 - across);
    else if (kind == KindRect)
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

    if (kind == KindHalo)
        return shadeHalo(v);

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
