// PARTICLES: a particle show on the spawner. Every particle is worked out from
// scratch each frame from its number and the time (no state), so anything here
// can be scrubbed, slowed, reversed or changed while it runs.
//
// Eight scenes, cycled with a dissolve between them (or pinned with `scene`):
//   0 fireworks   rockets, six shell types (peony, ring, willow, heart, glitter, double)
//   1 galaxy      three arms, a bulge, glowing knots, a tilted disc, twinkling sky
//   2 black hole  a lensed accretion disc, a photon ring, jets, doppler brightening
//   3 flow        a curl-noise field the particles ride, hue from their heading
//   4 fountain    three swaying jets that splash and bounce off the floor
//   5 fire        a flame, embers flying off it, smoke above
//   6 swarm       a cloud flowing along curves that morph into each other
//   7 warp        hyperspace: stars rushing past from a wandering vanishing point
//
// The pointer pushes particles about (mouseMode). Every particle also leaves
// `trail` copies of itself back in time, and fast ones stretch into streaks.
import xanadu;

// ---- knobs ------------------------------------------------------------------

static const int scene = -1;              // -1 cycles them all; 0..7 pins one
static const float sceneLength = 14;      // seconds per scene when cycling
static const float blendLength = 3;       // seconds the dissolve into the next takes
static const float timeScale = 1;         // 0.25 slow motion, -1 runs it backwards

static const uint particles = 48000;      // particles per scene (count = particles × trail, 1M at most)
static const uint trail = 6;              // copies per particle, each further back in time (1 = no trail)
static const float trailStep = 0.016;     // seconds between those copies
static const bool streaks = true;         // fast particles stretch along their velocity
static const float streakTime = 0.035;    // a streak is this many seconds of travel long
static const float sizeScale = 1;         // every particle bigger or smaller

static const float bpm = 124;             // the beat everything pulses to
static const float pulse = 0.35;          // how hard (0 off)

static const int mouseMode = 1;           // 0 off, 1 repel, 2 attract, 3 swirl, 4 lens
static const float mouseRadius = 120;     // how far the pointer reaches (px on a stage 900 tall)
static const float mouseStrength = 70;    // how far it pushes (px on a stage 900 tall; radians×60 for swirl)
static const float mouseGlow = 1.2;       // particles near the pointer brighten by this

static const int paletteMode = 0;         // 0 each scene's own, 1 rainbow, 2 fire, 3 ice, 4 neon, 5 white

static const bool additive = true;
static const uint count = particles * trail;

// ---- what shade draws -------------------------------------------------------

static const int GLOW = 0;      // a soft dot with a hot core
static const int STREAK = 1;    // a capsule along the velocity, bright at the head
static const int SPARKLE = 2;   // a four-point twinkle
static const int RING = 3;      // a thin ring
static const int STAR = 4;      // a six-point star
static const int SOFT = 5;      // a big gaussian puff (smoke, mist, nebula)

static const int SCENES = 8;

/// One particle as a scene sees it, before the trail, beat, pointer and palette.
struct Particle
{
    float2 position;    // px from the centre
    float2 velocity;    // px per second, when hasVelocity
    bool hasVelocity;   // false: worked out from a moment earlier
    float size;         // px across (a streak's thickness)
    float3 rgb;
    float alpha;
    int kind;
    float spin;         // radians, for shapes that aren't round
    float tone;         // 0..1, what the palettes colour by
    float heat;         // 0..1, how much the core whitens
    bool solo;          // no trail copies (flashes, puffs)
    bool hidden;

    __init()
    {
        position = float2(0, 0);
        velocity = float2(0, 0);
        hasVelocity = false;
        size = 3;
        rgb = float3(1, 1, 1);
        alpha = 1;
        kind = GLOW;
        spin = 0;
        tone = 0;
        heat = 0.5;
        solo = false;
        hidden = false;
    }
};

// ---- small helpers ----------------------------------------------------------

float rnd(uint id, float salt) { return random(float2(float(id), salt)); }
float3 rnd3(uint id, float salt) { return random3(float2(float(id), salt)); }

/// Roughly normal (mean 0, spread about 1), from three uniforms.
float gauss(uint id, float salt)
{
    float3 r = rnd3(id, salt);
    return (r.x + r.y + r.z - 1.5) * 2;
}

/// The stage's shorter side: everything is sized from it, so the show fits any preview.
float span() { return min(resolution.x, resolution.y); }

/// px for a stage 900 px tall.
float px(float x) { return x * span() / 900; }

/// Black body-ish: 0 cold dark red, through red, orange, yellow, to 1 white.
float3 fireColour(float h)
{
    h = saturate(h);
    float3 c = lerp(float3(0.15, 0.01, 0.0), float3(0.9, 0.12, 0.02), saturate(h * 3));
    c = lerp(c, float3(1.0, 0.5, 0.08), saturate(h * 3 - 1));
    c = lerp(c, float3(1.0, 0.92, 0.6), saturate(h * 3 - 2));
    return c;
}

float3 hueRgb(float h, float s = 0.85) { return colours.hsv(frac(h), s, 1).rgb; }

// ---- 0 fireworks ------------------------------------------------------------

Particle fireworks(uint id, float T)
{
    Particle o = Particle();
    const uint shells = 16;
    uint shell = id % shells;
    uint spark = id / shells;
    const float period = 3.8;

    float t = T + rnd(shell, 1) * period;
    float cycle = floor(t / period);
    float age = t - cycle * period;
    float2 seed = float2(float(shell), cycle);
    float3 r = random3(seed);

    float2 start = float2((r.x - 0.5) * resolution.x * 0.75, resolution.y * 0.55);
    float2 apex = float2(start.x + (r.y - 0.5) * resolution.x * 0.2, -resolution.y * lerp(0.02, 0.3, r.z));
    float flight = lerp(0.9, 1.3, random(seed + 0.5));
    int type = int(random(seed + 1.5) * 6);
    float hue = random(seed + 2.5);
    float hue2 = frac(hue + 0.3 + 0.4 * random(seed + 3.5));

    // Going up: the head, a soft glow round it, and the trail behind does the rest.
    if (age < flight)
    {
        if (spark >= 2)
        {
            o.hidden = true;
            return o;
        }
        float a = age / flight;
        float rise = 1 - (1 - a) * (1 - a);
        o.position = float2(lerp(start.x, apex.x, a) + sin(age * 30 + shell) * px(2), lerp(start.y, apex.y, rise));
        o.rgb = float3(1, 0.7, 0.35);
        o.tone = 0.08;
        o.heat = 1;
        o.size = spark == 0 ? px(5) : px(26);
        o.alpha = spark == 0 ? 1 : 0.25;
        o.kind = spark == 0 ? GLOW : SOFT;
        o.solo = spark == 1;
        return o;
    }

    float e = age - flight;

    // One flash per burst.
    if (spark == 0)
    {
        o.solo = true;
        o.hidden = e > 0.4;
        o.position = apex;
        o.kind = SOFT;
        o.size = px(320) * (0.6 + e);
        o.alpha = pow(saturate(1 - e / 0.4), 2) * 0.45;
        o.rgb = lerp(float3(1, 1, 1), hueRgb(hue), 0.5);
        o.tone = hue;
        return o;
    }

    float3 s = rnd3(id, 7);
    float angle = s.x * TAU;
    float2 direction = float2(cos(angle), sin(angle));
    float power = px(lerp(330, 480, r.x));
    // A sphere seen from the side: most sparks fly partly towards or away from you.
    float z = s.y * 2 - 1;
    float speed = power * sqrt(1 - z * z);
    float drag = 1.7;
    float gravity = px(200);
    float life = lerp(1.3, 2.1, s.z);
    float3 rgb = hueRgb(hue);
    float tone = hue;
    bool glitter = false;
    o.kind = GLOW;

    if (type == 1)          // ring, tipped towards you
    {
        speed = power * 0.9;
        direction.y *= lerp(0.25, 1, r.y);
        direction = rotate(direction, r.z * PI);
    }
    else if (type == 2)     // willow: gold, heavy, slow to die, crackling
    {
        drag = 2.6;
        gravity *= 1.7;
        life *= 1.9;
        rgb = float3(1, 0.72, 0.3);
        tone = 0.1;
        glitter = true;
    }
    else if (type == 3)     // heart
    {
        float a = s.x * TAU;
        float hx = 16 * pow(sin(a), 3);
        float hy = -(13 * cos(a) - 5 * cos(2 * a) - 2 * cos(3 * a) - cos(4 * a));
        direction = float2(hx, hy) / 17;
        speed = power * 0.75 * lerp(0.92, 1, s.y);
        rgb = lerp(float3(1, 0.2, 0.45), float3(1, 0.6, 0.8), s.y);
        tone = 0.95;
        gravity *= 0.5;
    }
    else if (type == 4)     // glitter: white sparkles that strobe
    {
        rgb = lerp(float3(1, 1, 1), rgb, 0.3);
        glitter = true;
        o.kind = SPARKLE;
    }
    else if (type == 5)     // two shells, an inner one in another colour
    {
        if (s.y < 0.45)
        {
            speed *= 0.5;
            rgb = hueRgb(hue2);
            tone = hue2;
        }
    }

    float spread = (1 - exp(-drag * e)) / drag;
    o.position = apex + direction * speed * spread + float2(0, gravity / drag * (e - spread));
    o.velocity = direction * speed * exp(-drag * e) + float2(0, gravity / drag * (1 - exp(-drag * e)));
    o.hasVelocity = true;

    float left = saturate(1 - e / life);
    o.hidden = e > life;
    o.alpha = pow(left, 1.5) * 0.55;
    if (glitter)
        o.alpha *= step(0.45, rnd(id, floor(T * 24))) * 1.6;
    // White hot as it bursts, its colour a moment later.
    o.rgb = lerp(float3(1, 1, 1), rgb, saturate(e * 5));
    o.tone = tone;
    o.heat = left;
    o.size = px(lerp(2.5, 4.5, s.z)) * (0.5 + 0.5 * left);
    o.spin = T * 3 + s.x * TAU;
    if (o.kind == GLOW)
        o.kind = STREAK;
    return o;
}

// ---- 1 galaxy ---------------------------------------------------------------

Particle galaxy(uint id, float T)
{
    Particle o = Particle();
    float3 r = rnd3(id, 11);
    float group = rnd(id, 12);
    float R = span() * 0.47;
    float spin = T * 0.1;
    float2 flat = float2(0, 0);

    if (group > 0.92)       // the sky behind: twinkling stars, not in the disc
    {
        o.position = (r.xy - 0.5) * resolution * 1.05;
        float twinkle = sin01(T * lerp(1.5, 5, r.z) + r.x * 40);
        o.alpha = lerp(0.15, 0.9, twinkle * twinkle);
        o.size = px(lerp(1.5, 3, r.z * r.z));
        o.rgb = lerp(float3(0.7, 0.8, 1), float3(1, 0.9, 0.8), r.y);
        o.tone = r.y;
        if (r.z > 0.93)
        {
            o.kind = SPARKLE;
            o.size = px(18) * (0.4 + twinkle);
            o.spin = 0.4;
        }
        return o;
    }

    if (group < 0.2)        // the bulge: a warm gaussian heap
    {
        float rad = R * 0.13 * sqrt(-2 * log(max(r.x, 1e-4)));
        float a = r.y * TAU + spin * 2.2;
        flat = rad * float2(cos(a), sin(a));
        o.rgb = lerp(float3(1, 0.95, 0.85), float3(1, 0.75, 0.45), r.z);
        o.size = px(lerp(2, 3.5, r.z));
        o.alpha = 0.6;
        o.tone = 0.1 * r.z;
        o.heat = 1;
    }
    else                    // three arms, wound by 4 radians, scattered more further out
    {
        const int arms = 3;
        int arm = int(id % arms);
        float along = pow(r.x, 0.75);
        float rad = R * (0.07 + 0.93 * along);
        float scatter = gauss(id, 13) * 0.22 * (0.4 + along);
        float a = float(arm) * TAU / arms + along * 4.2 + scatter + spin;
        flat = rad * float2(cos(a), sin(a)) + float2(gauss(id, 14), gauss(id, 15)) * px(6);
        o.rgb = lerp(float3(1, 0.85, 0.6), float3(0.45, 0.6, 1), saturate(along * 1.4));
        o.tone = 0.15 + along * 0.6;
        o.size = px(lerp(1.5, 3.2, r.z));
        o.alpha = lerp(0.9, 0.5, along);

        // Star-forming knots strung along the arms: big pink puffs.
        if (r.y > 0.996)
        {
            o.kind = SOFT;
            o.solo = true;
            o.rgb = float3(1, 0.35, 0.6);
            o.tone = 0.9;
            o.size = px(lerp(16, 34, r.z));
            o.alpha = 0.12;
        }
        // Young blue giants.
        else if (r.y > 0.99)
        {
            o.kind = STAR;
            o.rgb = float3(0.7, 0.85, 1);
            o.size = px(9);
            o.spin = r.z * TAU + T;
        }
    }

    // Seen at a slant that rocks slowly, then turned on the page.
    float tilt = 1.0 + 0.2 * sin(T * 0.13);
    flat.y *= cos(tilt);
    o.position = rotate(flat, 0.4 + 0.15 * sin(T * 0.07));
    return o;
}

// ---- 2 black hole -----------------------------------------------------------

Particle blackHole(uint id, float T)
{
    Particle o = Particle();
    float2 centre = float2(sin(T * 0.21), sin(T * 0.17) * 0.5) * resolution * 0.12;
    float horizon = span() * 0.055;
    float group = rnd(id, 21);
    float3 r = rnd3(id, 22);
    const float squash = 0.28;   // how flat the disc looks
    const float lean = -0.18;    // its tilt on the page

    if (group < 0.07)            // jets, up and down the spin axis
    {
        float a = frac(T / 1.4 + r.x);
        float side = r.y < 0.5 ? -1 : 1;
        float out = horizon * 1.1 + pow(a, 1.3) * span() * 0.65;
        float wobble = gauss(id, 24) * a * px(30) + sin(T * 3 + a * 9) * px(6) * a;
        o.position = centre + rotate(float2(wobble, side * out), lean);
        o.rgb = lerp(float3(0.55, 0.65, 1), float3(0.85, 0.5, 1), r.z);
        o.tone = 0.6 + r.z * 0.2;
        o.alpha = (1 - a) * smoothstep(0, 0.05, a) * 0.3;
        o.size = px(lerp(1.5, 3, r.z)) * (1 + a);
        o.kind = STREAK;
        return o;
    }

    if (group < 0.13)            // the photon ring: light bent all the way round, seen face on
    {
        float a = r.x * TAU + T * 4;
        float rad = horizon * lerp(1.45, 1.6, r.y);
        o.position = centre + rad * float2(cos(a), sin(a));
        o.velocity = rad * 4 * float2(-sin(a), cos(a));
        o.hasVelocity = true;
        o.rgb = float3(1, 0.85, 0.6);
        o.tone = 0.12;
        o.heat = 1;
        o.alpha = 0.55;
        o.size = px(1.8);
        o.kind = STREAK;
        return o;
    }

    // The disc: each particle spirals in from r0 and is born again outside.
    float life = lerp(5, 9, r.x);
    float a = frac(T / life + r.y);
    float r0 = span() * lerp(0.2, 0.55, r.z);
    float theta = rnd(id, 23) * TAU;
    // Turned by ∫ω dt, ω ~ rad^-1.5 (Kepler), summed in a few steps.
    const int steps = 8;
    float rad = r0;
    for (int k = 0; k < steps; k++)
    {
        float m = (float(k) + 0.5) / steps * a;
        float rk = lerp(r0, horizon, pow(m, 1.6));
        theta += 2.2 * pow(horizon * 2.5 / rk, 1.5) * (a * life / steps);
    }
    rad = lerp(r0, horizon, pow(a, 1.6));

    float2 disc = rad * float2(cos(theta), sin(theta));
    bool behind = disc.y < 0;
    float2 screen = float2(disc.x, disc.y * squash);
    // Lensing: the far side of the disc is bent up over the top of the hole.
    if (behind)
        screen.y -= horizon * 1.7 * exp(-pow(disc.x / (horizon * 2.4), 2)) * saturate(horizon * 4 / rad);
    o.position = centre + rotate(screen, lean);
    o.hidden = rad < horizon * 1.08 || (behind && length(screen) < horizon);

    float hot = saturate(horizon * 1.6 / rad);
    // The side coming towards you is brighter.
    float doppler = 1 + 0.9 * -sin(theta + PI / 2);
    o.rgb = lerp(float3(1, 0.42, 0.1), float3(0.85, 0.92, 1), hot * hot);
    o.tone = lerp(0.05, 0.6, hot);
    o.heat = hot;
    o.alpha = smoothstep(0, 0.08, a) * lerp(0.35, 1, hot) * doppler * 0.7;
    o.size = px(lerp(2, 3.2, r.x));
    o.kind = STREAK;
    return o;
}

// ---- 3 flow -----------------------------------------------------------------

/// Curl of a few travelling sine waves, warped: a field with no sources or sinks,
/// so particles swirl without bunching up. px per second.
float2 flowAt(float2 p, float T)
{
    float2 q = p / span() * 3.2;
    q += 0.45 * sin(q.yx * float2(1.7, 1.3) + T * float2(0.3, 0.23));
    float2 gradient = float2(0, 0);
    for (int w = 0; w < 5; w++)
    {
        float f = 1 + w * 0.7;
        float a = w * 2.4 + T * 0.05 * (w % 2 == 0 ? 1 : -1);
        float2 k = float2(cos(a), sin(a)) * f;
        gradient += cos(dot(q, k) + T * (0.4 + 0.15 * w) + w) * k / f;
    }
    return float2(gradient.y, -gradient.x) * px(85);
}

Particle flow(uint id, float T)
{
    Particle o = Particle();
    float3 r = rnd3(id, 31);
    float life = lerp(2.5, 4.5, r.x);
    float phase = (T + r.y * life) / life;
    float age = frac(phase) * life;
    float born = T - age;
    float2 p = (random2(float2(float(id), floor(phase) + 0.5)) - 0.5) * resolution * 1.15;

    const int steps = 12;
    float h = age / steps;
    for (int k = 0; k < steps; k++)
        p += flowAt(p, born + (k + 0.5) * h) * h;

    o.position = p;
    o.velocity = flowAt(p, T);
    o.hasVelocity = true;
    float heading = atan2(o.velocity.y, o.velocity.x) / TAU + 0.5;
    o.tone = frac(heading + T * 0.03);
    o.rgb = colours.rainbow(o.tone, 0.6, 0.4).rgb;
    o.alpha = sin(PI * age / life) * 0.75;
    o.size = px(lerp(1.5, 3, r.z));
    o.kind = STREAK;
    return o;
}

// ---- 4 fountain -------------------------------------------------------------

Particle fountain(uint id, float T)
{
    Particle o = Particle();
    int nozzle = int(id % 3);
    float3 r = rnd3(id, 41);
    float floorY = resolution.y * 0.42;
    float2 from = float2((nozzle - 1) * resolution.x * 0.28, floorY);
    const float life = 2.8;
    float age = frac(T / life + r.x) * life;
    float born = T - age;
    float beat = frac(born * bpm / 60);

    if (r.y < 0.08)         // mist round each basin
    {
        float a = age / life;
        o.position = from + float2((r.z - 0.5) * px(260) * (0.3 + a), -px(30) * a - px(8));
        o.kind = SOFT;
        o.solo = true;
        o.size = px(lerp(30, 70, a));
        o.alpha = sin(PI * a) * 0.012;
        o.rgb = float3(0.6, 0.8, 1);
        o.tone = 0.55;
        return o;
    }

    float sway = sin(born * 0.8 + nozzle * 2.1) * 0.28;
    float angle = -PI / 2 + sway + (r.z - 0.5) * 0.14;
    float speed = span() * lerp(1.05, 1.2, r.y) * (1 + 0.12 * sin(born * 2.3 + nozzle) + pulse * 0.3 * exp(-beat * 6));
    float gravity = span() * 1.35;
    float2 v = float2(cos(angle), sin(angle)) * speed;
    float2 p = from;
    float t = age;
    int bounces = 0;
    for (int b = 0; b < 4; b++)
    {
        float hit = -2 * v.y / gravity;   // back to the floor from it
        if (t < hit || b == 3)
        {
            p += float2(v.x * t, v.y * t + 0.5 * gravity * t * t);
            v.y += gravity * t;
            break;
        }
        p.x += v.x * hit;
        t -= hit;
        v.y *= 0.38;                      // lands as fast as it left, goes up 0.38 as fast
        v.x *= 0.65;
        bounces++;
    }

    o.position = p;
    o.velocity = v;
    o.hasVelocity = true;
    o.rgb = lerp(float3(0.15, 0.45, 1), float3(0.7, 0.95, 1), saturate(r.z + bounces * 0.3));
    o.tone = lerp(0.5, 0.62, r.z);
    o.heat = bounces > 0 ? 0.8 : 0.3;
    o.alpha = smoothstep(life, life * 0.7, age) * 0.2;
    o.size = px(lerp(2, 3.5, r.z)) * (bounces > 0 ? 0.8 : 1);
    o.kind = STREAK;
    return o;
}

// ---- 5 fire -----------------------------------------------------------------

Particle fire(uint id, float T)
{
    Particle o = Particle();
    float3 r = rnd3(id, 51);
    float group = rnd(id, 52);
    float baseY = resolution.y * 0.42;
    float wind = sin(T * 0.35) * px(60) + sin(T * 1.1) * px(15);

    if (group < 0.12)       // embers thrown up and off
    {
        const float life = 2.8;
        float age = frac(T / life + r.x) * life;
        float2 v = float2((r.y - 0.5) * px(260), -px(lerp(300, 520, r.z)));
        float2 p = float2(gauss(id, 53) * px(70), baseY) + v * age + float2(0, px(110) * age * age);
        p.x += wind * age + sin(age * 7 + r.x * 30) * px(14) * age;
        o.position = p;
        o.velocity = v + float2(wind + cos(age * 7 + r.x * 30) * px(98) * age, px(220) * age);
        o.hasVelocity = true;
        float left = 1 - age / life;
        o.heat = left;
        o.rgb = fireColour(0.55 + 0.45 * left);
        o.tone = 0.12 * left;
        o.alpha = left * (0.6 + 0.4 * step(0.3, rnd(id, floor(T * 16))));
        o.size = px(lerp(1.5, 3, r.z));
        o.kind = STREAK;
        return o;
    }

    if (group < 0.22)       // smoke, curling above the flame
    {
        const float life = 4;
        float age = frac(T / life + r.x) * life;
        float a = age / life;
        o.position = float2(gauss(id, 54) * px(40) + wind * age * 0.8 + sin(age * 1.5 + r.y * 9) * px(30) * a,
            baseY - px(260) - age * px(110));
        o.kind = SOFT;
        o.solo = true;
        o.size = px(lerp(40, 140, a));
        o.alpha = sin(PI * a) * 0.008;
        o.rgb = float3(0.55, 0.5, 0.5);
        o.tone = 0.02;
        return o;
    }

    // The flame: puffs that rise, narrow to a tip and cool.
    float life = lerp(0.9, 1.6, r.x);
    float age = frac(T / life + r.y) * life;
    float a = age / life;
    float x0 = gauss(id, 55) * px(85);
    float lick = sin(T * 6 + x0 * 0.03) * px(18) * a + sin(T * 11 + r.z * 6) * px(8) * a;
    o.position = float2(x0 * (1 - 0.75 * a) + lick + wind * a * a, baseY - age * px(lerp(260, 380, r.z)) - a * a * px(80));
    float heat = (1 - a) * (1 - saturate(abs(x0) / px(170)) * 0.5);
    o.heat = heat;
    o.rgb = fireColour(heat * 1.1);
    o.tone = heat * 0.15;
    o.alpha = smoothstep(0, 0.12, a) * (1 - a) * 0.1;
    o.size = px(lerp(14, 40, a)) * lerp(0.7, 1.2, r.z);
    o.kind = GLOW;
    return o;
}

// ---- 6 swarm ----------------------------------------------------------------

/// Curve number k at u (0..1 round it), about ±0.45 across.
float2 curveAt(int k, float u, float T)
{
    float a = u * TAU;
    switch (k % 6)
    {
    case 0: // a five-petal rose, turning
    {
        float rad = 0.45 * cos(5 * a);
        return rotate(rad * float2(cos(a), sin(a)), T * 0.2);
    }
    case 1: // Lissajous 3:4, the phase drifting
        return 0.42 * float2(sin(3 * a + T * 0.4), sin(4 * a));
    case 2: // a hypotrochoid (spirograph), three loops round
    {
        float b = a * 3;
        float2 p = 2 * float2(cos(b), sin(b)) + 1.6 * float2(cos(b * 5.0 / 3), -sin(b * 5.0 / 3));
        return p * 0.12;
    }
    case 3: // a heart
    {
        float2 p = float2(16 * pow(sin(a), 3), -(13 * cos(a) - 5 * cos(2 * a) - 2 * cos(3 * a) - cos(4 * a)));
        return p / 38;
    }
    case 4: // the lemniscate, ∞
        return 0.5 * float2(cos(a), sin(a) * cos(a)) / (1 + sin(a) * sin(a));
    default: // a five-point star polygon
    {
        float b = floor(u * 5);
        float f = frac(u * 5);
        float2 p0 = float2(sin(b * 4 * PI / 5), -cos(b * 4 * PI / 5));
        float2 p1 = float2(sin((b + 1) * 4 * PI / 5), -cos((b + 1) * 4 * PI / 5));
        return lerp(p0, p1, f) * 0.45;
    }
    }
}

Particle swarm(uint id, float T)
{
    Particle o = Particle();
    float3 r = rnd3(id, 61);
    float u = frac(float(id) / particles + T * 0.025);
    float stage = T / 4.5;
    int k = int(floor(stage));
    float morph = ease.inOutCubic(saturate((frac(stage) - 0.55) / 0.45));
    float2 p = lerp(curveAt(k, u, T), curveAt(k + 1, u, T), morph) * span();

    // Each particle circles its spot on the curve; the cloud breathes with the beat.
    float beat = frac(T * bpm / 60);
    float puff = 1 + pulse * 2 * exp(-beat * 5);
    float orbit = r.x * TAU + T * lerp(1, 4, r.y);
    p += float2(cos(orbit), sin(orbit)) * px(lerp(2, 22, r.z * r.z)) * puff;

    o.position = p;
    o.tone = frac(u + T * 0.07);
    o.rgb = colours.rainbow(o.tone, 0.6, 0.4).rgb;
    o.alpha = 0.55;
    o.size = px(lerp(2, 3.5, r.y));
    o.kind = r.z > 0.985 ? STAR : GLOW;
    o.spin = T * 2 + r.x * TAU;
    if (r.z > 0.985)
        o.size = px(10);
    return o;
}

// ---- 7 warp -----------------------------------------------------------------

Particle warp(uint id, float T)
{
    Particle o = Particle();
    float3 r = rnd3(id, 71);
    float rush = 0.35;
    float depth = frac(-T * rush + r.x);           // 1 far, 0 at your face
    float z = lerp(0.03, 1, depth);
    float a = r.y * TAU;
    float spread = lerp(0.05, 1, sqrt(r.z)) * span() * 0.18;
    float2 vanish = float2(sin(T * 0.37), cos(T * 0.29)) * span() * 0.12;
    float2 direction = float2(cos(a), sin(a));

    o.position = vanish + direction * spread / z;
    // d/dt (spread / z) with dz/dt = -rush × 0.97.
    o.velocity = direction * spread * rush * 0.97 / (z * z);
    o.hasVelocity = true;
    o.alpha = smoothstep(1, 0.7, depth) * 0.3;
    o.size = px(lerp(1.5, 3.5, 1 - z));
    bool tinted = rnd(id, 72) < 0.1;
    o.rgb = tinted ? hueRgb(0.5 + rnd(id, 73) * 0.35, 0.6) : float3(0.8, 0.88, 1);
    o.tone = tinted ? 0.7 : 0.55;
    o.heat = 1 - z;
    o.kind = STREAK;
    return o;
}

// ---- putting it together ----------------------------------------------------

Particle sceneAt(int which, uint id, float T)
{
    switch (which)
    {
    case 0: return fireworks(id, T);
    case 1: return galaxy(id, T);
    case 2: return blackHole(id, T);
    case 3: return flow(id, T);
    case 4: return fountain(id, T);
    case 5: return fire(id, T);
    case 6: return swarm(id, T);
    default: return warp(id, T);
    }
}

/// Which scene particle id is in at T, and how far faded in (the dissolve: each
/// particle crosses over at its own moment in the last blendLength seconds).
int sceneOf(uint id, float T, out float fade)
{
    fade = 1;
    if (scene >= 0)
        return scene;
    float t = T / sceneLength;
    int s = int(floor(t));
    float into = (t - s) * sceneLength;
    float crossAt = sceneLength - blendLength * (1 - rnd(id, 99));
    fade = saturate(abs(into - crossAt) / 0.35);
    int which = into > crossAt ? s + 1 : s;
    return ((which % SCENES) + SCENES) % SCENES;
}

/// The pointer's push on a particle.
float2 pushed(float2 p, out float near)
{
    float2 d = p - mouse;
    float dist = length(d) + 1e-3;
    float reach = px(mouseRadius), strength = px(mouseStrength);
    near = exp(-pow(dist / reach, 2));
    switch (mouseMode)
    {
    case 1: return p + d / dist * strength * near;
    case 2: return p - d / dist * min(strength * near, dist * 0.9);
    case 3: return mouse + rotate(d, mouseStrength / 60 * near);
    case 4: return mouse + d * (1 + near * mouseStrength / 90);
    default: near = 0; return p;
    }
}

float3 paletted(Particle p)
{
    switch (paletteMode)
    {
    case 1: return colours.rainbow(p.tone).rgb;
    case 2: return fireColour(lerp(0.35, 1, p.heat));
    case 3: return lerp(float3(0.1, 0.3, 1), float3(0.85, 1, 1), p.heat * 0.6 + p.tone * 0.4);
    case 4: return colours.palette(p.tone, float3(0.5), float3(0.5), float3(2, 1, 0), float3(0.5, 0.2, 0.25)).rgb;
    case 5: return float3(1, 1, 1);
    default: return p.rgb;
    }
}

Thing spawn(uint i)
{
    Thing t;
    uint id = i / trail;
    uint copy = i % trail;
    float T = time * timeScale - copy * trailStep * sign(timeScale);

    float fade = 1;
    int which = sceneOf(id, T, fade);
    Particle p = sceneAt(which, id, T);
    if (p.hidden || (copy > 0 && p.solo))
    {
        t.hidden = true;
        return t;
    }

    float2 velocity = p.velocity;
    if (!p.hasVelocity)
    {
        const float h = 1.0 / 120;
        velocity = (p.position - sceneAt(which, id, T - h).position) / h;
    }
    velocity *= timeScale;

    float near = 0;
    float2 position = pushed(p.position, near);

    float beat = frac(T * bpm / 60);
    float kick = pulse * exp(-beat * 7);
    float back = 1 - float(copy) / trail;
    float alpha = p.alpha * fade * pow(back, 1.6) * (1 + kick * 0.6) * (1 + near * mouseGlow);
    float size = p.size * sizeScale * (1 + kick * 0.5) * lerp(0.4, 1, back) * (mouseMode == 4 ? 1 + near : 1);

    int kind = p.kind;
    t.position = position;
    t.rotation = p.spin;
    t.size = float2(size, size);
    if (kind == STREAK)
    {
        float speed = length(velocity);
        if (streaks && speed * streakTime > size)
        {
            t.size = float2(min(speed * streakTime, px(160)), size);
            t.rotation = atan2(velocity.y, velocity.x);
        }
        else
            kind = GLOW;
    }

    t.colour = colour(paletted(p), saturate(alpha));
    t.data = float4(kind, copy, p.tone, p.heat);
    t.hidden = alpha < 0.004;
    return t;
}

// ---- drawing one --------------------------------------------------------------

float4 shade(Thing t, Canvas canvas)
{
    int kind = int(t.data.x + 0.5);
    float heat = t.data.w;
    float2 p = canvas.centred;
    float r = min(t.size.x, t.size.y) * 0.5;
    float q = length(p) / r;
    float cover = 0;
    float white = 0;

    if (kind == STREAK)
    {
        // A capsule: dim at the tail, bright and white at the head.
        float len = t.size.x;
        float d = Box(t.size, r).distance(p);
        float along = saturate(p.x / len + 0.5);
        cover = smoothstep(r * 0.6, -r * 0.4, d) * (0.15 + 0.85 * along * along);
        white = along * along * heat;
    }
    else if (kind == SPARKLE)
    {
        // Two thin spikes crossed, two shorter on the diagonals, and a glow.
        float2 a = abs(p) / r;
        float2 b = abs(rotate(p, PI / 4)) / r;
        float spikes = max(exp(-a.y * 18) * saturate(1 - a.x), exp(-a.x * 18) * saturate(1 - a.y));
        spikes = max(spikes, 0.5 * max(exp(-b.y * 22) * saturate(1 - b.x * 1.6), exp(-b.x * 22) * saturate(1 - b.y * 1.6)));
        cover = spikes + pow(saturate(1 - q * 2.5), 2);
        white = 0.6;
    }
    else if (kind == RING)
    {
        cover = Circle(r * 0.75).stroked(r * 0.14) + 0.3 * pow(saturate(1 - q), 3);
    }
    else if (kind == STAR)
    {
        cover = Star6(r * 0.85).filled() * 0.8 + 0.4 * pow(saturate(1 - q), 2);
        white = 0.3;
    }
    else if (kind == SOFT)
    {
        cover = pow(saturate(1 - q * q), 3);
    }
    else
    {
        // A glow: a falloff that reaches 0 at the quad's edge, and a hot core.
        float glow = pow(saturate(1 - q * q), 3);
        float core = smoothstep(0.4, 0.1, q);
        cover = glow * 0.6 + core * 0.6;
        white = core * heat * 0.7;
    }

    colour c = t.colour;
    c.rgb = lerp(c.rgb, float3(1, 1, 1), saturate(white));
    return c.withAlpha(saturate(c.a * cover));
}
