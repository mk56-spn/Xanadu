// The particle lab (Labs' PARTICLES), whole, as a stack: a pool whose state carries over from
// frame to frame, free slots handed out and taken back, emitters bearing at a rate and in
// bursts, deaths that burst another emitter (rockets into shells into crackle), a ring of past
// positions per particle for real trails, and a field every particle can write into and read
// from, fading and spreading each frame. The moves come from xanadu.particles; which ones run
// on which emitter is `move` below, and the emitters are `emitterOf`: edit those to make it yours.
//
// A frame: plan → burst → emit → simulate → finish → spread. `particle` (look.slang) draws the
// field under it all, then every trail segment, then every live particle, as copies of a quad,
// as many as `drawn` says (counted on the GPU).
import xanadu;

static const uint CAP = 262144;             // particles the pool holds
static const uint EMITTERS = 4;
static const uint TRAIL = 12;               // past positions each one keeps
static const uint TRAIL_EVERY = 2;          // frames between them: longer trails from the same samples
static const uint EVENTS = 16384;           // deaths a frame that can burst
static const uint MOST_SPAWNS = 65536;      // births a frame
static const uint FIELD_W = 640;
static const uint FIELD_H = 360;
static const uint CELLS = FIELD_W * FIELD_H;
static const float DEPOSIT_SCALE = 1024;    // deposits sum as whole numbers: this many a unit of field
static const float2 WORLD = float2(1280, 720);   // px, centred, y down

// What the frame keeps at the head of `lists`.
static const uint STARTED = 0, DEAD = 1, ALIVE = 2, TRAILED = 3, WRITTEN = 4, TO_READ = 5, PARITY = 6,
                  FRAME = 7, HEAD = 8, SAMPLED = 9, TOTAL = 10, FIRST = 12, COUNT = 16, OWED = 20, QUEUED = 24;
static const uint HEADER = 32;
static const uint DEAD_LIST = HEADER, ALIVE_LIST = HEADER + CAP, TRAIL_LIST = HEADER + 2 * CAP;

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

[Buffer(CAP)] RWStructuredBuffer<Particle> pool;
[Buffer(CAP * TRAIL)] RWStructuredBuffer<float2> trails;     // each slot's past positions, a ring from the head
[Buffer(HEADER + 3 * CAP)] RWStructuredBuffer<uint> lists;   // the head, free slots, alive, trailed
[Buffer(2 * EVENTS)] RWStructuredBuffer<Death> deaths;       // last frame's half, this frame's half
[Buffer(2 * CELLS)] RWStructuredBuffer<float> field;         // the half read, the half written
[Buffer(CELLS)] RWStructuredBuffer<uint> deposits;
[Buffer(1)] RWStructuredBuffer<uint> drawn;              // the copies `particle` draws

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
    e.born = colour(1, 1, 1, 1); e.dies = colour(1, 1, 1, 0); e.hueJitter = 0; e.fadeIn = 0;
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
        e.trail = 6; e.trailWidth = 0.6; e.deposit = 1;
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
        velocity += fieldSlope(position, 6) * slope * dt;
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

/// The half of `field` this frame reads (last frame's spread), and the half it writes.
uint fieldRead() { return (1 - lists[PARITY]) * CELLS; }

uint cellOf(float2 position)
{
    uint2 c = uint2(clamp((position / WORLD + 0.5) * float2(FIELD_W, FIELD_H), 0, float2(FIELD_W, FIELD_H) - 1));
    return c.y * FIELD_W + c.x;
}

/// The field at a position, from half `from`, blended between its four nearest cells.
float fieldIn(uint from, float2 position)
{
    float2 g = (position / WORLD + 0.5) * float2(FIELD_W, FIELD_H) - 0.5;
    float2 f = frac(g);
    int2 most = int2(FIELD_W, FIELD_H) - 1;
    int2 c0 = clamp(int2(floor(g)), 0, most), c1 = clamp(int2(floor(g)) + 1, 0, most);
    float a = field[from + c0.y * FIELD_W + c0.x], b = field[from + c0.y * FIELD_W + c1.x];
    float d = field[from + c1.y * FIELD_W + c0.x], e = field[from + c1.y * FIELD_W + c1.x];
    return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
}

float fieldAt(float2 position) { return fieldIn(fieldRead(), position); }

/// The field's slope at a position, `probe` px either side: up it gathers, down it spreads.
float2 fieldSlope(float2 position, float probe)
{
    return float2(fieldAt(position + float2(probe, 0)) - fieldAt(position - float2(probe, 0)),
                  fieldAt(position + float2(0, probe)) - fieldAt(position - float2(0, probe)));
}

// ---- births ------------------------------------------------------------------------

float rnd(uint seed, uint salt) { return random(uint3(seed, salt, 0x2545f491u)); }

/// Takes a free slot and starts a particle of emitter `k` at `origin`; false when the pool is full.
bool spawn(uint k, float2 origin, float2 parentVelocity, uint seed)
{
    uint before;
    InterlockedAdd(lists[DEAD], 0xFFFFFFFFu, before);
    if (int(before) <= 0)
    {
        InterlockedAdd(lists[DEAD], 1u);
        return false;
    }

    uint slot = lists[DEAD_LIST + before - 1];
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
    // A new particle's history is where it starts, so its trail grows out of it.
    pool[slot] = p;
    for (uint t = 0; t < TRAIL; t++)
        trails[slot * TRAIL + t] = p.position;
    return true;
}

// ---- the frame ---------------------------------------------------------------------

/// After a compile: every slot free (until the head says it has started).
[Pass(256, over: CAP)]
void reset(uint i)
{
    if (lists[STARTED] != 0)
        return;
    lists[DEAD_LIST + i] = CAP - 1 - i;
    pool[i].alive = 0;
}

/// One thread: the frame's halves swap, last frame's deaths become this frame's bursts, and
/// each emitter's births this frame (its rate's share, the fraction carried, and its burst).
[Pass(1, over: 1)]
void plan(uint i)
{
    if (lists[STARTED] == 0)
    {
        lists[STARTED] = 1;
        lists[DEAD] = CAP;
        for (uint k = 0; k < EMITTERS; k++)
            lists[QUEUED + k] = emitterOf(k).burst;
    }

    lists[PARITY] = 1 - lists[PARITY];
    lists[TO_READ] = min(lists[WRITTEN], EVENTS);
    lists[WRITTEN] = 0;
    lists[ALIVE] = 0;
    lists[TRAILED] = 0;
    bool moving = dt > 0;
    uint frame = lists[FRAME] + (moving ? 1 : 0);
    lists[FRAME] = frame;
    bool sample = moving && frame % TRAIL_EVERY == 0;
    if (sample)
        lists[HEAD] = (lists[HEAD] + 1) % TRAIL;
    lists[SAMPLED] = sample ? 1 : 0;

    uint total = 0;
    for (uint k = 0; k < EMITTERS; k++)
    {
        float owed = asfloat(lists[OWED + k]) + emitterOf(k).rate * dt;
        uint room = MOST_SPAWNS - total;
        uint fromRate = uint(min(owed, float(room)));
        // A rate the pool can't take never piles up into one huge frame later.
        lists[OWED + k] = asuint(min(owed - fromRate, float(MOST_SPAWNS)));
        uint fromBurst = moving ? min(lists[QUEUED + k], room - fromRate) : 0;
        lists[QUEUED + k] -= fromBurst;
        lists[FIRST + k] = total;
        lists[COUNT + k] = fromRate + fromBurst;
        total += fromRate + fromBurst;
    }
    lists[TOTAL] = total;
}

/// Every death of last frame that bursts: its children, where it died.
[Pass(64, over: EVENTS)]
void burst(uint i)
{
    if (i >= lists[TO_READ])
        return;
    Death d = deaths[(1 - lists[PARITY]) * EVENTS + i];
    for (uint k = 0; k < min(d.children, 4096u); k++)
        if (!spawn(d.emitter, d.position, d.velocity, randomBits(uint3(d.seed, k, lists[FRAME]))))
            return;
}

/// The emitters' own births: thread i belongs to the emitter whose run it falls in.
[Pass(64, over: MOST_SPAWNS)]
void emit(uint i)
{
    if (i >= lists[TOTAL])
        return;
    for (uint k = 0; k < EMITTERS; k++)
    {
        if (i < lists[FIRST + k] + lists[COUNT + k])
        {
            spawn(k, emitterOf(k).at, float2(0, 0), randomBits(uint3(i, lists[FRAME], k * 7919u)));
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
        pool[i].alive = 0;
        uint top;
        InterlockedAdd(lists[DEAD], 1u, top);
        lists[DEAD_LIST + top] = i;
        if (e.child >= 0 && !killed)
        {
            uint slot;
            InterlockedAdd(lists[WRITTEN], 1u, slot);
            if (slot < EVENTS)
            {
                Death d;
                d.position = p.position;
                d.velocity = p.velocity;
                d.emitter = uint(e.child);
                d.children = e.children;
                d.seed = p.seed * 747796405u + 1u;
                d.pad = 0;
                deaths[lists[PARITY] * EVENTS + slot] = d;
            }
        }
        return;
    }

    if (lists[SAMPLED] != 0)
        trails[i * TRAIL + lists[HEAD]] = p.position;
    pool[i] = p;

    if (e.deposit > 0)
        InterlockedAdd(deposits[cellOf(p.position)], uint(e.deposit * dt * DEPOSIT_SCALE));

    uint at;
    InterlockedAdd(lists[ALIVE], 1u, at);
    lists[ALIVE_LIST + at] = i;
    if (e.trail >= 2)
    {
        uint t;
        InterlockedAdd(lists[TRAILED], 1u, t);
        lists[TRAIL_LIST + t] = i;
    }
}

/// The copies to draw: the field, every trail segment, every live particle.
[Pass(1, over: 1)]
void finish(uint i)
{
    drawn[0] = 1 + lists[TRAILED] * (TRAIL - 1) + lists[ALIVE];
}

/// The field fades, spreads into its neighbours and takes this frame's deposits.
[Pass(64, over: CELLS)]
void spreadField(uint i)
{
    uint from = fieldRead(), to = CELLS - from;
    uint row = i / FIELD_W;
    int2 c = int2(i % FIELD_W, row);
    float sum = 0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            int2 n = clamp(c + int2(x, y), 0, int2(FIELD_W, FIELD_H) - 1);
            sum += field[from + n.y * FIELD_W + n.x];
        }
    float spreadOut = lerp(field[from + i], sum / 9, saturate(spread));
    field[to + i] = spreadOut * exp(-fade * dt) + float(deposits[i]) / DEPOSIT_SCALE;
    deposits[i] = 0;
}

void graph()
{
    reset();
    plan();
    burst();
    emit();
    simulate();
    finish();
    spreadField();
    particle(frame);
}
