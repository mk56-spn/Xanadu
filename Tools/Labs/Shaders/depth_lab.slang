// Xanadu Labs' depth and stencil lab. Quads are pulled from SV_VertexID (no
// vertex buffer); each fragment picks its own shape (discarding the rest, so
// stencil only sees the shape) and writes its own depth through SV_Depth.

struct Quad
{
    float4 rect;   // x, y, width, height in target pixels
    float4 colour;
    float4 depth;  // source, value, range, time
    float4 shape;  // shape, unused, target width, target height
};

[[vk::binding(0, 1)]] ConstantBuffer<Quad> quadVertex;
[[vk::binding(0, 3)]] ConstantBuffer<Quad> quad;

struct Varyings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };

[shader("vertex")]
Varyings quadMain(uint vertex : SV_VertexID)
{
    float2 corner = Corners[vertex];
    float2 pixel = quadVertex.rect.xy + corner * quadVertex.rect.zw;
    float2 ndc = pixel / quadVertex.shape.zw * 2.0 - 1.0;
    Varyings o;
    o.position = float4(ndc.x, -ndc.y, 0.5, 1.0);
    o.uv = corner;
    return o;
}

float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
}

bool inside(int shape, float2 uv, float time)
{
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    switch (shape)
    {
    case 1: return r <= 1.0;                                               // circle
    case 2: return r <= 1.0 && r >= 0.6;                                   // ring
    case 3: return r <= 0.4 + 0.6 * pow(abs(cos(2.5 * atan2(p.y, p.x))), 2.0); // star
    case 4: return frac((uv.x + uv.y) * 5.0) < 0.5;                        // stripes
    case 5: return (int(floor(uv.x * 6.0)) + int(floor(uv.y * 6.0))) % 2 == 0; // checker
    case 6: return noise(uv * 4.0 + time * 0.3) > 0.5;                    // blobs
    default: return true;                                                  // square
    }
}

float depthOf(int source, float2 uv, float value, float range, float time)
{
    float2 p = uv * 2.0 - 1.0;
    float r = length(p);
    switch (source)
    {
    case 1: return value + range * (0.5 - sqrt(max(0.0, 1.0 - r * r)));    // sphere: nearest in the middle
    case 2: return value + range * (uv.x - 0.5);                           // tilt
    case 3: return value + 0.5 * range * sin(uv.x * 12.0 + time * 2.0);    // waves
    case 4: return value + range * (noise(uv * 5.0 + time * 0.3) - 0.5);   // noise
    case 5: return value + range * (frac(r * 4.0 - time * 0.5) - 0.5);     // rings
    default: return value;                                                 // flat
    }
}

struct Shaded
{
    float4 colour : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
Shaded quadShade(Varyings v)
{
    float time = quad.depth.w;
    if (!inside(int(quad.shape.x), v.uv, time))
        discard;

    float d = saturate(depthOf(int(quad.depth.x), v.uv, quad.depth.y, quad.depth.z, time));
    Shaded o;
    // Nearer is brighter, so the made-up depth reads as shape.
    o.colour = float4(quad.colour.rgb * lerp(1.25, 0.45, d), quad.colour.a);
    o.depth = d;
    return o;
}

// ---- views ------------------------------------------------------------------

struct Flat { float4 colour; };

[[vk::binding(0, 2)]] Sampler2D depthMap;
[[vk::binding(0, 3)]] ConstantBuffer<Flat> flat;

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

[shader("fragment")]
float4 showDepth(FullVaryings v) : SV_Target
{
    float d = depthMap.Sample(v.uv).r;
    float grey = 1.0 - d;
    return float4(grey, grey, grey, 1.0);
}

[shader("fragment")]
float4 flatColour(FullVaryings v) : SV_Target
{
    return flat.colour;
}
