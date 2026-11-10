// Builds the screen copy's blur pyramid: each level is the one above it
// halved through a 13-tap Gaussian-like filter (Jimenez, "Next Generation Post
// Processing in Call of Duty: Advanced Warfare"), so a single read of a low
// level looks smooth, as Godot's Gaussian-built screen mips did. A plain 2×2
// box halving (SDL's mipmap generation) aliases, and the blur reads blocky.

struct Down
{
    /// One texel of the level being read, in UV.
    float2 texel;
    float2 pad;
};

[[vk::binding(0, 2)]] Sampler2D source;
[[vk::binding(0, 3)]] ConstantBuffer<Down> down;

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

/// One triangle that covers the target; uv runs 0..1 across it, y down.
[shader("vertex")]
Varyings fullscreen(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    Varyings v;
    v.uv = uv;
    v.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    return v;
}

float3 tap(float2 uv, float x, float y)
{
    return source.SampleLevel(uv + down.texel * float2(x, y), 0.0).rgb;
}

[shader("fragment")]
float4 halve(Varyings v) : SV_Target
{
    float2 uv = v.uv;
    float3 centre = tap(uv, 0, 0);
    float3 inner = tap(uv, -1, -1) + tap(uv, 1, -1) + tap(uv, -1, 1) + tap(uv, 1, 1);
    float3 edges = tap(uv, 0, -2) + tap(uv, -2, 0) + tap(uv, 2, 0) + tap(uv, 0, 2);
    float3 corners = tap(uv, -2, -2) + tap(uv, 2, -2) + tap(uv, -2, 2) + tap(uv, 2, 2);
    return float4(centre * 0.125 + inner * 0.125 + edges * 0.0625 + corners * 0.03125, 1.0);
}
