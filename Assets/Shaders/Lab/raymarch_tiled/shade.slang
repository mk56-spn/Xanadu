// Raymarcher, tiled: once a frame, `bin` bounds-checks every sphere against a
// GRID × GRID set of tiles and gives each tile the set of spheres that can show
// in it. Each pixel then tests its ray against
// just that set and marches only the spheres the ray passes near, only between
// where it enters the first and leaves the last. The same scene as raymarch_single.
import xanadu;

// Tint each tile by how many spheres it marches, and line the tiles out.
uniform bool show_tiles;

static const int SPHERES = 128;     // a multiple of 32: each tile keeps a bit per sphere in WORDS uints
static const int WORDS = SPHERES / 32;
static const float FOCAL = 1.6;     // the eye at the origin looking down +z; y down like the canvas
static const float BLEND = 0.12;    // how far the spheres melt into each other

static const int GRID = 6;          // tiles across and down: try 2, 4 and 6
static const int TILES = GRID * GRID;
static const int NEAR_RAY = 16;     // the most spheres one ray marches; past that, the ones it reaches first
static const float ASPECT = 16.0 / 9.0;   // the preview's shape; the outer tiles run on past it, so another shape is still right

/// Sphere i at time t: its centre, and its radius in w. A cloud swirling round the view's axis.
float4 sphereAt(int i, float t)
{
    float spread = frac(i * 0.618034);                    // golden-ratio steps: evenly spread, never in rows
    float a = t * (0.25 + 0.35 * frac(i * 0.37)) + i * 2.39996;
    float ring = 0.6 + 2.2 * spread;
    float y = sin(i * 1.37 + t * 0.7) * (0.4 + 0.9 * spread);
    return float4(cos(a) * ring * 1.3, y, 6.5 + sin(a) * ring, 0.10 + 0.12 * frac(i * 0.4142));
}

/// A tile edge's place across the view: column or row k of GRID, from -extent to extent.
float edge(int k, float extent) { return -extent + 2.0 * extent * k / GRID; }

/// How far the sphere's centre is past the plane through the eye and the view line at `at`
/// across `axis` (0: x, 1: y), towards the higher side; a sphere crosses it within `reach`.
float past(float3 c, float at, int axis)
{
    float3 normal = axis == 0 ? float3(FOCAL, 0, -at) : float3(0, FOCAL, -at);
    return dot(normalize(normal), c);
}

/// The scene's distance at p over the spheres the ray passes near; nearest is the closest one.
float field(float3 p, int near[NEAR_RAY], int count, inout int nearest)
{
    float d = 1e5, best = 1e5;
    for (int k = 0; k < count; k++)
    {
        float4 b = tiles[0].spheres[near[k]];
        float di = length(p - b.xyz) - b.w;
        if (di < best)
        {
            best = di;
            nearest = near[k];
        }
        d = sdf.melted(d, di, BLEND);
    }

    return d;
}

float4 shade(Canvas canvas)
{
    float2 uv = canvas.centred / (canvas.size.y * 0.5);
    colour sky = lerp(colour(0.12, 0.10, 0.20, 1.0), colour(0.04, 0.05, 0.09, 1.0), uv.y * 0.5 + 0.5);

    int col = clamp(int(floor((uv.x / ASPECT * 0.5 + 0.5) * GRID)), 0, GRID - 1);
    int row = clamp(int(floor((uv.y * 0.5 + 0.5) * GRID)), 0, GRID - 1);
    int tile = row * GRID + col;
    float3 dir = normalize(float3(uv, FOCAL));

    // The tile's spheres the ray passes within BLEND of. Past that a sphere can't touch the surface
    // the ray meets: the melt only bends a surface within BLEND of a sphere. Kept with where the
    // ray enters and leaves them, the nearest-entering first when there are too many.
    int near[NEAR_RAY];
    float enters[NEAR_RAY];
    int count = 0, inTile = 0;
    float from = 1e5, to = 0.0;
    for (int w = 0; w < WORDS; w++)
    {
        for (uint left = tiles[0].sets[tile * WORDS + w]; left != 0; left &= left - 1)
        {
            inTile++;
            int i = w * 32 + int(firstbitlow(left));
            float4 b = tiles[0].spheres[i];
            float reach = b.w + BLEND;
            float along = dot(dir, b.xyz);
            float h = along * along - dot(b.xyz, b.xyz) + reach * reach;
            if (h < 0.0)
                continue;      // the ray misses it by more than BLEND
            float half = sqrt(h);
            float enter = max(along - half, 0.0);
            to = max(to, along + half);
            if (count < NEAR_RAY)
            {
                near[count] = i;
                enters[count] = enter;
                count++;
            }
            else
            {
                // Full: swap out the one the ray reaches last, if this one comes sooner.
                int last = 0;
                for (int k = 1; k < NEAR_RAY; k++)
                    last = enters[k] > enters[last] ? k : last;
                if (enter >= enters[last])
                    continue;
                near[last] = i;
                enters[last] = enter;
            }
        }
    }

    if (show_tiles)
    {
        sky.tint(colours.white, inTile / float(SPHERES) * 2.0);
        float2 cell = frac((uv / float2(ASPECT, 1.0) * 0.5 + 0.5) * GRID);
        float2 line = min(cell, 1.0 - cell) * canvas.size / GRID;
        sky.tint(colours.white, (1.0 - saturate(min(line.x, line.y))) * 0.3);
    }
    if (count == 0)
        return sky;      // the ray passes near nothing: no marching at all

    for (int k = 0; k < count; k++)
        from = min(from, enters[k]);

    float t = from;
    int nearest = 0;
    for (int n = 0; n < 80; n++)
    {
        float3 p = dir * t;
        float d = field(p, near, count, nearest);
        if (d < 0.001)
        {
            // The normal from four more samples (a tetrahedron round p).
            const float2 e = float2(1, -1) * 0.001;
            int ignored = 0;
            float3 normal = normalize(e.xyy * field(p + e.xyy, near, count, ignored) + e.yyx * field(p + e.yyx, near, count, ignored)
                                    + e.yxy * field(p + e.yxy, near, count, ignored) + e.xxx * field(p + e.xxx, near, count, ignored));
            float light = saturate(dot(normal, normalize(float3(-0.6, -0.8, -0.4))));
            float rim = pow(1.0 - saturate(-dot(normal, dir)), 3.0);
            colour c = colours.hue(nearest / float(SPHERES));
            c.brighten(0.15 + 0.85 * light);
            c.tint(colours.white, rim * 0.5);
            return c;
        }

        t += d;
        if (t > to)
            break;
    }

    return sky;
}
