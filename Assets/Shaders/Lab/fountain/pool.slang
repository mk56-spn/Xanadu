// The particle lab's FOUNTAIN, cut down to one pool. `bear` works out the births this frame,
// RATE a second; they take the slots after the last ones, round a ring (the longest life fits
// in it, so a slot is free again by the time the ring comes back to it). `move` makes each
// newborn with start and steps the rest, a slot dying when moved returns false. Modules, in the
// lab's order: gravity, drag, a pusher (the lab's pointer; drag its dot), then the walls.
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
    uint alive;
};

// 8000 slots, 2500 births a second: the longest life (3 s) fits.
static const uint SLOTS = 8000;
static const float RATE = 2500;

[Buffer(SLOTS)] RWStructuredBuffer<Particle> slots;
// Births so far, the first born this frame, how many, and the birth owed (a float's bits).
[Buffer(4)] RWStructuredBuffer<uint> births;

/// Birth number n: at the emitter, with its own direction, speed and length of life.
Particle start(uint n)
{
    Particle p;
    p.position = emitter;
    p.velocity = fromPolar(lerp(Speed.x, Speed.y, random(uint3(n, 14, 0))), Heading + (random(uint3(n, 13, 0)) - 0.5) * Spread);
    p.age = 0;
    p.life = lerp(Life.x, Life.y, random(uint3(n, 15, 0)));
    p.hue = (random(uint3(n, 17, 0)) - 0.5) * 0.04;
    p.alive = 1;
    return p;
}

/// One frame of a live particle; false once it dies.
bool moved(inout Particle p)
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

[Pass(1, over: 1)]
void bear(uint i)
{
    float owed = asfloat(births[3]) + RATE * dt;
    uint n = min(uint(owed), SLOTS);
    births[3] = asuint(owed - n);
    births[1] = births[0];
    births[2] = n;
    births[0] += n;
}

[Pass(64, over: SLOTS)]
void move(uint i)
{
    // How far round the ring from this frame's first birth: within the births, it is one of them.
    uint first = births[1], k = (i + SLOTS - first % SLOTS) % SLOTS;
    Particle p = slots[i];
    if (k < births[2])
        p = start(first + k);
    else if (p.alive != 0 && !moved(p))
        p.alive = 0;
    slots[i] = p;
}

void graph()
{
    bear();
    move();
    particle(frame);
}
