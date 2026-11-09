// The spike's three stages as one Slang module, compiled per entry point.
// SDL_GPU binding layout: compute read-write buffers in set 1 and uniforms in
// set 2; vertex storage buffers in set 0 and uniforms in set 1.

struct Particle { float4 motion; float4 life; }; // motion = pos.xy, vel.xy; life = age, lifespan, hue, unused

struct Step { float2 screen; float dt; float time; };
struct View { float2 screen; float size; float pad; };

uint hash(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; return x; }
float rand(inout uint s) { s = hash(s); return float(s) / 4294967295.0; }

// --- compute: move every particle one step ---------------------------------

[[vk::binding(0, 1)]] RWStructuredBuffer<Particle> moving;
[[vk::binding(0, 2)]] ConstantBuffer<Step> step;

[shader("compute")]
[numthreads(64, 1, 1)]
void simulate(uint3 id : SV_DispatchThreadID)
{
    uint count, stride;
    moving.GetDimensions(count, stride);
    uint i = id.x;
    if (i >= count) return;
    Particle p = moving[i];

    p.life.x += step.dt;
    if (p.life.x >= p.life.y)
    {
        uint s = i * 9781u + uint(step.time * 1000.0) * 6271u;
        float angle = radians(-90.0) + (rand(s) - 0.5) * 0.9;
        float speed = 500.0 + rand(s) * 450.0;
        p.motion = float4(step.screen.x * 0.5, step.screen.y - 40.0, cos(angle) * speed, sin(angle) * speed);
        p.life = float4(0.0, 1.5 + rand(s) * 2.0, rand(s), 0.0);
    }

    p.motion.w += 600.0 * step.dt;
    p.motion.xy += p.motion.zw * step.dt;
    if (p.motion.y > step.screen.y)
    {
        p.motion.y = step.screen.y;
        p.motion.w *= -0.45;
    }
    moving[i] = p;
}

// --- vertex + fragment: one quad per particle -------------------------------

[[vk::binding(0, 0)]] StructuredBuffer<Particle> drawn;
[[vk::binding(0, 1)]] ConstantBuffer<View> view;

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
    float4 colour : COLOR0;
};

static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

float3 hueToRgb(float h) { return saturate(abs(frac(h + float3(0, 2, 1) / 3.0) * 6.0 - 3.0) - 1.0); }

[shader("vertex")]
Varyings quad(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Particle p = drawn[instance];
    float2 corner = corners[vertex];
    float2 pixel = p.motion.xy + corner * view.size;

    Varyings o;
    o.position = float4(pixel / view.screen * 2.0 - 1.0, 0.0, 1.0);
    o.position.y = -o.position.y;
    o.uv = corner;
    float fade = 1.0 - p.life.x / p.life.y;
    o.colour = float4(lerp(float3(1.0), hueToRgb(p.life.z * 0.25 + 0.5), 0.7), fade);
    return o;
}

[shader("fragment")]
float4 glowDot(Varyings i) : SV_Target
{
    float glow = max(0.0, 1.0 - dot(i.uv, i.uv));
    return float4(i.colour.rgb, i.colour.a * glow * glow); // Additive blend scales colour by this alpha
}
