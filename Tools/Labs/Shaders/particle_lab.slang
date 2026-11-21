// Xanadu Labs' particle lab: a particle system whose state lives on the GPU
// and carries over from frame to frame. Nothing is worked out from the time
// alone; every frame moves what the last one left.
//
// What stays between frames:
//
//   particles  the pool: position, velocity, age, life, which emitter made it
//   dead       a stack of free pool slots; emitting pops, dying pushes
//   trails     a short ring of past positions per particle (true history, not
//              a guess back in time)
//   field      a grid every particle can write into (deposit) and read from
//              (sense); it fades and spreads each frame, so particles leave
//              marks the others follow or avoid
//   events     where particles died last frame; an emitter can be told to burst
//              out of every death of another (rockets into shells into crackle)
//
// A frame: begin → emitEvents → emit → simulate → finish → spread the field.
// Each runs in its own compute pass. What moves a particle is a stack of
// modules (forces, drag, noise, attractors, the field, bounds), applied in
// order, each to the emitters its mask names. Emitters, modules and the world
// all come in one uniform block (Sim); there is no per-scene code here.
//
// SDL_GPU's SPIR-V layout: compute reads storage buffers from set 0, writes them
// in set 1, uniforms in set 2; vertex storage set 0, uniforms set 1; fragment
// storage set 2, uniforms set 3.

import xanadu.math;
import xanadu.noise;
import xanadu.shape;
import xanadu.colour;

static const uint Emitters = 4;
static const uint Modules = 12;
static const uint Group = 64;

// counters[...]
static const uint DeadCount = 0;
static const uint AliveCount = 1;
static const uint TrailCount = 2;
static const uint EventsWritten = 3;
static const uint EventsToRead = 4;
static const uint Spawned = 5;
static const uint Died = 6;

// Emitter shapes.
static const uint POINT = 0;
static const uint LINE = 1;
static const uint CIRCLE = 2;
static const uint DISC = 3;
static const uint BOX = 4;
static const uint RING = 5;

// How a particle is drawn.
static const uint GLOW = 0;
static const uint DOT = 1;
static const uint RINGED = 2;
static const uint SPARK = 3;
static const uint SQUARE = 4;

// Modules.
static const uint FORCE = 1;
static const uint DRAG = 2;
static const uint NOISE = 3;
static const uint ATTRACT = 4;
static const uint VORTEX = 5;
static const uint POINTER = 6;
static const uint FIELD_SLOPE = 7;
static const uint FIELD_STEER = 8;
static const uint BOUNDS = 9;
static const uint SPEED = 10;

struct Emitter
{
    uint4 kind;         // shape, child emitter + 1 (0 none), children per death, how it is drawn
    uint4 emit;         // spawned this frame, first spawn index, on, unused
    float4 place;       // centre x, y (px from the middle, y down), size x, y
    float4 aim;         // shape turn, direction, spread (radians), outward 0..1
    float4 motion;      // speed min, max (px/s), share of the parent's velocity kept, deposit per second
    float4 life;        // life min, max (s), trail samples, stretch along velocity (s)
    float4 size;        // size at birth, at death (px), jitter 0..1, trail width share
    float4 look;        // hue jitter (turns), tangent speed share, fade in (s), unused
    float4 born;        // colour at birth (straight alpha)
    float4 dies;        // colour at death
};

struct Module
{
    uint4 head;         // kind, emitter mask, mode, unused
    float4 a;
    float4 b;
};

struct Sim
{
    uint4 frame;        // frame number, trail head, trail ring length, total spawned this frame
    uint4 counts;       // pool size, modules in use, most events, unused
    float4 time;        // time, dt, sim width, height (px)
    float4 mouse;       // x, y (px from the middle), held, over the view
    float4 field;       // fade per second, spread 0..1, grid width, height
    float4 look;        // field brightness, unused ×3
    float4 tint;        // field colour
    Emitter emitters[Emitters];
    Module modules[Modules];
};

struct Particle
{
    float4 motion;      // position x, y, velocity x, y
    float4 state;       // age, life, size scale, hue offset
    uint4 info;         // alive, emitter, seed, unused
};

struct Death
{
    float4 motion;      // where and how fast it was going
    uint4 info;         // the emitter to burst, its seed, unused ×2
};

// ---- compute bindings -------------------------------------------------------

[[vk::binding(0, 0)]] StructuredBuffer<Death> eventsIn;
[[vk::binding(1, 0)]] StructuredBuffer<float> fieldIn;

[[vk::binding(0, 1)]] RWStructuredBuffer<Particle> particles;
[[vk::binding(1, 1)]] RWStructuredBuffer<float2> trails;
[[vk::binding(2, 1)]] RWStructuredBuffer<uint> dead;
[[vk::binding(3, 1)]] RWStructuredBuffer<uint> alive;
[[vk::binding(4, 1)]] RWStructuredBuffer<int> counters;
[[vk::binding(5, 1)]] RWStructuredBuffer<Death> eventsOut;
[[vk::binding(6, 1)]] RWStructuredBuffer<uint> args;
[[vk::binding(7, 1)]] RWStructuredBuffer<uint> deposit;

[[vk::binding(0, 2)]] ConstantBuffer<Sim> sim;

// The field pass has its own: last frame's field in, this frame's out.
[[vk::binding(0, 1)]] RWStructuredBuffer<float> fieldOut;
[[vk::binding(1, 1)]] RWStructuredBuffer<uint> depositIn;

/// Deposits are summed as whole numbers; this many per unit of field.
static const float DepositScale = 1024;

// ---- helpers ----------------------------------------------------------------

uint capacity() { return sim.counts.x; }
uint ring() { return sim.frame.z; }
float dt() { return sim.time.y; }
float2 world() { return sim.time.zw; }
uint2 grid() { return uint2(sim.field.zw); }

/// A fresh 0..1 for a seed and a salt.
float rnd(uint seed, uint salt) { return random(uint3(seed, salt, 0x2545f491u)); }

/// The field's cell under a position, clamped to the grid.
uint cellOf(float2 position)
{
    float2 g = (position / world() + 0.5) * float2(grid());
    uint2 c = uint2(clamp(g, 0, float2(grid()) - 1));
    return c.y * grid().x + c.x;
}

/// The field at a position, blended between its four nearest cells.
float fieldAt(float2 position)
{
    float2 g = (position / world() + 0.5) * float2(grid()) - 0.5;
    float2 f = frac(g);
    int2 c = int2(floor(g));
    int2 most = int2(grid()) - 1;
    int2 c0 = clamp(c, 0, most), c1 = clamp(c + 1, 0, most);
    uint w = grid().x;
    float a = fieldIn[c0.y * w + c0.x], b = fieldIn[c0.y * w + c1.x];
    float d = fieldIn[c1.y * w + c0.x], e = fieldIn[c1.y * w + c1.x];
    return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
}

/// A point on an emitter's shape, before it is turned and placed.
float2 onShape(Emitter e, uint seed)
{
    float u = rnd(seed, 11), v = rnd(seed, 12);
    float2 size = e.place.zw;
    switch (e.kind.x)
    {
    case LINE:
        return float2((u - 0.5) * size.x, 0);
    case CIRCLE:
        return fromPolar(size.x, u * TAU);
    case DISC:
        return fromPolar(size.x * sqrt(v), u * TAU);
    case BOX:
        return (float2(u, v) - 0.5) * size;
    case RING:
        return fromPolar(lerp(size.x, max(size.x, size.y), sqrt(v)), u * TAU);
    default:
        return float2(0, 0);
    }
}

/// Takes a free slot and starts a particle of emitter `index` in it at `origin`; false when the pool is full.
bool spawn(uint index, float2 origin, float2 parentVelocity, uint seed)
{
    int before;
    InterlockedAdd(counters[DeadCount], -1, before);
    if (before <= 0)
    {
        InterlockedAdd(counters[DeadCount], 1);
        return false;
    }

    uint slot = dead[before - 1];
    Emitter e = sim.emitters[index];
    float2 local = rotate(onShape(e, seed), e.aim.x);
    float2 position = origin + local;

    float heading = e.aim.y + (rnd(seed, 13) - 0.5) * e.aim.z;
    float2 direction = fromPolar(1, heading);
    float2 out = length(local) > 1e-3 ? normalize(local) : direction;
    direction = normalize(lerp(direction, out, e.aim.w) + 1e-6);
    float speed = lerp(e.motion.x, e.motion.y, rnd(seed, 14));
    float2 tangent = float2(-out.y, out.x) * speed * e.look.y;
    float2 velocity = direction * speed * (1 - abs(e.look.y)) + tangent + parentVelocity * e.motion.z;

    Particle p;
    p.motion = float4(position, velocity);
    p.state = float4(0, lerp(e.life.x, e.life.y, rnd(seed, 15)), 1 + (rnd(seed, 16) * 2 - 1) * e.size.z, (rnd(seed, 17) - 0.5) * e.look.x);
    p.info = uint4(1, index, seed, 0);
    particles[slot] = p;

    // A new particle's history is where it starts, so its trail grows out of it.
    for (uint k = 0; k < ring(); k++)
        trails[slot * ring() + k] = position;
    InterlockedAdd(counters[Spawned], 1);
    return true;
}

// ---- the frame --------------------------------------------------------------

/// Empties the pool and every counter: all slots free.
[shader("compute")]
[numthreads(Group, 1, 1)]
void initPool(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i == 0)
    {
        for (uint c = 0; c < 8; c++)
            counters[c] = 0;
        counters[DeadCount] = int(capacity());
        for (uint a = 0; a < 8; a++)
            args[a] = 0;
    }
    if (i >= capacity())
        return;
    dead[i] = capacity() - 1 - i;
    particles[i].info = uint4(0, 0, 0, 0);
}

/// Last frame's deaths become this frame's events; the lists start empty.
[shader("compute")]
[numthreads(1, 1, 1)]
void beginFrame()
{
    counters[EventsToRead] = min(counters[EventsWritten], int(sim.counts.z));
    counters[EventsWritten] = 0;
    counters[AliveCount] = 0;
    counters[TrailCount] = 0;
    counters[Spawned] = 0;
    counters[Died] = 0;
}

/// Every death an emitter bursts from: its children, at the place it died.
[shader("compute")]
[numthreads(Group, 1, 1)]
void emitEvents(uint3 id : SV_DispatchThreadID)
{
    if (int(id.x) >= counters[EventsToRead])
        return;
    Death d = eventsIn[id.x];
    uint index = d.info.x;
    Emitter e = sim.emitters[index];
    if (e.emit.z == 0)
        return;
    uint children = min(sim.emitters[d.info.z].kind.z, 4096u);
    for (uint k = 0; k < children; k++)
    {
        if (!spawn(index, d.motion.xy, d.motion.zw, randomBits(uint3(d.info.y, k, sim.frame.x))))
            return;
    }
}

/// The emitters' own spawns this frame: thread i belongs to the emitter whose run it falls in.
[shader("compute")]
[numthreads(Group, 1, 1)]
void emit(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i >= sim.frame.w)
        return;
    for (uint index = 0; index < Emitters; index++)
    {
        Emitter e = sim.emitters[index];
        if (i >= e.emit.y && i < e.emit.y + e.emit.x)
        {
            spawn(index, e.place.xy, float2(0, 0), randomBits(uint3(i, sim.frame.x, index * 7919u)));
            return;
        }
    }
}

/// One module's push on a particle: the acceleration it adds, or a change it makes to the velocity straight away.
void apply(Module m, inout float2 position, inout float2 velocity, inout bool killed, Particle p)
{
    float4 a = m.a, b = m.b;
    float h = dt();
    switch (m.head.x)
    {
    case FORCE:
        velocity += a.xy * h;
        break;
    case DRAG:
        velocity *= exp(-(a.x + a.y * length(velocity) * 0.01) * h);
        break;
    case NOISE:
    {
        float2 flow = noise.curl(position / max(a.y, 1), sim.time.x * a.z);
        if (m.head.z == 0)
            velocity += flow * a.x * h;                                   // pushed by the flow
        else
            velocity = damped(velocity, flow * a.x, max(b.x, 0.01), h);   // carried along it
        break;
    }
    case ATTRACT:
    {
        float2 to = a.xy - position;
        float r = length(to);
        if (r < a.w || a.w <= 0)
            velocity += to / max(r, 1) * a.z / (1 + (r * r) / max(b.x * b.x, 1)) * h;
        break;
    }
    case VORTEX:
    {
        float2 to = a.xy - position;
        float r = length(to);
        float reach = a.w > 0 ? saturate(1 - r / a.w) : 1;
        float2 dir = to / max(r, 1);
        velocity += (float2(-dir.y, dir.x) * a.z + dir * b.x) * reach * h;
        break;
    }
    case POINTER:
    {
        if (sim.mouse.w < 0.5 || (b.x > 0.5 && sim.mouse.z < 0.5))
            break;
        float2 to = sim.mouse.xy - position;
        float r = length(to);
        float reach = saturate(1 - r / max(a.y, 1));
        float2 dir = to / max(r, 1);
        float2 push = m.head.z == 0 ? -dir : m.head.z == 1 ? dir : float2(-dir.y, dir.x);
        velocity += push * a.x * reach * reach * h;
        break;
    }
    case FIELD_SLOPE:
    {
        float s = max(a.y, 1);
        float2 slope = float2(fieldAt(position + float2(s, 0)) - fieldAt(position - float2(s, 0)),
                              fieldAt(position + float2(0, s)) - fieldAt(position - float2(0, s)));
        velocity += slope * a.x * h;
        break;
    }
    case FIELD_STEER:
    {
        // Physarum: look ahead, a little left and a little right, and turn towards the most.
        float speed = a.w > 0 ? a.w : length(velocity);
        float heading = atan2(velocity.y, velocity.x);
        float ahead = fieldAt(position + fromPolar(a.x, heading));
        float left = fieldAt(position + fromPolar(a.x, heading - a.y));
        float right = fieldAt(position + fromPolar(a.x, heading + a.y));
        float turn = a.z * h;
        if (ahead >= left && ahead >= right)
            turn = 0;
        else if (left > right)
            turn = -turn;
        else if (left == right)
            turn *= rnd(p.info.z ^ sim.frame.x, 21) < 0.5 ? -1 : 1;
        heading += turn + (rnd(p.info.z ^ sim.frame.x, 22) - 0.5) * b.x * h;
        velocity = fromPolar(speed, heading);
        break;
    }
    case BOUNDS:
    {
        // b: which walls (left, right, top, bottom); a.z pulls them in from the edge.
        float2 low = -world() * 0.5 + a.z, high = world() * 0.5 - a.z;
        bool4 walls = b != 0;
        bool2 under = position < low, over = position > high;
        bool2 hit = bool2((under.x && walls.x) || (over.x && walls.y), (under.y && walls.z) || (over.y && walls.w));
        if (!any(hit))
            break;
        if (m.head.z == 2)
        {
            killed = true;
            break;
        }
        for (int k = 0; k < 2; k++)
        {
            if (!hit[k])
                continue;
            if (m.head.z == 1)
            {
                position[k] = under[k] ? high[k] : low[k];
                continue;
            }
            position[k] = clamp(position[k], low[k], high[k]);
            velocity[k] = -velocity[k] * a.x;
            velocity[1 - k] *= 1 - a.y;
        }
        break;
    }
    case SPEED:
    {
        float s = length(velocity);
        if (s > 1e-4)
            velocity *= clamp(s, a.x, max(a.x, a.y)) / s;
        break;
    }
    default:
        break;
    }
}

/// Moves every live particle one frame: modules, then the step, then age; dying frees its slot and may leave an event.
[shader("compute")]
[numthreads(Group, 1, 1)]
void simulate(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i >= capacity())
        return;
    Particle p = particles[i];
    if (p.info.x == 0)
        return;

    uint index = p.info.y;
    Emitter e = sim.emitters[index];
    float2 position = p.motion.xy, velocity = p.motion.zw;
    bool killed = false;
    for (uint m = 0; m < sim.counts.y; m++)
    {
        Module module = sim.modules[m];
        if ((module.head.y & (1u << index)) != 0)
            apply(module, position, velocity, killed, p);
    }
    position += velocity * dt();
    p.state.x += dt();

    if (killed || p.state.x >= p.state.y || any(isnan(position)))
    {
        p.info.x = 0;
        particles[i] = p;
        int top;
        InterlockedAdd(counters[DeadCount], 1, top);
        dead[top] = i;
        InterlockedAdd(counters[Died], 1);
        // Children only for a death of old age: a particle swept off by bounds just goes.
        if (e.kind.y > 0 && !killed)
        {
            int slot;
            InterlockedAdd(counters[EventsWritten], 1, slot);
            if (slot < int(sim.counts.z))
            {
                Death d;
                d.motion = float4(position, velocity);
                d.info = uint4(e.kind.y - 1, p.info.z * 747796405u + 1u, index, 0);
                eventsOut[slot] = d;
            }
        }
        return;
    }

    p.motion = float4(position, velocity);
    particles[i] = p;
    if (sim.frame.y != 0xffffffffu)
        trails[i * ring() + sim.frame.y] = position;

    if (e.motion.w > 0)
        InterlockedAdd(deposit[cellOf(position)], uint(e.motion.w * dt() * DepositScale));

    int at;
    InterlockedAdd(counters[AliveCount], 1, at);
    alive[at] = i;
    if (e.life.z >= 2)
    {
        int t;
        InterlockedAdd(counters[TrailCount], 1, t);
        alive[capacity() + t] = i;
    }
}

/// The two draws' instance counts from this frame's lists.
[shader("compute")]
[numthreads(1, 1, 1)]
void finishFrame()
{
    args[0] = 6;
    args[1] = uint(counters[AliveCount]);
    args[2] = 0;
    args[3] = 0;
    args[4] = 6;
    args[5] = uint(counters[TrailCount]) * (ring() - 1);
    args[6] = 0;
    args[7] = 0;
}

/// The field fades, spreads into its neighbours and takes this frame's deposits.
[shader("compute")]
[numthreads(Group, 1, 1)]
void spreadField(uint3 id : SV_DispatchThreadID)
{
    uint2 size = grid();
    if (id.x >= size.x * size.y)
        return;
    int2 c = int2(id.x % size.x, id.x / size.x);
    float sum = 0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            int2 n = clamp(c + int2(x, y), 0, int2(size) - 1);
            sum += fieldIn[n.y * size.x + n.x];
        }
    float here = fieldIn[id.x];
    float spread = lerp(here, sum / 9, sim.field.y);
    fieldOut[id.x] = spread * exp(-sim.field.x * dt()) + float(depositIn[id.x]) / DepositScale;
    depositIn[id.x] = 0;
}

/// Every cell to zero (on reset).
[shader("compute")]
[numthreads(Group, 1, 1)]
void clearField(uint3 id : SV_DispatchThreadID)
{
    uint2 size = grid();
    if (id.x >= size.x * size.y)
        return;
    fieldOut[id.x] = 0;
    depositIn[id.x] = 0;
}

// ---- drawing ----------------------------------------------------------------

[[vk::binding(0, 0)]] StructuredBuffer<Particle> drawParticles;
[[vk::binding(1, 0)]] StructuredBuffer<uint> drawAlive;
[[vk::binding(2, 0)]] StructuredBuffer<float2> drawTrails;
[[vk::binding(0, 1)]] ConstantBuffer<Sim> vertexSim;
[[vk::binding(0, 2)]] StructuredBuffer<float> drawField;
[[vk::binding(0, 3)]] ConstantBuffer<Sim> fragmentSim;

struct Varyings
{
    float4 position : SV_Position;
    float4 colour : COLOR0;
    float2 local : TEXCOORD0;                   // px from the particle's middle, along its velocity
    nointerpolation float2 extent : TEXCOORD1;  // half length, radius (px)
    nointerpolation uint kind : TEXCOORD2;
};

static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

float4 toClip(float2 pixel, float2 size)
{
    float2 ndc = (pixel / size) * 2;
    return float4(ndc.x, -ndc.y, 0, 1);
}

/// A particle's colour at its age: birth to death, its hue nudged, fading in.
float4 colourOf(Emitter e, Particle p)
{
    float t = saturate(p.state.x / max(p.state.y, 1e-4));
    float4 c = lerp(e.born, e.dies, t);
    if (p.state.w != 0)
        c.rgb = float4(c.rgb, 1).hueShifted(p.state.w).rgb;
    c.a *= e.look.z > 0 ? saturate(p.state.x / e.look.z) : 1;
    return c;
}

float sizeOf(Emitter e, Particle p)
{
    return lerp(e.size.x, e.size.y, saturate(p.state.x / max(p.state.y, 1e-4))) * p.state.z;
}

/// One live particle: a quad round it, stretched along its velocity.
[shader("vertex")]
Varyings particleVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    Particle p = drawParticles[drawAlive[instance]];
    Emitter e = vertexSim.emitters[p.info.y];
    float radius = max(sizeOf(e, p) * 0.5, 0.5);
    float2 velocity = p.motion.zw;
    float speed = length(velocity);
    float2 along = speed > 1e-3 ? velocity / speed : float2(1, 0);
    float stretch = speed * e.life.w * 0.5;
    // Halos reach past the core.
    float reach = e.kind.w == GLOW ? radius * 2.5 : radius + 1;
    float2 corner = Corners[vertex] * float2(stretch + reach, reach);

    Varyings o;
    float2 pixel = p.motion.xy - along * stretch + along * corner.x + float2(-along.y, along.x) * corner.y;
    o.position = toClip(pixel, vertexSim.time.zw);
    o.colour = colourOf(e, p);
    o.local = corner;
    o.extent = float2(stretch, radius);
    o.kind = e.kind.w;
    return o;
}

[shader("fragment")]
float4 particleFragment(Varyings v) : SV_Target
{
    float r = v.extent.y;
    // Distance to the particle's spine: a capsule when stretched, a circle when still.
    float2 q = float2(v.local.x - clamp(v.local.x, -v.extent.x, v.extent.x), v.local.y);
    float d = length(q) - r;
    float a;
    switch (v.kind)
    {
    case GLOW:
        a = saturate(sdf.filled(d + r * 0.5, 1.5) * 0.8 + sdf.haloed(d + r, r * 2.5, 2.5));
        break;
    case DOT:
        a = sdf.filled(d, 1.2);
        break;
    case RINGED:
        a = sdf.stroked(d + r * 0.2, max(r * 0.3, 1), 1.2);
        break;
    case SPARK:
    {
        float2 s = abs(q) / max(r, 0.5);
        a = saturate(exp(-s.x * 6) * exp(-s.y) + exp(-s.y * 6) * exp(-s.x) + sdf.filled(d + r * 0.7, 1));
        break;
    }
    default:
        a = sdf.filled(max(abs(v.local.x) - v.extent.x, abs(v.local.y)) - r, 1.2);
        break;
    }
    a *= v.colour.a;
    return float4(v.colour.rgb * a, a);
}

/// One trail segment: from history sample k to k + 1, thinning and fading towards the tail.
[shader("vertex")]
Varyings trailVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    uint n = vertexSim.frame.z;
    uint segments = n - 1;
    uint slot = drawAlive[vertexSim.counts.x + instance / segments];
    uint k = instance % segments;
    Particle p = drawParticles[slot];
    Emitter e = vertexSim.emitters[p.info.y];
    uint samples = min(uint(e.life.z), n);

    Varyings o;
    o.position = float4(0, 0, 0, 0);
    o.colour = float4(0, 0, 0, 0);
    o.local = float2(0, 0);
    o.extent = float2(0, 0);
    o.kind = 0;
    if (k + 1 >= samples)
        return o;

    uint head = vertexSim.frame.y == 0xffffffffu ? 0 : vertexSim.frame.y;
    float2 from = k == 0 ? p.motion.xy : drawTrails[slot * n + (head + n - k) % n];
    float2 to = drawTrails[slot * n + (head + n - k - 1) % n];
    float2 span = to - from;
    // A jump (a wrap round the edge) is no trail.
    if (dot(span, span) > vertexSim.time.z * vertexSim.time.z * 0.0625)
        return o;

    float fade = 1 - (float(k) + 0.5) / float(samples);
    float width = max(sizeOf(e, p) * e.size.w * fade * 0.5, 0.35);
    float len = sqrt(dot(span, span));
    float2 along = len > 1e-4 ? span / len : float2(1, 0);
    float2 corner = Corners[vertex];
    float2 pixel = lerp(from, to, corner.x * 0.5 + 0.5) + float2(-along.y, along.x) * corner.y * (width + 1);

    o.position = toClip(pixel, vertexSim.time.zw);
    o.colour = colourOf(e, p);
    o.colour.a *= fade;
    o.local = float2(0, corner.y * (width + 1));
    o.extent = float2(0, width);
    return o;
}

[shader("fragment")]
float4 trailFragment(Varyings v) : SV_Target
{
    float a = sdf.filled(abs(v.local.y) - v.extent.y, 1.2) * v.colour.a;
    return float4(v.colour.rgb * a, a);
}

struct FieldVaryings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

/// The field, under everything: one triangle over the view.
[shader("vertex")]
FieldVaryings fieldVertex(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    FieldVaryings o;
    o.position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    o.uv = uv;
    return o;
}

[shader("fragment")]
float4 fieldFragment(FieldVaryings v) : SV_Target
{
    uint2 size = uint2(fragmentSim.field.zw);
    float2 g = v.uv * float2(size) - 0.5;
    int2 c = clamp(int2(floor(g)), 0, int2(size) - 1), c1 = clamp(int2(floor(g)) + 1, 0, int2(size) - 1);
    float2 f = frac(g);
    float a = drawField[c.y * size.x + c.x], b = drawField[c.y * size.x + c1.x];
    float d = drawField[c1.y * size.x + c.x], e = drawField[c1.y * size.x + c1.x];
    float value = lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
    float shown = 1 - exp(-value * fragmentSim.look.x);
    float4 tint = fragmentSim.tint;
    return float4(tint.rgb * shown * tint.a, 1);
}
