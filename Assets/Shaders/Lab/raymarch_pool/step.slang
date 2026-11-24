// Raymarcher, pooled, on xanadu.march: the scene of raymarch_single and raymarch_tiled,
// with no tiles. Each sphere is an item. STEP puts it where it is this frame and keeps the
// spheres it melts into, nearest first; DEFORM draws the box it takes on screen; SHADE
// marches only through it, against it and those. A pixel no sphere covers does no work.
import xanadu;

static const int SPHERES = 128;
static const float SCALE = 80;      // pixels a unit of the old scene
static const float BLEND = 0.12 * SCALE;    // how far the spheres melt into each other, in pixels

static const int NEAR = 8;          // the most neighbours a sphere melts with: the nearest ones

/// The eye, 520 px back from the cloud's middle, with a 300 px lens: the old scene's framing.
Camera camera() { return Camera(520, 300); }

struct Sphere
{
    float4 ball;            // centre, and radius in w
    float4 near[NEAR];      // the spheres it melts into, nearest first, as balls
    uint ids[NEAR];         // and which spheres those are, for their colour
    uint index;
    uint melts;             // how many of near are kept
};

/// Sphere i at time t, in pixels round the cloud's middle: its centre, and its radius in w.
/// A cloud swirling round the view's axis.
float4 sphereAt(int i, float t)
{
    float spread = frac(i * 0.618034);                    // golden-ratio steps: evenly spread, never in rows
    float a = t * (0.25 + 0.35 * frac(i * 0.37)) + i * 2.39996;
    float ring = 0.6 + 2.2 * spread;
    float y = sin(i * 1.37 + t * 0.7) * (0.4 + 0.9 * spread);
    return float4(cos(a) * ring * 1.3, y, sin(a) * ring, 0.10 + 0.12 * frac(i * 0.4142)) * SCALE;
}

/// A sphere's own colour.
colour hueOf(uint index) { return colours.hue(index / float(SPHERES)); }

Sphere start(uint i)
{
    Sphere s = {};
    s.index = i;
    s.ball = sphereAt(int(i), time);
    return s;
}

/// Where it is now, and the spheres close enough to melt into it (surfaces within twice
/// BLEND), the nearest NEAR of them. The motion is a function of time, so each sphere works
/// out the others itself, once a frame, and no pixel has to.
[Pool(128)]     // SPHERES, written as a number: the engine reads it
void step(inout Sphere s, uint i, float dt)
{
    s.ball = sphereAt(int(i), time);
    float gaps[NEAR];
    uint kept = 0;
    for (int j = 0; j < SPHERES; j++)
    {
        if (j == int(i))
            continue;
        float4 o = sphereAt(j, time);
        float gap = distance(o.xyz, s.ball.xyz) - o.w - s.ball.w;
        if (gap >= 2 * BLEND)
            continue;
        // Into the nearest-first list, dropping the farthest when full.
        int at = int(kept);
        if (kept < NEAR)
            kept++;
        else if (gap >= gaps[NEAR - 1])
            continue;
        else
            at = NEAR - 1;
        for (; at > 0 && gaps[at - 1] > gap; at--)
        {
            gaps[at] = gaps[at - 1];
            s.near[at] = s.near[at - 1];
            s.ids[at] = s.ids[at - 1];
        }
        gaps[at] = gap;
        s.near[at] = o;
        s.ids[at] = uint(j);
    }
    s.melts = kept;
}
