// A canvas item's own draws as the look of each of its particles (CanvasItem.ParticleMesh):
// the item's triangles, already on the target, drawn once per particle of its field and
// moved there, turned by the particle's angle and sized by its size and scale curve,
// tinted by its colour and ramp. Only the vertex stage is here: the fragment is whatever
// effect the triangles were drawn with (or the plain one), so a node's shader shades
// every particle as it shades the node.
//
// SDL_GPU layout: vertex storage buffers in set 0, vertex uniforms in set 1.
import xanadu.stage;

// As in particles.slang.
struct Particle { float4 motion; float4 life; float4 colour; float4 look; };
static const uint Samples = 32;
static const uint RowSize = 1 + Samples * 2;

float4 sampleRow(StructuredBuffer<float4> table, uint start, float t)
{
    float x = saturate(t) * (Samples - 1);
    uint a = uint(floor(x));
    uint b = min(a + 1, Samples - 1);
    return lerp(table[start + a], table[start + b], x - a);
}

/// The target's size and the draw's depth, as canvas.slang's view.
struct View { float2 screen; float depth; float pad; };

/// The item's transform: where its origin lands and its axes, as the triangles were placed by.
/// `first`: the field's first slot in the particle arena.
struct Place { float2 origin; uint first; uint pad; float4 axes; };

[[vk::binding(0, 0)]] StructuredBuffer<Particle> drawn;
[[vk::binding(1, 0)]] StructuredBuffer<float4> drawEffects;
[[vk::binding(0, 1)]] ConstantBuffer<View> view;
[[vk::binding(1, 1)]] ConstantBuffer<Place> place;

struct VertexIn
{
    float2 position : TEXCOORD0;
    float2 uv : TEXCOORD1;
    float4 colour : TEXCOORD2;
    float2 local : TEXCOORD3;
};

[shader("vertex")]
ItemVaryings meshParticle(VertexIn v, uint instance : SV_InstanceID)
{
    Particle p = drawn[place.first + instance];
    ItemVaryings o;
    o.uv = v.uv;
    o.colour = float4(0.0, 0.0, 0.0, 0.0);
    o.local = v.local;
    o.rest = float2(0.0, 0.0);
    o.instance = 0;

    // Waiting, dead or never used: off screen, so nothing rasterises.
    if (p.life.x < 0.0 || p.life.x >= p.life.y)
    {
        o.position = float4(-2.0, -2.0, 0.0, 1.0);
        return o;
    }

    float t = p.life.x / p.life.y;
    uint row = uint(p.look.y) * RowSize;
    float4 ramp = sampleRow(drawEffects, row + 1, t);
    float scale = sampleRow(drawEffects, row + 1 + Samples, t).x;

    // The corner's place from the item's origin, on the target; turned and sized round
    // it as the particle is, then carried to where the particle is.
    float2 offset = (v.position - place.origin) * (0.5 * p.look.x * scale);
    float s, c;
    sincos(p.life.w, s, c);
    offset = float2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);
    float2 pixel = place.axes.xy * p.motion.x + place.axes.zw * p.motion.y + place.origin + offset;

    float2 ndc = pixel / view.screen * 2.0 - 1.0;
    o.position = float4(ndc.x, -ndc.y, view.depth, 1.0);
    o.colour = v.colour * p.colour * ramp;
    o.rest = pixel;
    return o;
}
