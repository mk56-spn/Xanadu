// The canvas's GPU particles (Docs/host.md §5.6): one compute step
// that moves every particle of a canvas item, and one instanced quad per
// particle drawn where the item sits in the tree.
//
// SDL_GPU layout: compute read-only buffers in set 0, read-write in set 1,
// uniforms in set 2; vertex storage buffers in set 0, uniforms in set 1;
// fragment samplers in set 2.

// motion = position.xy, velocity.xy (item space)
// life   = age (negative while waiting to appear), lifespan, damping, angle
// colour = the effect's colour times the burst's tint
// look   = size in pixels, effect row, strip cell (-1 = soft dot), strip cells
struct Particle { float4 motion; float4 life; float4 colour; float4 look; };

// Each effect's row in the table: gravity, then the ramp, then the scale curve.
static const uint Samples = 32;
static const uint RowSize = 1 + Samples * 2;

float4 sampleRow(StructuredBuffer<float4> table, uint start, float t)
{
    float x = saturate(t) * (Samples - 1);
    uint a = uint(floor(x));
    uint b = min(a + 1, Samples - 1);
    return lerp(table[start + a], table[start + b], x - a);
}

// --- compute: one step of every particle ------------------------------------

struct Step { float dt; uint count; float2 pad; };

[[vk::binding(0, 0)]] StructuredBuffer<float4> stepEffects;
[[vk::binding(0, 1)]] RWStructuredBuffer<Particle> moving;
[[vk::binding(0, 2)]] ConstantBuffer<Step> step;

[shader("compute")]
[numthreads(64, 1, 1)]
void simulate(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i >= step.count)
        return;

    Particle p = moving[i];
    if (p.life.x >= p.life.y)
        return;

    // Only the part of the step it was alive for moves it.
    float before = p.life.x;
    p.life.x += step.dt;
    float alive = min(p.life.x, p.life.y) - max(before, 0.0);
    if (alive > 0.0)
    {
        // As Godot ordered it: gravity, then damping, then the move.
        float2 gravity = stepEffects[uint(p.look.y) * RowSize].xy;
        p.motion.zw += gravity * alive;
        float speed = length(p.motion.zw);
        if (p.life.z > 0.0 && speed > 0.0)
            p.motion.zw *= max(0.0, speed - p.life.z * alive) / speed;
        p.motion.xy += p.motion.zw * alive;
    }

    moving[i] = p;
}

// --- vertex + fragment: one quad per particle ---------------------------------

// The item's transform (x axis, y axis, origin) and its modulate, from the tree.
struct Draw { float2 screen; float2 origin; float4 axes; float4 modulate; };

[[vk::binding(0, 0)]] StructuredBuffer<Particle> drawn;
[[vk::binding(1, 0)]] StructuredBuffer<float4> drawEffects;
[[vk::binding(0, 1)]] ConstantBuffer<Draw> draw;
[[vk::binding(0, 2)]] Sampler2D strip;

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    float4 colour : COLOR0;
    float2 cell : TEXCOORD1;
};

static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

[shader("vertex")]
Varyings quad(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Particle p = drawn[instance];
    Varyings o;
    o.uv = float2(0.0, 0.0);
    o.colour = float4(0.0, 0.0, 0.0, 0.0);
    o.cell = float2(-1.0, 1.0);

    // Waiting, dead or never used: a point off screen, so nothing rasterises.
    if (p.life.x < 0.0 || p.life.x >= p.life.y)
    {
        o.position = float4(-2.0, -2.0, 0.0, 1.0);
        return o;
    }

    float t = p.life.x / p.life.y;
    uint row = uint(p.look.y) * RowSize;
    float4 ramp = sampleRow(drawEffects, row + 1, t);
    float scale = sampleRow(drawEffects, row + 1 + Samples, t).x;

    float2 corner = corners[vertex];
    float s, c;
    sincos(p.life.w, s, c);
    float2 local = p.motion.xy + float2(corner.x * c - corner.y * s, corner.x * s + corner.y * c) * (0.5 * p.look.x * scale);
    float2 pixel = draw.axes.xy * local.x + draw.axes.zw * local.y + draw.origin;

    float2 ndc = pixel / draw.screen * 2.0 - 1.0;
    o.position = float4(ndc.x, -ndc.y, 0.0, 1.0);
    o.uv = corner * 0.5 + 0.5;
    o.colour = p.colour * ramp * draw.modulate;
    o.cell = p.look.zw;
    return o;
}

[shader("fragment")]
float4 shadeParticle(Varyings i) : SV_Target
{
    // The soft dot: white, fading as (1 - distance)² to its rim.
    if (i.cell.x < 0.0)
    {
        float fade = saturate(1.0 - length(i.uv * 2.0 - 1.0));
        return float4(i.colour.rgb, i.colour.a * fade * fade);
    }

    float2 uv = float2((i.cell.x + i.uv.x) / i.cell.y, i.uv.y);
    return strip.Sample(uv) * i.colour;
}
