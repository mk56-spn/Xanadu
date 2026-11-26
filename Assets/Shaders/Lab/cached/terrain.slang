// A cache: a mountain range marched through a heavy fbm heightfield (up to 600 steps, 10 octaves
// a sample) into [Cached] images, the surface only: how far, which way it faces, how high. Nothing
// in `carve` reads time, so it runs when the item is made, resized, or a value changes, and never
// in between. `light` runs every frame over the cached surface with a sun that circles, so the
// range is still and the light isn't: the frame costs the lighting, not the march.
import xanadu;

[Cached] [Clear(130)] Image<float> far;
[Cached] Image<float4> facing;
Image<float4> shown;

/// Steps along each ray: the cost of carving, paid only when the cache is stale.
[Default(600)] uniform int steps;
/// Octaves of the heightfield, each sample.
[Default(10)] uniform int octaves;
/// How fast the sun goes round, in turns a second (live: it costs nothing to change).
[Default(0.08)] uniform float spin;

/// The ground's height at p (world units), 0..about 2.4.
float heightAt(float2 p, int n) { return pow(noise.fbm(p * 0.28, n), 1.6) * 3.2; }

/// The ray through this pixel: a camera above the range, looking down a little.
float3 rayOf(Canvas canvas)
{
    float2 uv = canvas.centred / canvas.size.y * float2(1, -1);
    return normalize(float3(uv.x, uv.y - 0.32, 1.4));
}

[Pixels(8, 8)]
void carve(ItemInput i, Canvas canvas)
{
    float3 from = float3(0, 3.2, 0);
    float3 ray = rayOf(canvas);
    float t = 0.05;
    bool hit = false;
    for (int k = 0; k < steps; k++)
    {
        float3 p = from + ray * t;
        float above = p.y - heightAt(p.xz, octaves);
        if (above < 0.002 * t)
        {
            hit = true;
            break;
        }
        t += max(0.004, above * 0.35);
        if (t > 60)
            break;
    }

    if (!hit)
        return;
    float3 p = from + ray * t;
    float e = 0.002 * t;
    float h = heightAt(p.xz, octaves);
    float3 normal = normalize(float3(heightAt(p.xz - float2(e, 0), octaves) - heightAt(p.xz + float2(e, 0), octaves), 2 * e,
                                     heightAt(p.xz - float2(0, e), octaves) - heightAt(p.xz + float2(0, e), octaves)));
    far[group.pixel] = t;
    facing[group.pixel] = float4(normal, h);
}

[Pixels(8, 8)]
void light(ItemInput i, Canvas canvas)
{
    float a = time * spin * TAU;
    float3 sun = normalize(float3(cos(a), 0.45 + 0.3 * sin(a * 0.5), sin(a)));
    colour sky = lerp(colours.midnightBlue, colour(0.55, 0.7, 0.95), saturate(sun.y * 1.5));
    float t = far[group.pixel];
    if (t > 1e29)
    {
        float2 uv = canvas.centred / canvas.size.y;
        shown[group.pixel] = float4(lerp(sky.rgb, sky.rgb * 0.5, saturate(-uv.y)), 1);
        return;
    }

    float4 f = facing[group.pixel];
    float3 n = f.xyz;
    colour rock = lerp(colour(0.32, 0.27, 0.22), colour(0.55, 0.5, 0.45), saturate(f.w / 2.4));
    colour ground = lerp(rock, colours.white, smoothstep(0.75, 0.9, n.y) * smoothstep(1.3, 1.8, f.w));
    float lit = saturate(dot(n, sun));
    float3 c = ground.rgb * (lit * float3(1.25, 1.1, 0.95) + sky.rgb * 0.25 * (0.5 + 0.5 * n.y));
    float fog = 1 - exp(-t * 0.045);
    shown[group.pixel] = float4(lerp(c, sky.rgb, fog), 1);
}

void graph()
{
    carve();
    light();
    present(shown);
}
