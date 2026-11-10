// Xanadu Labs' radiance cascades lab: 2D global illumination in screen space.
//
// The scene is one RGBA16F texture: rgb is what a pixel emits, a is whether it
// blocks light (lights block too, so a ray that reaches one stops there). A
// jump flood turns the blockers into a distance field the rays sphere-trace.
//
// Cascade n keeps a probe every 2^(n+1) pixels with 4^(n+1) directions, laid
// out probe-major: each probe owns a 2^(n+1) square block of texels, one per
// direction, so every cascade is the same size. Its rays cover only the
// interval [base (4^n - 1)/3, base (4^(n+1) - 1)/3]. From the top down, each
// cascade traces its interval and, where nothing was hit, adds what the
// cascade above saw beyond it (four directions averaged, four probes blended).
// Cascade 0 then holds the light arriving at every 2 px probe.
//
// Every pass is a full-screen triangle. Fragment textures are set 2 (point
// sampled, read by texel), the one uniform block is set 3.

struct Params
{
    float4 size;    // stage width, height, cascade width, height
    float4 cascade; // index, block (probe spacing in px), interval start, interval end
    float4 setup;   // cascade count, has upper, bilinear fix, jump flood step
    float4 brushA;  // from x, y, to x, y (stage pixels)
    float4 brushB;  // what the brush writes: rgb emission, a blocks
    float4 brushC;  // radius
    float4 look;    // time, movers, exposure, view
    float4 extra;   // viewed cascade
};

[[vk::binding(0, 3)]] ConstantBuffer<Params> P;
[[vk::binding(0, 2)]] Sampler2D tex0;
[[vk::binding(1, 2)]] Sampler2D tex1;
[[vk::binding(2, 2)]] Sampler2D tex2;
[[vk::binding(3, 2)]] Sampler2D tex3;

static const float Tau = 6.28318530718;
static const float NoSeed = -10000.0;

struct V
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
V full(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    V o;
    o.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    o.uv = uv;
    return o;
}

float4 texel(Sampler2D t, float2 pixel, float2 size)
{
    return t.SampleLevel((floor(pixel) + 0.5) / size, 0);
}

float sdBox(float2 p, float2 extent)
{
    float2 d = abs(p) - extent;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

float2 rotate(float2 p, float a)
{
    float c = cos(a), s = sin(a);
    return float2(c * p.x + s * p.y, -s * p.x + c * p.y);
}

// ---- painting ---------------------------------------------------------------

/// The starting layout: a few walls, a pillar and two lights to light them.
[shader("fragment")]
float4 demo(V v) : SV_Target
{
    float2 s = P.size.xy;
    float m = min(s.x, s.y);
    float2 p = v.position.xy;
    float wall = 1e9;
    wall = min(wall, sdBox(p - s * float2(0.30, 0.28), float2(0.16, 0.012) * m));
    wall = min(wall, sdBox(p - s * float2(0.66, 0.50), float2(0.012, 0.20) * m));
    wall = min(wall, sdBox(p - s * float2(0.78, 0.22), float2(0.10, 0.012) * m));
    wall = min(wall, length(p - s * float2(0.28, 0.66)) - 0.05 * m);
    wall = min(wall, sdBox(rotate(p - s * float2(0.45, 0.82), 0.5), float2(0.09, 0.012) * m));
    if (length(p - s * float2(0.14, 0.14)) < 0.025 * m)
        return float4(1.0, 0.72, 0.38, 1.0) * float4(3, 3, 3, 1);
    if (sdBox(p - s * float2(0.88, 0.86), float2(0.06, 0.01) * m) < 0)
        return float4(0.35, 0.8, 1.0, 1.0) * float4(2.5, 2.5, 2.5, 1);
    return wall < 0 ? float4(0, 0, 0, 1) : float4(0, 0, 0, 0);
}

/// A capsule from the last pointer position to this one; the rest is left alone.
[shader("fragment")]
float4 brush(V v) : SV_Target
{
    float2 p = v.position.xy, a = P.brushA.xy, b = P.brushA.zw;
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
    if (length(p - a - ab * t) > P.brushC.x)
        discard;
    return P.brushB;
}

/// The painted layer plus things that move: two orbiting lights, a beat light and a turning bar.
[shader("fragment")]
float4 scene(V v) : SV_Target
{
    float2 s = P.size.xy;
    float m = min(s.x, s.y);
    float2 p = v.position.xy;
    float4 c = texel(tex0, p, s);
    if (P.look.y < 0.5)
        return c;

    float t = P.look.x;
    float2 centre = s * 0.5;
    if (sdBox(rotate(p - s * float2(0.5, 0.5), t * 0.35), float2(0.15, 0.01) * m) < 0)
        c = float4(0, 0, 0, 1);

    float2 orbit = float2(cos(t * 0.6), sin(t * 0.6)) * 0.3 * m;
    if (length(p - centre - orbit) < 0.018 * m)
        c = float4(3.0, 1.6, 0.6, 1.0);
    if (length(p - centre + orbit) < 0.018 * m)
        c = float4(0.4, 1.8, 2.6, 1.0);

    // 120 BPM: a spike on every beat that dies away before the next.
    float pulse = exp(-frac(t * 2.0) * 7.0);
    if (length(p - s * float2(0.84, 0.70)) < 0.028 * m)
        c = float4(float3(2.4, 0.5, 2.0) * (0.15 + 2.5 * pulse), 1.0);
    return c;
}

// ---- distance field (jump flood) ----------------------------------------------

/// Every blocking pixel seeds itself; the rest start with no seed.
[shader("fragment")]
float4 floodSeed(V v) : SV_Target
{
    float2 p = floor(v.position.xy);
    return texel(tex0, p, P.size.xy).a > 0.5 ? float4(p, 0, 1) : float4(NoSeed, NoSeed, 0, 1);
}

/// One jump: keep the nearest seed among the nine at this step's distance.
[shader("fragment")]
float4 floodStep(V v) : SV_Target
{
    float2 s = P.size.xy;
    float2 p = floor(v.position.xy);
    float jump = P.setup.w;
    float2 best = float2(NoSeed, NoSeed);
    float bestDistance = 1e9;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 q = p + float2(x, y) * jump;
            if (any(q < 0) || any(q >= s))
                continue;
            float2 seed = texel(tex0, q, s).xy;
            if (seed.x <= NoSeed + 1)
                continue;
            float d = dot(seed - p, seed - p);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = seed;
            }
        }
    }
    return float4(best, 0, 1);
}

float distanceAt(float2 p)
{
    float2 seed = texel(tex1, p, P.size.xy).xy;
    return seed.x <= NoSeed + 1 ? 1e5 : length(floor(p) - seed);
}

// ---- cascades -----------------------------------------------------------------

/// Sphere-traces a to b through the distance field (tex1). rgb is what it hit,
/// a is 1 when it got through (so the cascade above should be added) and 0 when it hit.
float4 march(float2 a, float2 b)
{
    float2 s = P.size.xy;
    float2 delta = b - a;
    float len = length(delta);
    float2 dir = delta / max(len, 1e-4);
    float t = 0;
    for (int i = 0; i < 64; i++)
    {
        float2 p = a + dir * t;
        if (any(p < 0) || any(p >= s))
            break;
        float d = distanceAt(p);
        if (d < 1.0)
            return float4(texel(tex0, p, s).rgb, 0);
        t += max(d - 0.7, 0.5);
        if (t >= len)
            break;
    }
    return float4(0, 0, 0, 1);
}

float4 merge(float4 near, float4 far)
{
    return float4(near.rgb + near.a * far.rgb, near.a * far.a);
}

/// The texel of the cascade above (tex2) for its probe q and direction index.
float4 upperTexel(float2 q, float index, float block)
{
    float2 inBlock = float2(fmod(index, block), floor(index / block));
    return texel(tex2, q * block + inBlock, P.size.zw);
}

[shader("fragment")]
float4 cascade(V v) : SV_Target
{
    float2 pixel = floor(v.position.xy);
    float block = P.cascade.y;
    float2 probe = floor(pixel / block);
    float2 inBlock = pixel - probe * block;
    float dirs = block * block;
    float index = inBlock.x + inBlock.y * block;
    float2 origin = (probe + 0.5) * block;
    float start = P.cascade.z, end = P.cascade.w;
    bool hasUpper = P.setup.y > 0.5;

    float upperBlock = block * 2.0;
    float2 upperProbes = floor(P.size.zw / upperBlock);
    float2 grid = origin / upperBlock - 0.5;
    float2 base = floor(grid);
    float2 f = grid - base;

    if (P.setup.z < 0.5)
    {
        float angle = (index + 0.5) / dirs * Tau;
        float2 dir = float2(cos(angle), sin(angle));
        float4 near = march(origin + dir * start, origin + dir * end);
        if (!hasUpper || near.a == 0)
            return near;

        // Bilinear over the four nearest probes above, each the mean of its four directions.
        float4 far = 0;
        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < 2; i++)
            {
                float2 q = clamp(base + float2(i, j), 0, upperProbes - 1);
                float w = (i == 0 ? 1 - f.x : f.x) * (j == 0 ? 1 - f.y : f.y);
                float4 sum = 0;
                for (int k = 0; k < 4; k++)
                    sum += upperTexel(q, index * 4 + k, upperBlock);
                far += w * sum * 0.25;
            }
        }
        return merge(near, far);
    }

    // Bilinear fix: a ray per upper probe and direction, from this probe's
    // interval start to where that probe's interval ends, so the two meet.
    if (!hasUpper)
    {
        float angle = (index + 0.5) / dirs * Tau;
        float2 dir = float2(cos(angle), sin(angle));
        return march(origin + dir * start, origin + dir * end);
    }

    float4 total = 0;
    for (int j = 0; j < 2; j++)
    {
        for (int i = 0; i < 2; i++)
        {
            float2 q = clamp(base + float2(i, j), 0, upperProbes - 1);
            float2 upperOrigin = (q + 0.5) * upperBlock;
            float w = (i == 0 ? 1 - f.x : f.x) * (j == 0 ? 1 - f.y : f.y);
            float4 sum = 0;
            for (int k = 0; k < 4; k++)
            {
                float sub = index * 4 + k;
                float angle = (sub + 0.5) / (dirs * 4) * Tau;
                float2 dir = float2(cos(angle), sin(angle));
                float4 near = march(origin + dir * start, upperOrigin + dir * end);
                sum += merge(near, upperTexel(q, sub, upperBlock));
            }
            total += w * sum * 0.25;
        }
    }
    return total;
}

// ---- showing ------------------------------------------------------------------

float3 tonemap(float3 x)
{
    return pow(1.0 - exp(-max(x, 0)), 1.0 / 2.2);
}

/// Light arriving at a stage pixel: cascade 0 (tex1) blended over its four nearest probes.
float3 fluence(float2 p)
{
    float2 grid = p / 2.0 - 0.5;
    float2 base = floor(grid);
    float2 f = grid - base;
    float2 probes = floor(P.size.zw / 2.0);
    float3 total = 0;
    for (int j = 0; j < 2; j++)
    {
        for (int i = 0; i < 2; i++)
        {
            float2 q = clamp(base + float2(i, j), 0, probes - 1);
            float w = (i == 0 ? 1 - f.x : f.x) * (j == 0 ? 1 - f.y : f.y);
            float3 sum = 0;
            for (int k = 0; k < 4; k++)
                sum += texel(tex1, q * 2 + float2(k & 1, k >> 1), P.size.zw).rgb;
            total += w * sum * 0.25;
        }
    }
    return total;
}

/// tex0 scene, tex1 cascade 0, tex2 distance field, tex3 the cascade being viewed.
[shader("fragment")]
float4 show(V v) : SV_Target
{
    float2 s = P.size.xy;
    float2 p = v.position.xy;
    float4 sceneTexel = texel(tex0, p, s);
    int view = int(P.look.w);
    float exposure = P.look.z;

    if (view == 1)
        return float4(sceneTexel.a > 0.5 ? (dot(sceneTexel.rgb, 1) > 0 ? tonemap(sceneTexel.rgb) : float3(0.35, 0.36, 0.4)) : float3(0.05, 0.05, 0.06), 1);

    if (view == 2)
    {
        float2 seed = texel(tex2, p, s).xy;
        float d = seed.x <= NoSeed + 1 ? 1e5 : length(floor(p) - seed);
        float bands = 0.5 + 0.5 * cos(d * 0.25);
        float3 c = lerp(float3(0.1, 0.12, 0.2), float3(0.4, 0.6, 1.0), exp(-d * 0.01)) * (0.75 + 0.25 * bands);
        return float4(d < 1 ? float3(1, 1, 1) : c, 1);
    }

    if (view >= 3)
        return float4(tonemap(texel(tex3, p, P.size.zw).rgb * exposure * 0.25), 1);

    if (sceneTexel.a > 0.5)
    {
        // Lights show their own colour; walls a flat dark grey.
        float3 own = dot(sceneTexel.rgb, 1) > 0 ? sceneTexel.rgb * 2 : float3(0.035, 0.037, 0.045);
        return float4(tonemap(own), 1);
    }

    float2 cell = floor(p / 32);
    float albedo = fmod(cell.x + cell.y, 2) == 0 ? 0.62 : 0.55;
    return float4(tonemap(fluence(p) * albedo * exposure), 1);
}
