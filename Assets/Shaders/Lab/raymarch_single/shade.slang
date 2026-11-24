// Raymarcher, single pass: every pixel works out where the spheres are and
// marches all of them. The same scene as raymarch_tiled.
import xanadu;

static const int SPHERES = 128;
static const float FOCAL = 1.6;     // the eye at the origin looking down +z; y down like the canvas
static const float BLEND = 0.12;    // how far the spheres melt into each other

/// Sphere i at time t: its centre, and its radius in w. A cloud swirling round the view's axis.
float4 sphereAt(int i, float t)
{
    float spread = frac(i * 0.618034);                    // golden-ratio steps: evenly spread, never in rows
    float a = t * (0.25 + 0.35 * frac(i * 0.37)) + i * 2.39996;
    float ring = 0.6 + 2.2 * spread;
    float y = sin(i * 1.37 + t * 0.7) * (0.4 + 0.9 * spread);
    return float4(cos(a) * ring * 1.3, y, 6.5 + sin(a) * ring, 0.10 + 0.12 * frac(i * 0.4142));
}

/// The scene's distance at p over every sphere; nearest is the closest one.
float field(float3 p, float4 spheres[SPHERES], inout int nearest)
{
    float d = 1e5, best = 1e5;
    for (int i = 0; i < SPHERES; i++)
    {
        float4 b = spheres[i];
        float di = length(p - b.xyz) - b.w;
        if (di < best)
        {
            best = di;
            nearest = i;
        }
        d = sdf.melted(d, di, BLEND);
    }

    return d;
}

float4 shade(Canvas canvas)
{
    float2 uv = canvas.centred / (canvas.size.y * 0.5);
    colour sky = lerp(colour(0.12, 0.10, 0.20, 1.0), colour(0.04, 0.05, 0.09, 1.0), uv.y * 0.5 + 0.5);

    float4 spheres[SPHERES];
    for (int i = 0; i < SPHERES; i++)
        spheres[i] = sphereAt(i, time);

    float3 dir = normalize(float3(uv, FOCAL));
    float t = 0.0;
    int nearest = 0;
    for (int n = 0; n < 80; n++)
    {
        float3 p = dir * t;
        float d = field(p, spheres, nearest);
        if (d < 0.001)
        {
            // The normal from four more samples (a tetrahedron round p).
            const float2 e = float2(1, -1) * 0.001;
            int ignored = 0;
            float3 normal = normalize(e.xyy * field(p + e.xyy, spheres, ignored) + e.yyx * field(p + e.yyx, spheres, ignored)
                                    + e.yxy * field(p + e.yxy, spheres, ignored) + e.xxx * field(p + e.xxx, spheres, ignored));
            float light = saturate(dot(normal, normalize(float3(-0.6, -0.8, -0.4))));
            float rim = pow(1.0 - saturate(-dot(normal, dir)), 3.0);
            colour c = colours.hue(nearest / float(SPHERES));
            c.brighten(0.15 + 0.85 * light);
            c.tint(colours.white, rim * 0.5);
            return c;
        }

        t += d;
        if (t > 20.0)
            break;
    }

    return sky;
}
