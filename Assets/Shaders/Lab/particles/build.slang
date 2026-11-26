// The particle lab (Labs' PARTICLES), whole, as a stack: a pool whose state carries over from
// frame to frame, free slots handed out and taken back, emitters bearing at a rate and in
// bursts, deaths that burst another emitter (rockets into shells into crackle), a ring of past
// positions per particle for real trails, and a field every particle can write into and read
// from, fading and spreading each frame. The moves come from xanadu.particles; which ones run
// on which emitter is `move` below, and the emitters are `emitterOf`: edit those to make it yours.
//
// A frame: plan → burst → emit → simulate → finish → field.spread (the Field block's pass).
// `particle` (look.slang) draws the field under it all, then every trail segment, then every
// live particle, as copies of a quad, as many as `drawn` says (counted on the GPU). Each
// emitter's `glow` picks normal or additive, or between: the draw is premultiplied, so one
// draw holds both.
import xanadu;

static const uint CAP = 262144;             // particles the pool holds
static const uint EMITTERS = 4;
static const uint TRAIL = 12;               // past positions each one keeps
static const uint TRAIL_EVERY = 2;          // frames between them: longer trails from the same samples
static const uint EVENTS = 16384;           // deaths a frame that can burst
static const uint MOST_SPAWNS = 65536;      // births a frame
static const uint FIELD_W = 640;           // the field's cells across and down
static const uint FIELD_H = 360;
static const float2 WORLD = float2(1280, 720);   // px, centred, y down


// Item pixels a world pixel: the world is the lab's 1280 x 720, drawn this much smaller or bigger.
[Default(0.53)] uniform float zoom;
// The pusher, in item pixels: drag its dot on the preview. 0 strength turns it off.
[Default(160, 0)] uniform float2 pointer;
[Default(4000)] uniform float push;
[Default(160)] uniform float reach;
// How hard particles run down the field (negative climbs it, gathering where others were).
[Default(-300)] uniform float slope;
// The field: how fast it fades a second, how much spreads into its neighbours a frame, how bright it shows.
[Default(2)] uniform float fade;
[Default(0.4)] uniform float spread;
[Default(0.3)] uniform float gain;
[Default(0.3, 0.9, 0.6, 1)] uniform colour tint;
[Default(0.02, 0.02, 0.035, 1)] uniform colour background;

struct Particle
{
    float2 position;        // px from the middle, y down
    float2 velocity;        // px/s
    float age;              // s
    float life;             // s it lives
    float scale;            // its size's share
    float hue;              // turns its colour is nudged by
    uint alive;
    uint emitter;
    uint seed;
    uint pad;
};

/// A death that bursts: where, how fast, which emitter it bursts and how many of it.
struct Death
{
    float2 position;
    float2 velocity;
    uint emitter;
    uint children;
    uint seed;
    uint pad;
};

/// The frame's bookkeeping, kept from frame to frame.
struct Book
{
    uint started;           // the emitters' bursts are queued
    uint frame;             // frames run while time moves
    uint total;             // the emitters' births this frame
    uint first[EMITTERS];   // where each emitter's births start among them
    uint count[EMITTERS];   // and how many
    float owed[EMITTERS];   // births carried to the next frame, a fraction of one
    uint queued[EMITTERS];  // a burst's births still to come
};

[Size(CAP)] Pool<Particle> pool;            // the particles, their free places and the live ones
[Size(CAP, TRAIL)] Trails trails;           // each slot's past positions
Stored<Book> book;
[Size(CAP)] Gathered<uint> trailed;          // the live ones with a trail
[Size(EVENTS)] Events<Death> deaths;         // this frame's, and last frame's, which burst now
[Size(FIELD_W, FIELD_H)] Field field;       // particles read last frame's and deposit into this one
Counter drawn;              // the copies `particle` draws

// ---- the emitters ------------------------------------------------------------------

struct Emitter
{
    uint shape;             // particles.POINT, LINE, CIRCLE, DISC, BOX, RING
    float2 at;              // px from the middle
    float2 size;            // the shape's size (particles.onShape)
    float turn;             // radians the shape is turned
    float rate;             // births a second
    uint burst;             // births once, at the start
    float heading;          // radians: 0 right, -PI/2 up
    float spread;           // radians either way, in all
    float outward;          // 0..1: bent away from the shape's middle
    float tangent;          // share of the speed sent round the middle
    float inherit;          // share of a parent's velocity a burst child keeps
    float2 speed;           // px/s, least and most
    float2 life;            // s, least and most
    float2 grows;           // px across at birth and at death
    float jitter;           // 0..1 size jitter
    float stretch;          // s of travel a moving one stretches along
    uint look;              // particles.GLOW, DOT, RINGED, SPARK, SQUARE
    colour born;
    colour dies;
    float glow;             // 0 laid over what's behind (normal) .. 1 adding its light (additive)
    float hueJitter;        // turns
    float fadeIn;           // s
    uint trail;             // samples of trail drawn, 0 none
    float trailWidth;       // share of its size
    float deposit;          // field it writes a second
    int child;              // the emitter each death of old age bursts, -1 none
    uint children;          // how many
};

Emitter plain()
{
    Emitter e;
    e.shape = particles.POINT; e.at = float2(0, 0); e.size = float2(40, 40); e.turn = 0;
    e.rate = 0; e.burst = 0;
    e.heading = -PI / 2; e.spread = radians(30); e.outward = 0; e.tangent = 0; e.inherit = 0;
    e.speed = float2(80, 160); e.life = float2(1, 2);
    e.grows = float2(6, 2); e.jitter = 0; e.stretch = 0; e.look = particles.GLOW;
    e.born = colour(1, 1, 1, 1); e.dies = colour(1, 1, 1, 0); e.glow = 1; e.hueJitter = 0; e.fadeIn = 0;
    e.trail = 0; e.trailWidth = 0.8; e.deposit = 0; e.child = -1; e.children = 40;
    return e;
}

/// The four emitters: rockets that burst into shells that crackle into sparks, and a fountain
/// that writes into the field and runs down it.
Emitter emitterOf(uint k)
{
    Emitter e = plain();
    switch (k)
    {
    case 0:     // rockets, from a line along the bottom
        e.shape = particles.LINE; e.at = float2(0, 360); e.size = float2(900, 0); e.rate = 1.6;
        e.spread = radians(12); e.speed = float2(560, 700); e.life = float2(1.1, 1.5); e.grows = float2(5, 4);
        e.born = colour(1, 0.75, 0.4, 1); e.dies = colour(1, 0.5, 0.2, 1);
        e.trail = 12; e.trailWidth = 0.5; e.child = 1; e.children = 220;
        break;
    case 1:     // shells: sparks every way, each crackling at the end
        e.spread = TAU; e.speed = float2(40, 300); e.life = float2(1.3, 2.2); e.grows = float2(4, 1.5);
        e.born = colour(1, 0.35, 0.6, 1); e.dies = colour(0.4, 0.1, 1, 0); e.hueJitter = 0.5; e.inherit = 0.2;
        e.trail = 8; e.trailWidth = 0.7; e.child = 2; e.children = 2;
        break;
    case 2:     // crackle
        e.spread = TAU; e.speed = float2(10, 60); e.life = float2(0.15, 0.4); e.grows = float2(5, 0);
        e.look = particles.SPARK; e.born = colour(1, 1, 0.85, 1); e.dies = colour(1, 0.9, 0.6, 0);
        break;
    default:    // the fountain
        e.at = float2(-300, 300); e.rate = 1200; e.spread = radians(18); e.speed = float2(520, 640);
        e.life = float2(2.2, 3); e.grows = float2(5, 2); e.stretch = 0.01;
        e.born = colour(0.5, 0.8, 1, 1); e.dies = colour(0.1, 0.3, 1, 0); e.hueJitter = 0.04;
        e.trail = 6; e.trailWidth = 0.6; e.deposit = 1; e.glow = 0;
        break;
    }
    return e;
}

/// One frame's moves on a particle of emitter `e`: which of xanadu.particles run, in order.
/// Setting `killed` frees it (no burst: only old age bursts).
void move(uint e, inout float2 position, inout float2 velocity, inout bool killed)
{
    float2 low = -WORLD / 2, high = WORLD / 2;
    if (e == 3)
    {
        velocity = particles.forced(velocity, float2(0, 520));
        velocity = particles.dragged(velocity, 0.15);
        velocity += field.slope(uvOf(position), 6 / WORLD) * slope * dt;
        particles.bounce(position, velocity, low, high, bool4(true), 0.35, 0.2);
    }
    else
    {
        velocity = particles.forced(velocity, float2(0, 200));
        if (e != 0)
            velocity = particles.dragged(velocity, 1.1);
        // Off the sides or the top, well past the edge: gone.
        killed = any(particles.pastWalls(position, low - 80, high + 80, bool4(true, true, true, false)));
    }
    if (push != 0)
        velocity = particles.pushed(position, velocity, pointer / max(zoom, 1e-3), push, reach, particles.AWAY);
}

// ---- the field ---------------------------------------------------------------------

/// Where a world position falls on the field, 0..1 across the world.
float2 uvOf(float2 position) { return position / WORLD + 0.5; }

// ---- births ------------------------------------------------------------------------

float rnd(uint seed, uint salt) { return random(uint3(seed, salt, 0x2545f491u)); }

/// Takes a free slot and starts a particle of emitter `k` at `origin`; false when the pool is full.
bool spawn(uint k, float2 origin, float2 parentVelocity, uint seed)
{
    uint slot;
    if (!pool.take(slot))
        return false;
    Emitter e = emitterOf(k);
    float2 local = rotate(particles.onShape(e.shape, e.size, rnd(seed, 11), rnd(seed, 12)), e.turn);
    float heading = e.heading + (rnd(seed, 13) - 0.5) * e.spread;
    float speed = lerp(e.speed.x, e.speed.y, rnd(seed, 14));

    Particle p;
    p.position = origin + local;
    p.velocity = particles.launched(local, heading, e.outward, speed, e.tangent) + parentVelocity * e.inherit;
    p.age = 0;
    p.life = lerp(e.life.x, e.life.y, rnd(seed, 15));
    p.scale = 1 + (rnd(seed, 16) * 2 - 1) * e.jitter;
    p.hue = (rnd(seed, 17) - 0.5) * e.hueJitter;
    p.alive = 1;
    p.emitter = k;
    p.seed = seed;
    p.pad = 0;
    pool.put(slot, p);
    trails.start(slot, p.position);
    return true;
}

// ---- the frame ---------------------------------------------------------------------

/// One thread: last frame's deaths become this frame's bursts, and each emitter's births this
/// frame (its rate's share, the fraction carried, and its burst).
[Pass(1, over: 1)]
void plan(uint i)
{
    if (book.started == 0)
    {
        book.started = 1;
        for (uint k = 0; k < EMITTERS; k++)
            book.queued[k] = emitterOf(k).burst;
    }

    deaths.turn();
    pool.turn();
    trailed.clear();
    bool moving = dt > 0;
    book.frame += moving ? 1 : 0;
    trails.turn(moving, TRAIL_EVERY);

    uint total = 0;
    for (uint k = 0; k < EMITTERS; k++)
    {
        float owed = book.owed[k] + emitterOf(k).rate * dt;
        uint room = MOST_SPAWNS - total;
        uint fromRate = uint(min(owed, float(room)));
        // A rate the pool can't take never piles up into one huge frame later.
        book.owed[k] = min(owed - fromRate, float(MOST_SPAWNS));
        uint fromBurst = moving ? min(book.queued[k], room - fromRate) : 0;
        book.queued[k] -= fromBurst;
        book.first[k] = total;
        book.count[k] = fromRate + fromBurst;
        total += fromRate + fromBurst;
    }
    book.total = total;
}

/// Every death of last frame that bursts: its children, where it died.
[Pass(64, over: EVENTS)]
void burst(uint i)
{
    if (i >= deaths.heard)
        return;
    Death d = deaths[i];
    for (uint k = 0; k < min(d.children, 4096u); k++)
        if (!spawn(d.emitter, d.position, d.velocity, randomBits(uint3(d.seed, k, book.frame))))
            return;
}

/// The emitters' own births: thread i belongs to the emitter whose run it falls in.
[Pass(64, over: MOST_SPAWNS)]
void emit(uint i)
{
    if (i >= book.total)
        return;
    for (uint k = 0; k < EMITTERS; k++)
    {
        if (i < book.first[k] + book.count[k])
        {
            spawn(k, emitterOf(k).at, float2(0, 0), randomBits(uint3(i, book.frame, k * 7919u)));
            return;
        }
    }
}

/// Every live particle a frame on: its moves, then its step and age. Dying frees its slot
/// and, of old age, leaves a death to burst; living, it joins the lists the draw reads.
[Pass(64, over: CAP)]
void simulate(uint i)
{
    Particle p = pool[i];
    if (p.alive == 0)
        return;

    Emitter e = emitterOf(p.emitter);
    bool killed = false;
    move(p.emitter, p.position, p.velocity, killed);
    p.position += p.velocity * dt;
    p.age += dt;

    if (killed || p.age >= p.life || any(isnan(p.position)))
    {
        p.alive = 0;
        pool.put(i, p);
        pool.give(i);
        if (e.child >= 0 && !killed)
        {
            Death d;
            d.position = p.position;
            d.velocity = p.velocity;
            d.emitter = uint(e.child);
            d.children = e.children;
            d.seed = p.seed * 747796405u + 1u;
            d.pad = 0;
            deaths.add(d);
        }
        return;
    }

    trails.record(i, p.position);
    pool.put(i, p);

    if (e.deposit > 0)
        field.deposit(uvOf(p.position), e.deposit * dt);

    pool.live(i);
    if (e.trail >= 2)
        trailed.add(i);
}

/// The copies to draw: the field, every trail segment, every live particle.
[Pass(1, over: 1)]
void finish(uint i)
{
    drawn[0] = 1 + trailed.count * (TRAIL - 1) + pool.count;
}

void graph()
{
    plan();
    burst();
    emit();
    simulate();
    finish();
    field.spread(fade, spread);
    particle(frame);
}
