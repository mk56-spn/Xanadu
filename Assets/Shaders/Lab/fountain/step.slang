// STEP: the particle lab's FOUNTAIN, cut down to one pool (Tools/Labs/Shaders/particle_lab.slang).
// The engine bears 2500 particles a second into free slots, each made by start, and
// frees the slot when step returns false. Modules, in the lab's order: gravity, drag,
// a pusher (the lab's pointer; drag its dot), then the walls.
import xanadu;

// Where the particles come from, in pixels from the middle: drag its dot on the preview.
[Default(0, 150)] uniform float2 emitter;
// The lab's pointer: it pushes particles away within its reach.
[Default(160, -40)] uniform float2 pusher;
[Default(450)] uniform float push;
[Default(110)] uniform float reach;
[Default(260)] uniform float gravity;
[Default(0.15)] uniform float drag;
// How much speed a wall bounce keeps, and how much the slide along it loses.
[Default(0.35)] uniform float bounce;
[Default(0.2)] uniform float friction;
// The world the walls stand round, centred on the middle.
[Default(640, 360)] uniform float2 world;

// The emitter, as the lab sets it: straight up, 18° wide, 260 to 320 px/s, 2.2 to 3 s.
static const float Heading = -PI / 2;
static const float Spread = 18 * PI / 180;
static const float2 Speed = float2(260, 320);
static const float2 Life = float2(2.2, 3);

struct Particle
{
    float2 position;    // pixels from the middle, y down
    float2 velocity;    // pixels a second
    float age;          // seconds
    float life;         // seconds it lives
    float hue;          // turns its colour is nudged by
};

/// Birth number n: at the emitter, with its own direction, speed and length of life.
Particle start(uint n)
{
    Particle p;
    p.position = emitter;
    p.velocity = fromPolar(lerp(Speed.x, Speed.y, random(uint3(n, 14, 0))), Heading + (random(uint3(n, 13, 0)) - 0.5) * Spread);
    p.age = 0;
    p.life = lerp(Life.x, Life.y, random(uint3(n, 15, 0)));
    p.hue = (random(uint3(n, 17, 0)) - 0.5) * 0.04;
    return p;
}

// 8000 slots, 2500 births a second: the longest life (3 s) fits.
[Pool(8000, 2500)]
bool step(inout Particle p, uint i, float dt)
{
    p.age += dt;
    if (p.age >= p.life || any(isnan(p.position)))
        return false;

    // Gravity, then drag.
    p.velocity.y += gravity * dt;
    p.velocity *= exp(-drag * dt);

    // The pusher: away from it, strongest close in.
    float2 to = pusher - p.position;
    float r = length(to);
    float near = saturate(1 - r / max(reach, 1));
    p.velocity -= to / max(r, 1) * push * near * near * dt;

    p.position += p.velocity * dt;

    // The walls: bounce off, losing some speed, and slide along them.
    float2 low = -world * 0.5, high = world * 0.5;
    for (int k = 0; k < 2; k++)
    {
        if (p.position[k] >= low[k] && p.position[k] <= high[k])
            continue;
        p.position[k] = clamp(p.position[k], low[k], high[k]);
        p.velocity[k] = -p.velocity[k] * bounce;
        p.velocity[1 - k] *= 1 - friction;
    }
    return true;
}
