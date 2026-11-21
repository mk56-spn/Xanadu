// Xanadu Labs' radiance cascades lab: 2D global illumination in screen space.
//
// The scene is one RGBA16F texture: rgb is what a pixel emits, a is whether it
// blocks light: 1 a wall, 2 a light (lights block too, so a ray that reaches one
// stops there). A jump flood finds every pixel's nearest blocker and nearest
// open pixel; one more pass turns the first into a one-channel distance field
// the rays sphere-trace, and the second says where a wall's face is.
//
// Cascade n keeps a probe every 2^(n+1) pixels and casts 4^(n+1) rays from it,
// over the interval [base (4^n - 1)/3, base (4^(n+1) - 1)/3]. It is stored
// pre-averaged, texel j of a probe the mean of its rays 4j..4j+3, and direction
// first: the texture is a 2^n square grid of tiles, tile j holding texel j of
// every probe in probe order. Neighbouring texels then march side by side in
// one direction, which keeps a GPU's threads together on long rays. Each
// cascade is a quarter of the stage, and cascade 0 is one texel a probe, its
// fluence. Texel j of cascade n+1 is then exactly the
// mean of ray j's four children, so merging a ray reads one texel per upper probe.
// From the top down, each cascade traces its rays and, where nothing was hit,
// adds what the cascade above saw beyond them (four probes blended).
//
// Walls bounce: the scene pass lights each wall pixel from last frame's
// cascade 0 just outside its nearest face, so light keeps bouncing a little
// more every frame.
//
// Every pass is a full-screen triangle. Fragment textures are set 2 (sampled
// at texel centres, read by texel; cascade 0 is also read between them), the
// one uniform block is set 3.

struct Params
{
    float4 size;    // stage width, height; this cascade's (or the viewed cascade's) texture width, height
    float4 cascade; // index, probe spacing in px, interval start, interval end
    float4 setup;   // cascade count, has upper, cascades below this take the bilinear fix, jump flood step
    float4 brushA;  // from x, y, to x, y (stage pixels)
    float4 brushB;  // what the brush writes: rgb emission, a blocks
    float4 brushC;  // radius, wall albedo (0 is no bounce)
    float4 look;    // time, movers, exposure, view
    float4 extra;   // upper cascade's probes x, y; cascade 0's texture width, height
};

[[vk::binding(0, 3)]] ConstantBuffer<Params> P;
[[vk::binding(0, 2)]] Sampler2D tex0;
[[vk::binding(1, 2)]] Sampler2D tex1;
[[vk::binding(2, 2)]] Sampler2D tex2;
[[vk::binding(3, 2)]] Sampler2D tex3;

static const float Tau = 6.28318530718;
static const float NoSeed = -10000.0;

/// RadianceLab.FloodReach: as far as the distance field tells.
static const float FloodReach = 64.0;

/// How bright a wall's face shows against the floor beside it, under the same light.
static const float WallShade = 0.45;

/// How far in from its face a wall still shows the light on it, in pixels.
static const float WallDepth = 20;

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

// ---- the scene ----------------------------------------------------------------

/// Light arriving at a stage point: cascade 0 (tex1), each texel a probe's
/// mean over every direction, blended between the four nearest probes.
float3 fluence(float2 p)
{
    return tex1.SampleLevel(clamp(p / 2.0, 0.5, P.extra.zw - 0.5) / P.extra.zw, 0).rgb;
}

/// The light on a wall pixel: what reaches the open pixel just outside its
/// nearest face (<paramref name="flood"/>'s zw, the jump flood's nearest open
/// pixel), dimmer the deeper in it sits, so a wall glows in from the face the
/// light falls on.
float3 wallLight(Sampler2D flood, float2 p)
{
    float2 open = texel(flood, p, P.size.xy).zw;
    if (open.x <= NoSeed + 1)
        return 0;
    float2 away = open - floor(p);
    float depth = length(away);
    return fluence(open + away / max(depth, 1e-4) * 1.5) * saturate(1.1 - depth / WallDepth);
}

/// The painted layer (tex0) plus things that move: two orbiting lights, a beat light and a turning bar.
float4 sceneAt(float2 p)
{
    float2 s = P.size.xy;
    float m = min(s.x, s.y);
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

/// The scene this frame: lights marked 2, walls lit by last frame's light (tex1
/// cascade 0, tex2 the jump flood) times their albedo, so they bounce it on.
[shader("fragment")]
float4 scene(V v) : SV_Target
{
    float2 p = v.position.xy;
    float4 c = sceneAt(p);
    if (c.a > 0.5 && dot(c.rgb, 1) > 0)
        c.a = 2;
    else if (c.a > 0.5)
        c.rgb = wallLight(tex2, p) * P.brushC.y;
    return c;
}

// ---- distance field (jump flood) ----------------------------------------------

/// Every pixel seeds itself: as a blocker in xy, or as an open pixel in zw.
[shader("fragment")]
float4 floodSeed(V v) : SV_Target
{
    float2 p = floor(v.position.xy);
    float2 none = float2(NoSeed, NoSeed);
    return texel(tex0, p, P.size.xy).a > 0.5 ? float4(p, none) : float4(none, p);
}

/// The nearer of two seeds to p.
float2 nearer(float2 p, float2 best, float2 seed)
{
    if (seed.x <= NoSeed + 1)
        return best;
    if (best.x <= NoSeed + 1)
        return seed;
    return dot(seed - p, seed - p) < dot(best - p, best - p) ? seed : best;
}

/// One jump: keep the nearest blocker and nearest open pixel among the nine at this step's distance.
[shader("fragment")]
float4 floodStep(V v) : SV_Target
{
    float2 s = P.size.xy;
    float2 p = floor(v.position.xy);
    float jump = P.setup.w;
    float2 blocker = float2(NoSeed, NoSeed), open = blocker;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 q = p + float2(x, y) * jump;
            if (any(q < 0) || any(q >= s))
                continue;
            float4 seeds = texel(tex0, q, s);
            blocker = nearer(p, blocker, seeds.xy);
            open = nearer(p, open, seeds.zw);
        }
    }
    return float4(blocker, open);
}

/// The flood's nearest seeds as one distance a pixel: what every ray step reads.
/// The flood only reaches so far (RadianceLab.FloodReach), so a pixel with no
/// seed, or a far one, is given that distance: never more than the truth near
/// a blocker, so a ray stepping by it can't step over one.
[shader("fragment")]
float4 floodDistance(V v) : SV_Target
{
    float2 p = floor(v.position.xy);
    float2 seed = texel(tex0, p, P.size.xy).xy;
    return seed.x <= NoSeed + 1 ? FloodReach : min(length(p - seed), FloodReach);
}

// ---- cascades -----------------------------------------------------------------

/// Sphere-traces a to b through the distance field (tex1). A ray stops once it
/// is within <paramref name="hit"/> px of a blocker and takes that blocker's
/// light (tex3, the jump flood, says which; tex0 the scene): far cascades, whose
/// probes are tens of pixels apart, stop short instead of creeping along a wall.
/// Every step is shorter than the distance, so nothing is stepped over. rgb is
/// what it hit, a is 1 when it got through (so the cascade above should be
/// added) and 0 when it hit.
float4 march(float2 a, float2 b, float hit)
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
        float d = texel(tex1, p, s).r;
        if (d < hit)
            return float4(texel(tex0, d < 1 ? p : texel(tex3, p, s).xy, s).rgb, 0);
        t += max(d - 0.5, hit * 0.5);
        if (t >= len)
            break;
    }
    return float4(0, 0, 0, 1);
}

/// Ray <paramref name="ray"/> of the cascade above (tex2) blended between its four
/// probes around <paramref name="grid"/> (in upper probes): one bilinear fetch,
/// as that direction's probes sit side by side in its tile.
float3 upperBlend(float2 grid, float ray, float upperBlock)
{
    float2 probes = P.extra.xy;
    float2 tile = float2(fmod(ray, upperBlock), floor(ray / upperBlock));
    float2 at = tile * probes + clamp(grid, 0, probes - 1) + 0.5;
    return tex2.SampleLevel(at / (probes * upperBlock), 0).rgb;
}

/// Texel <paramref name="ray"/> of upper probe q (tex2): the mean of that ray's four children.
float3 upperTexel(float2 q, float ray, float upperBlock)
{
    float2 tile = float2(fmod(ray, upperBlock), floor(ray / upperBlock));
    return texel(tex2, tile * P.extra.xy + q, P.extra.xy * upperBlock).rgb;
}

/// One texel of cascade n: the mean of its four rays, each merged with the cascade
/// above. tex0 scene, tex1 distance field, tex2 the cascade above, tex3 the jump flood.
[shader("fragment")]
float4 cascade(V v) : SV_Target
{
    float2 pixel = floor(v.position.xy);
    float block = exp2(P.cascade.x);
    float spacing = P.cascade.y;
    float2 probes = P.size.zw / block;
    float2 tile = floor(pixel / probes);
    float2 probe = pixel - tile * probes;
    float group = tile.x + tile.y * block;
    float rays = block * block * 4;
    float2 origin = (probe + 0.5) * spacing;
    float start = P.cascade.z, end = P.cascade.w;
    bool hasUpper = P.setup.y > 0.5;
    float hit = max(1, spacing / 8);

    // The four upper probes around this one, and their bilinear weights.
    float upperBlock = block * 2;
    float upperSpacing = spacing * 2;
    float2 upperProbes = P.extra.xy;
    float2 grid = origin / upperSpacing - 0.5;
    float2 base = floor(grid);
    float2 f = grid - base;
    float2 q[4];
    float w[4];
    for (int i = 0; i < 4; i++)
    {
        float2 corner = float2(i & 1, i >> 1);
        q[i] = clamp(base + corner, 0, upperProbes - 1);
        w[i] = (corner.x == 0 ? 1 - f.x : f.x) * (corner.y == 0 ? 1 - f.y : f.y);
    }

    float3 total = 0;
    for (int k = 0; k < 4; k++)
    {
        float ray = group * 4 + k;
        float angle = (ray + 0.5) / rays * Tau;
        float2 dir = float2(cos(angle), sin(angle));
        float2 from = origin + dir * start;

        if (!hasUpper)
        {
            total += march(from, origin + dir * end, hit).rgb;
            continue;
        }

        if (P.cascade.x >= P.setup.z)
        {
            // Plain merge: one ray, then the four upper probes blended where it got through.
            float4 near = march(from, origin + dir * end, hit);
            float3 far = near.a > 0 ? upperBlend(grid, ray, upperBlock) : 0;
            total += near.rgb + near.a * far;
            continue;
        }

        // Bilinear fix: a ray to each upper probe's interval end, so the two meet without rings.
        for (int i = 0; i < 4; i++)
        {
            float2 upperOrigin = (q[i] + 0.5) * upperSpacing;
            float4 near = march(from, upperOrigin + dir * end, hit);
            float3 far = near.a > 0 ? upperTexel(q[i], ray, upperBlock) : 0;
            total += w[i] * (near.rgb + near.a * far);
        }
    }
    return float4(total * 0.25, 1);
}

// ---- showing ------------------------------------------------------------------

/// Narkowicz's fit of the ACES film curve, then gamma: deeper shadows and
/// saturated colour where the plain exponential washed everything to pastel.
float3 tonemap(float3 x)
{
    x = max(x, 0) * 0.6;
    float3 film = saturate(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14));
    return pow(film, 1.0 / 2.2);
}

/// tex0 scene, tex1 cascade 0, tex2 the jump flood, tex3 the cascade being viewed.
[shader("fragment")]
float4 show(V v) : SV_Target
{
    float2 s = P.size.xy;
    float2 p = v.position.xy;
    float4 sceneTexel = texel(tex0, p, s);
    int view = int(P.look.w);
    float exposure = P.look.z;

    if (view == 1)
        return float4(sceneTexel.a > 1.5 ? tonemap(sceneTexel.rgb) : sceneTexel.a > 0.5 ? float3(0.35, 0.36, 0.4) + tonemap(sceneTexel.rgb) : float3(0.05, 0.05, 0.06), 1);

    if (view == 2)
    {
        float2 seed = texel(tex2, p, s).xy;
        float d = seed.x <= NoSeed + 1 ? 1e5 : length(floor(p) - seed);
        float bands = 0.5 + 0.5 * cos(d * 0.25);
        float3 c = lerp(float3(0.1, 0.12, 0.2), float3(0.4, 0.6, 1.0), exp(-d * 0.01)) * (0.75 + 0.25 * bands);
        return float4(d < 1 ? float3(1, 1, 1) : c, 1);
    }

    if (view >= 3)
        return float4(tonemap(texel(tex3, (p + 0.5) / s * P.size.zw, P.size.zw).rgb * exposure), 1);

    // Lights show their own colour; walls the light on their faces.
    if (sceneTexel.a > 1.5)
        return float4(tonemap(sceneTexel.rgb * 2), 1);
    if (sceneTexel.a > 0.5)
        return float4(tonemap(wallLight(tex2, p) * WallShade * exposure + 0.035), 1);

    float2 cell = floor(p / 32);
    float albedo = fmod(cell.x + cell.y, 2) == 0 ? 0.62 : 0.55;
    return float4(tonemap(fluence(p) * albedo * exposure), 1);
}
