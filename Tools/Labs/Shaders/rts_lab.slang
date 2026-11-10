// Xanadu Labs' GPU RTS. Five armies fight over thirty sites on rocky ground,
// and nothing about the battle lives on the CPU: it creates the buffers, reads
// the panel's buttons and the raw mouse and keys, and records the passes. The
// rest happens here.
//
// Every simulation step (several a frame when sped up):
//   clearCells  zero the spatial grid and the per-step tallies
//   count       a thread per unit: cell counts, army sizes and centres, who
//               stands on which site, squad centres (atomics)
//   scan        one group: a prefix sum over the grid turns counts into
//               starts, and writes the dispatch size of `think` (indirect)
//   scatter     every living unit takes a slot in its cell's run
//   strategy    sites change hands; squads pick objectives; armies get caps
//   think       a thread per *living* unit, dispatched indirectly: scans its
//               neighbourhood through the grid, picks a target, steers (flow
//               fields, kiting, separation) and fires. Damage and healing are
//               atomic adds onto the victim, so no one has to gather them.
//   resolve     damage lands, units die and credit their killer, the dead
//               respawn at sites that raise their type, the living move and
//               slide along rock
// Every frame:
//   frameBegin  one thread reads the mouse and keys: camera, box select,
//               move orders (each order seeds its own flow field)
//   seedFields / relax   flow fields toward every site and every live order,
//               relaxed in place until settled
//   cull        selection lands; visible units, tracers and effects are packed
//               into lists whose counts are the instance counts of three
//               indirect draws
//
// SDL_GPU's SPIR-V layout: compute writes storage buffers in set 1, uniforms
// set 2; vertex storage set 0, uniforms set 1; fragment storage set 2,
// uniforms set 3.

// ---- rules ---------------------------------------------------------------------

static const float2 WORLD = float2(14400, 8640);
static const float CELL = 160;                // grid and navigation cell, px
static const int GX = 90;                     // WORLD / CELL
static const int GY = 54;
static const int CELLS = GX * GY;
static const int FACTIONS = 5;
static const int PLAYER = 0;                  // the army the mouse commands
static const int NEUTRAL = 5;
static const int SITES = 30;                  // 5 bases, 25 neutral
static const int ORDERS = 8;                  // live move orders, each with a field
static const int FIELDS = SITES + ORDERS;
static const int SQUADS = 12;                 // per army
static const uint MAX_UNITS = 1536000;
static const uint FX = 8192;
static const float INF = 1e9;
static const float ROCK = 0.6;                // rockiness above this is solid

static const int SCOUT = 0;      // fast, hunts artillery and snipers
static const int RIFLE = 1;      // line infantry
static const int HEAVY = 2;      // armoured, crushes infantry
static const int SNIPER = 3;     // long range, picks off medics and commanders, backs off
static const int ARTILLERY = 4;  // lobs over rock, splash, can't fire up close
static const int MEDIC = 5;      // heals the most wounded friend it can see
static const int BERSERKER = 6;  // melee, hits harder the more hurt it is
static const int GUARDIAN = 7;   // friends near it take less damage
static const int COMMANDER = 8;  // friends near it hit harder
static const int TYPES = 9;

static const float SPEED[TYPES] = { 190, 100, 55, 80, 45, 110, 150, 60, 85 };
static const float HP[TYPES] = { 50, 110, 420, 70, 120, 90, 160, 380, 260 };
static const float DAMAGE[TYPES] = { 6, 12, 40, 70, 45, 18, 18, 10, 16 };      // a medic's heals
static const float RANGE[TYPES] = { 70, 130, 170, 600, 270, 80, 20, 60, 150 };
static const float COOLDOWN[TYPES] = { 0.35, 0.8, 2.0, 3.0, 3.5, 0.6, 0.5, 1.0, 1.0 };
static const float RESPAWN[TYPES] = { 2, 3, 6, 4, 6, 3, 3, 5, 8 };
static const float SIZE[TYPES] = { 5, 6, 10, 6, 9, 6, 7, 11, 9 };
static const float ARMOR[TYPES] = { 0, 1, 6, 0, 1, 0, 2, 4, 3 };              // off every hit
static const float RETREAT_AT[TYPES] = { 0.45, 0.3, 0.25, 0.4, 0.35, 0.35, 0, 0.2, 0.4 };
static const float HOLD_AT[TYPES] = { 0.8, 0.85, 0.85, 0.9, 0.9, 0.7, 0.3, 0.6, 0.85 };
static const float KITE[TYPES] = { 0, 0, 0, 160, 150, 0, 0, 0, 0 };

static const float SIGHT = 300;
static const float ARTILLERY_MIN = 90;
static const float SPLASH = 70;
static const float SPLASH_SHARE = 0.6;
static const float AURA = 70;
static const float AURA_SHARE = 0.6;
static const float BUFF = 140;
static const float BUFF_SHARE = 1.35;
static const float RAGE = 1.5;
static const float REGEN = 15;              // hp/s inside an owned site's ring
static const float TRACER = 0.12;
static const float CAPTURE_RADIUS = 320;
static const float CAPTURE_TIME = 8;
static const float CLEAR_RADIUS = 650;      // no rock this close to a site
static const float ORDER_ARRIVE = 220;
static const uint SCAN_BUDGET = 320;        // neighbours a unit looks at, at most

// Sites raise certain types, so armies must take ground to field everything.
static const uint SITE_BASE = 0;      // rifles, scouts, medics
static const uint SITE_FACTORY = 1;   // heavies, guardians
static const uint SITE_FIREBASE = 2;  // artillery, snipers
static const uint SITE_BARRACKS = 3;  // berserkers
static const uint SITE_COMMAND = 4;   // commanders

static const float3 COLOURS[6] = {
    float3(0.96, 0.32, 0.30), float3(0.98, 0.78, 0.22), float3(0.36, 0.86, 0.42),
    float3(0.26, 0.68, 1.00), float3(0.80, 0.46, 0.98), float3(0.55, 0.57, 0.62) };

// Unit flags.
static const uint ALIVE = 1;
static const uint SELECTED = 2;
static const uint RETREATING = 4;
static const uint GUARDED = 8;
static const uint BUFFED = 16;
static const uint FIRED = 32;

// Effect kinds.
static const uint FX_DEATH = 0;
static const uint FX_BLAST = 1;
static const uint FX_ORDER = 2;
static const uint FX_CAPTURE = 3;
static const float FX_LIFE[4] = { 0.5, 0.6, 0.7, 1.6 };

// Raw input bits, as the lab sends them.
static const uint MOUSE_LEFT = 1;
static const uint MOUSE_RIGHT = 2;
static const uint MOUSE_MIDDLE = 4;
static const uint KEY_W = 1, KEY_A = 2, KEY_S = 4, KEY_D = 8;
static const uint KEY_UP = 16, KEY_LEFT = 32, KEY_DOWN = 64, KEY_RIGHT = 128;
static const uint KEY_Q = 256, KEY_E = 512, KEY_F = 1024, KEY_SHIFT = 2048, KEY_ESCAPE = 4096;
static const uint KEY_DIGIT1 = 8192;   // and the next four bits for 2-5

// Indirect argument layout, in uints.
static const uint ARG_UNITS = 0;     // draw: 6 vertices, visible units
static const uint ARG_TRACERS = 4;   // draw: 6 vertices, visible shots
static const uint ARG_FX = 8;        // draw: 6 vertices, visible effects
static const uint ARG_THINK = 12;    // dispatch: groups of 64 living units

// Screen furniture, stage px.
static const float2 MINIMAP_SIZE = float2(300, 180);
static const float MINIMAP_MARGIN = 16;

// ---- state -------------------------------------------------------------------------

struct Frame
{
    float time;      // s since the stage opened
    float dt;        // s of battle per step (0 while paused)
    float frameDt;   // real s since the last frame
    float steps;     // steps this frame
    float width;     // stage px
    float height;
    float mouseX;    // stage px
    float mouseY;
    uint count;      // units in the battle
    uint buttons;    // MOUSE_*
    uint keys;       // KEY_*
    uint view;       // 0 battle, 1 flow field under the pointer
    int wheel;       // notches this frame
    uint pad0, pad1, pad2;
};

struct Unit
{
    float2 pos;
    float2 vel;
    float hp;
    float cooldown;
    float respawn;   // s until it may rise again (dead units)
    float heading;
    uint target;     // last unit shot at
    uint flags;
    uint order;      // order slot + 1 in the low 4 bits, that order's serial above
    float shot;      // s the tracer has left
    float2 shotTo;
    float pad0, pad1;
};

/// What landed on a unit this step: damage (×16, negative heals) and who hit it last (faction + 1).
struct Hit
{
    int damage;
    uint by;
};

struct Cell
{
    uint count;
    uint start;
    uint cursor;
    uint pad;
    uint density[FACTIONS];   // living units per army, for the minimap
    uint pad1, pad2, pad3;
};

struct Site
{
    float2 pos;
    int owner;        // army, or NEUTRAL
    int capturer;     // army taking it, or -1
    float progress;   // 0..1
    uint kind;        // SITE_*
    uint presence[FACTIONS];
    float pad;
};

struct Army
{
    uint alive;
    uint kills;
    int sumX;         // of positions / 8, for the army's centre
    int sumY;
    uint owned;
    uint cap;
    uint selected;
    uint deaths;
};

struct Squad
{
    int objective;    // site
    float timer;      // s until it thinks again
    int sumX;
    int sumY;
    uint count;
    uint pad0, pad1, pad2;
};

struct Order
{
    float2 dest;
    uint serial;
    uint pad;
};

struct Effect
{
    float2 pos;
    float birth;
    float radius;
    uint faction;
    uint kind;
    float pad0, pad1;
};

struct Game
{
    float4 camera;       // centre xy (world), zoom (stage px per world px), fit zoom
    float4 box;          // drag start xy, now xy (stage px)
    float4 selection;    // the last box in world space: min xy, max xy
    float2 mouseBefore;
    uint buttonsBefore;
    uint boxActive;
    uint selectApply;    // 1 replace, 2 add, this frame
    uint orderApply;     // order slot + 1, this frame
    uint orderNext;
    uint fxHead;
    uint aliveTotal;
    uint dragMinimap;
    uint keysBefore;
    uint pad;
    uint fieldAge[FIELDS];
    Site sites[SITES];
    Army armies[FACTIONS];
    Squad squads[FACTIONS * SQUADS];
    Order orders[ORDERS];
    Effect fx[FX];
};

// Compute sees everything read-write, in this order (the lab binds the same eight).
[[vk::binding(0, 1)]] RWStructuredBuffer<Unit> units;
[[vk::binding(1, 1)]] RWStructuredBuffer<Hit> hits;
[[vk::binding(2, 1)]] RWStructuredBuffer<Cell> cells;
[[vk::binding(3, 1)]] RWStructuredBuffer<uint> sorted;
[[vk::binding(4, 1)]] RWStructuredBuffer<float> world;    // rockiness per cell, then a field per goal
[[vk::binding(5, 1)]] RWStructuredBuffer<Game> game;
[[vk::binding(6, 1)]] RWStructuredBuffer<uint> lists;     // visible units, then shots, then effects
[[vk::binding(7, 1)]] RWStructuredBuffer<uint> args;      // indirect draws and the think dispatch
[[vk::binding(0, 2)]] ConstantBuffer<Frame> frame;

// Drawing reads.
[[vk::binding(0, 0)]] StructuredBuffer<Unit> unitsV;
[[vk::binding(1, 0)]] StructuredBuffer<uint> listsV;
[[vk::binding(2, 0)]] StructuredBuffer<Game> gameV;
[[vk::binding(0, 1)]] ConstantBuffer<Frame> frameV;

[[vk::binding(0, 2)]] StructuredBuffer<Game> gameF;
[[vk::binding(1, 2)]] StructuredBuffer<Cell> cellsF;
[[vk::binding(2, 2)]] StructuredBuffer<float> worldF;
[[vk::binding(0, 3)]] ConstantBuffer<Frame> frameF;

// ---- helpers ---------------------------------------------------------------------

uint pcg(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

float random(uint a, uint b)
{
    return float(pcg(a ^ pcg(b + 0x9e3779b9u))) / 4294967295.0;
}

float2 randomDisc(uint a, uint b)
{
    float angle = random(a, b) * 6.2831853;
    return float2(cos(angle), sin(angle)) * sqrt(random(a, b + 1));
}

uint factionOf(uint id) { return id % FACTIONS; }

/// Every army's mix, per twenty: 6 rifles, 3 scouts, 2 heavies, 2 medics, 3 berserkers, one each of the rest.
uint typeOf(uint id)
{
    uint k = (id / FACTIONS) % 20;
    if (k < 6) return RIFLE;
    if (k < 9) return SCOUT;
    if (k < 11) return HEAVY;
    if (k < 13) return MEDIC;
    if (k < 16) return BERSERKER;
    if (k == 16) return SNIPER;
    if (k == 17) return ARTILLERY;
    if (k == 18) return GUARDIAN;
    return COMMANDER;
}

uint squadOf(uint id) { return (id / FACTIONS / 20) % SQUADS; }

int2 cellOf(float2 p) { return clamp(int2(floor(p / CELL)), int2(0, 0), int2(GX - 1, GY - 1)); }

uint cellIndex(int2 c) { return uint(c.y * GX + c.x); }

float2 cellCentre(int2 c) { return (float2(c) + 0.5) * CELL; }

bool inGrid(int2 c) { return c.x >= 0 && c.y >= 0 && c.x < GX && c.y < GY; }

float bonus(uint shooter, uint target)
{
    if (shooter == SCOUT && (target == ARTILLERY || target == SNIPER)) return 2.0;
    if (shooter == RIFLE && target == SCOUT) return 1.5;
    if (shooter == HEAVY && (target == RIFLE || target == BERSERKER)) return 1.5;
    if (shooter == SNIPER && (target == COMMANDER || target == MEDIC || target == ARTILLERY)) return 1.5;
    if (shooter == BERSERKER && target == HEAVY) return 1.5;
    return 1.0;
}

bool siteRaises(uint site, uint type)
{
    if (site == SITE_BASE) return type == RIFLE || type == SCOUT || type == MEDIC;
    if (site == SITE_FACTORY) return type == HEAVY || type == GUARDIAN;
    if (site == SITE_FIREBASE) return type == ARTILLERY || type == SNIPER;
    if (site == SITE_BARRACKS) return type == BERSERKER;
    return type == COMMANDER;
}

float2 sitePosition(int s)
{
    float2 centre = WORLD * 0.5;
    if (s < FACTIONS)
    {
        float a = -1.5707963 + float(s) * 1.2566371;
        return centre + float2(cos(a), sin(a)) * WORLD * float2(0.43, 0.41);
    }
    int i = s - FACTIONS;
    float2 cell = WORLD / float2(5, 5);
    float2 jitter = float2(random(uint(i), 11u), random(uint(i), 12u)) - 0.5;
    return (float2(float(i % 5), float(i / 5)) + 0.5 + jitter * 0.55) * cell;
}

uint siteKind(int s)
{
    if (s < FACTIONS)
        return SITE_BASE;
    int i = s - FACTIONS;
    int k = (i % 5 + 2 * (i / 5)) % 7;
    if (k < 2) return SITE_FACTORY;
    if (k < 4) return SITE_FIREBASE;
    if (k < 6) return SITE_BARRACKS;
    return SITE_COMMAND;
}

float valueNoise(float2 p, uint salt)
{
    int2 i = int2(floor(p));
    float2 f = frac(p);
    f = f * f * (3 - 2 * f);
    float a = random(uint(i.x) * 73856093u ^ uint(i.y) * 19349663u, salt);
    float b = random(uint(i.x + 1) * 73856093u ^ uint(i.y) * 19349663u, salt);
    float c = random(uint(i.x) * 73856093u ^ uint(i.y + 1) * 19349663u, salt);
    float d = random(uint(i.x + 1) * 73856093u ^ uint(i.y + 1) * 19349663u, salt);
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float2 toScreen(float2 p, float4 camera, float2 size) { return (p - camera.xy) * camera.z + size * 0.5; }

float2 toWorld(float2 s, float4 camera, float2 size) { return (s - size * 0.5) / camera.z + camera.xy; }

float2 minimapOrigin(float2 size) { return size - MINIMAP_SIZE - MINIMAP_MARGIN; }

bool onMinimap(float2 s, float2 size)
{
    float2 o = minimapOrigin(size);
    return all(s >= o) && all(s < o + MINIMAP_SIZE);
}

// ---- compute: shared --------------------------------------------------------------

bool rockAt(float2 p)
{
    if (any(p < 0) || any(p >= WORLD))
        return true;
    return world[cellIndex(cellOf(p))] > ROCK;
}

bool lineOfSight(float2 a, float2 b)
{
    float d = distance(a, b);
    int n = int(d / 80);
    for (int i = 1; i <= n; i++)
        if (world[cellIndex(cellOf(lerp(a, b, float(i) / float(n + 1))))] > ROCK)
            return false;
    return true;
}

void pushEffect(float2 pos, float radius, uint faction, uint kind)
{
    uint slot;
    InterlockedAdd(game[0].fxHead, 1u, slot);
    Effect e;
    e.pos = pos;
    e.birth = frame.time;
    e.radius = radius;
    e.faction = faction;
    e.kind = kind;
    e.pad0 = 0;
    e.pad1 = 0;
    game[0].fx[slot % FX] = e;
}

bool orderLive(uint order, out uint slot)
{
    slot = (order & 15u) - 1u;
    return order != 0 && slot < ORDERS && game[0].orders[slot].serial == (order >> 4);
}

/// Which way to step toward a goal along its field: to the cheapest open neighbour cell.
float2 flowDirection(int field, float2 pos, float2 goal)
{
    float2 straight = goal - pos;
    float far = length(straight);
    if (far < CELL * 1.5)
        return straight / max(far, 1e-3);

    int2 c = cellOf(pos);
    uint base = uint(CELLS) * uint(1 + field);
    float here = world[base + cellIndex(c)];
    if (here >= INF * 0.5)
        return straight / max(far, 1e-3);

    float best = here;
    float2 aim = straight;
    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 n = c + int2(dx, dy);
            if ((dx == 0 && dy == 0) || !inGrid(n))
                continue;
            if (world[cellIndex(n)] > ROCK)
                continue;
            if (dx != 0 && dy != 0 && (world[cellIndex(c + int2(dx, 0))] > ROCK || world[cellIndex(c + int2(0, dy))] > ROCK))
                continue;
            float v = world[base + cellIndex(n)];
            if (v < best)
            {
                best = v;
                aim = cellCentre(n) - pos;
            }
        }
    }
    return normalize(aim + straight / far * 0.001);
}

// ---- compute: setup (the lab runs these when the battle starts) ------------------

[shader("compute")]
[numthreads(64, 1, 1)]
void initGame(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i == 0)
    {
        // Open on your own base, close enough to see unit shapes; F fits the whole field.
        float fit = min(frame.width / WORLD.x, frame.height / WORLD.y);
        game[0].camera = float4(sitePosition(PLAYER) + float2(0, 900), 0.45, fit);
        game[0].box = 0;
        game[0].selection = 0;
        game[0].mouseBefore = 0;
        game[0].buttonsBefore = 0;
        game[0].boxActive = 0;
        game[0].selectApply = 0;
        game[0].orderApply = 0;
        game[0].orderNext = 0;
        game[0].fxHead = 0;
        game[0].aliveTotal = 0;
        game[0].dragMinimap = 0;
        game[0].keysBefore = 0;
    }
    if (i < FIELDS)
        game[0].fieldAge[i] = i < SITES ? 0 : 1000000;   // orders seed when issued
    if (i < SITES)
    {
        Site s;
        s.pos = sitePosition(int(i));
        s.owner = i < FACTIONS ? int(i) : NEUTRAL;
        s.capturer = -1;
        s.progress = 0;
        s.kind = siteKind(int(i));
        for (int f = 0; f < FACTIONS; f++)
            s.presence[f] = 0;
        s.pad = 0;
        game[0].sites[i] = s;
    }
    if (i < FACTIONS)
    {
        Army a = (Army)0;
        a.owned = 1;
        a.cap = frame.count / FACTIONS / 3;
        game[0].armies[i] = a;
    }
    if (i < FACTIONS * SQUADS)
    {
        Squad q = (Squad)0;
        q.objective = -1;
        q.timer = random(i, 3u) * 2;
        game[0].squads[i] = q;
    }
    if (i < ORDERS)
    {
        Order o = (Order)0;
        game[0].orders[i] = o;
    }
    for (uint k = i; k < FX; k += 64)
    {
        Effect e = (Effect)0;
        e.birth = -100;
        game[0].fx[k] = e;
    }
}

/// Rock from noise, cleared round the sites; every field starts unreached.
[shader("compute")]
[numthreads(64, 1, 1)]
void initWorld(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= uint(CELLS))
        return;
    int2 c = int2(int(id.x) % GX, int(id.x) / GX);
    float2 p = float2(c) + 0.5;
    float n = valueNoise(p / 9.0, 1u) * 0.55 + valueNoise(p / 4.5, 2u) * 0.3 + valueNoise(p / 2.2, 3u) * 0.15;
    float2 centre = cellCentre(c);
    for (int s = 0; s < SITES; s++)
    {
        float d = distance(centre, game[0].sites[s].pos);
        n -= saturate(1 - (d - CLEAR_RADIUS) / 700) * 0.5;
    }
    world[id.x] = n;
    for (int f = 0; f < FIELDS; f++)
        world[uint(CELLS) * uint(1 + f) + id.x] = INF;
}

/// Everyone starts dead, rising over the first seconds at their bases.
[shader("compute")]
[numthreads(64, 1, 1)]
void initUnits(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i < CELLS)
        cells[i] = (Cell)0;
    if (i >= MAX_UNITS)
        return;
    Unit u = (Unit)0;
    u.respawn = 0.2 + random(i, 7u) * 5;
    u.pos = sitePosition(int(factionOf(i)));
    units[i] = u;
    Hit h = (Hit)0;
    hits[i] = h;
}

// ---- compute: every frame -----------------------------------------------------------

/// One thread: camera, selection box, orders. Raw input in, intentions out.
[shader("compute")]
[numthreads(64, 1, 1)]
void frameBegin(uint3 id : SV_DispatchThreadID)
{
    if (id.x == 0)
    {
        args[ARG_UNITS + 0] = 6;
        args[ARG_UNITS + 1] = 0;
        args[ARG_UNITS + 2] = 0;
        args[ARG_UNITS + 3] = 0;
        args[ARG_TRACERS + 0] = 6;
        args[ARG_TRACERS + 1] = 0;
        args[ARG_TRACERS + 2] = 0;
        args[ARG_TRACERS + 3] = 0;
        args[ARG_FX + 0] = 6;
        args[ARG_FX + 1] = 0;
        args[ARG_FX + 2] = 0;
        args[ARG_FX + 3] = 0;
    }
    if (id.x != 0)
        return;

    float2 size = float2(frame.width, frame.height);
    float2 mouse = float2(frame.mouseX, frame.mouseY);
    bool over = all(mouse >= 0) && all(mouse < size);
    uint buttons = frame.buttons;
    uint before = game[0].buttonsBefore;
    uint pressed = buttons & ~before;
    uint released = before & ~buttons;
    uint keys = frame.keys;
    uint keyPressed = keys & ~game[0].keysBefore;
    float4 camera = game[0].camera;
    float fit = camera.w;

    game[0].selectApply = 0;
    game[0].orderApply = 0;

    // Zoom about the pointer: the world point under it stays put.
    float zoomBy = pow(1.18, float(frame.wheel)) * pow(2.2, frame.frameDt * (float((keys & KEY_E) != 0) - float((keys & KEY_Q) != 0)));
    if (zoomBy != 1)
    {
        float2 anchor = over && frame.wheel != 0 ? mouse : size * 0.5;
        float2 under = toWorld(anchor, camera, size);
        camera.z = clamp(camera.z * zoomBy, fit * 0.8, 4.0);
        camera.xy = under - (anchor - size * 0.5) / camera.z;
    }

    // Keys pan at a steady speed on screen; the middle button drags the world.
    float2 pan = float2(float((keys & (KEY_D | KEY_RIGHT)) != 0) - float((keys & (KEY_A | KEY_LEFT)) != 0),
                        float((keys & (KEY_S | KEY_DOWN)) != 0) - float((keys & (KEY_W | KEY_UP)) != 0));
    camera.xy += pan * 1100 * frame.frameDt / camera.z;
    if ((buttons & MOUSE_MIDDLE) != 0 && (before & MOUSE_MIDDLE) != 0)
        camera.xy -= (mouse - game[0].mouseBefore) / camera.z;
    if ((keyPressed & KEY_F) != 0)
        camera = float4(WORLD * 0.5, fit, fit);
    for (int f = 0; f < FACTIONS; f++)
    {
        uint alive = game[0].armies[f].alive;
        if ((keyPressed & (KEY_DIGIT1 << f)) != 0 && alive > 0)
            camera.xy = float2(game[0].armies[f].sumX, game[0].armies[f].sumY) * 8.0 / float(alive);
    }

    // The minimap: press on it to look there, and keep dragging.
    float2 origin = minimapOrigin(size);
    if ((pressed & MOUSE_LEFT) != 0 && onMinimap(mouse, size))
        game[0].dragMinimap = 1;
    if ((buttons & MOUSE_LEFT) == 0)
        game[0].dragMinimap = 0;
    if (game[0].dragMinimap != 0)
        camera.xy = (mouse - origin) / MINIMAP_SIZE * WORLD;

    camera.xy = clamp(camera.xy, float2(0, 0), WORLD);
    game[0].camera = camera;

    // Box select for the player's army; a click is a small box.
    if ((pressed & MOUSE_LEFT) != 0 && over && game[0].dragMinimap == 0)
    {
        game[0].boxActive = 1;
        game[0].box = float4(mouse, mouse);
    }
    if (game[0].boxActive != 0)
        game[0].box.zw = mouse;
    if ((released & MOUSE_LEFT) != 0 && game[0].boxActive != 0)
    {
        float2 a = min(game[0].box.xy, game[0].box.zw) - 6;
        float2 b = max(game[0].box.xy, game[0].box.zw) + 6;
        game[0].selection = float4(toWorld(a, camera, size), toWorld(b, camera, size));
        game[0].selectApply = (keys & KEY_SHIFT) != 0 ? 2 : 1;
        game[0].boxActive = 0;
    }
    if ((keyPressed & KEY_ESCAPE) != 0)
    {
        game[0].selection = float4(-1, -1, -1, -1);
        game[0].selectApply = 1;
    }

    // A right click orders the selection there (on the map or the minimap),
    // and seeds a flow field of its own for them to follow.
    if ((pressed & MOUSE_RIGHT) != 0 && over && game[0].armies[PLAYER].selected > 0)
    {
        float2 dest = onMinimap(mouse, size) ? (mouse - origin) / MINIMAP_SIZE * WORLD : toWorld(mouse, camera, size);
        dest = clamp(dest, float2(1, 1), WORLD - 1);
        uint slot = game[0].orderNext % ORDERS;
        game[0].orderNext++;
        game[0].orders[slot].dest = dest;
        game[0].orders[slot].serial++;
        game[0].fieldAge[SITES + slot] = 0;
        game[0].orderApply = slot + 1;
        pushEffect(dest, 180, PLAYER, FX_ORDER);
    }

    game[0].mouseBefore = mouse;
    game[0].buttonsBefore = buttons;
    game[0].keysBefore = keys;
}

/// A field whose goal just appeared starts from the cells round its goal.
[shader("compute")]
[numthreads(64, 1, 1)]
void seedFields(uint3 id : SV_DispatchThreadID)
{
    uint f = id.x / uint(CELLS);
    uint i = id.x % uint(CELLS);
    if (f >= FIELDS || game[0].fieldAge[f] != 0)
        return;
    float2 goal = f < SITES ? game[0].sites[f].pos : game[0].orders[f - SITES].dest;
    int2 c = int2(int(i) % GX, int(i) / GX);
    float d = distance(cellCentre(c), goal) / CELL;
    world[uint(CELLS) * (1 + f) + i] = world[i] <= ROCK && d <= 2.5 ? d : INF;
}

/// One relaxation of every unsettled field, in place: each cell takes its
/// cheapest neighbour plus the step. Values only fall, so stale reads from
/// other threads are still upper bounds and the in-place race is harmless.
[shader("compute")]
[numthreads(64, 1, 1)]
void relax(uint3 id : SV_DispatchThreadID)
{
    uint f = id.x / uint(CELLS);
    uint i = id.x % uint(CELLS);
    if (f >= FIELDS || game[0].fieldAge[f] > 240 || world[i] > ROCK)
        return;
    int2 c = int2(int(i) % GX, int(i) / GX);
    uint base = uint(CELLS) * (1 + f);
    float best = world[base + i];
    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 n = c + int2(dx, dy);
            if ((dx == 0 && dy == 0) || !inGrid(n) || world[cellIndex(n)] > ROCK)
                continue;
            if (dx != 0 && dy != 0 && (world[cellIndex(c + int2(dx, 0))] > ROCK || world[cellIndex(c + int2(0, dy))] > ROCK))
                continue;
            best = min(best, world[base + cellIndex(n)] + (dx != 0 && dy != 0 ? 1.4142 : 1.0));
        }
    }
    world[base + i] = best;
}

// ---- compute: the step ------------------------------------------------------------

[shader("compute")]
[numthreads(64, 1, 1)]
void clearCells(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i < CELLS)
    {
        cells[i].count = 0;
        for (int f = 0; f < FACTIONS; f++)
            cells[i].density[f] = 0;
    }
    if (i < FACTIONS)
    {
        game[0].armies[i].alive = 0;
        game[0].armies[i].sumX = 0;
        game[0].armies[i].sumY = 0;
        game[0].armies[i].selected = 0;
    }
    if (i < SITES)
        for (int f = 0; f < FACTIONS; f++)
            game[0].sites[i].presence[f] = 0;
    if (i < FACTIONS * SQUADS)
    {
        game[0].squads[i].sumX = 0;
        game[0].squads[i].sumY = 0;
        game[0].squads[i].count = 0;
    }
}

[shader("compute")]
[numthreads(64, 1, 1)]
void count(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i >= frame.count)
        return;
    Unit u = units[i];
    if ((u.flags & ALIVE) == 0)
        return;
    uint f = factionOf(i);
    uint c = cellIndex(cellOf(u.pos));
    InterlockedAdd(cells[c].count, 1u);
    InterlockedAdd(cells[c].density[f], 1u);
    InterlockedAdd(game[0].armies[f].alive, 1u);
    int2 p = int2(u.pos / 8);
    InterlockedAdd(game[0].armies[f].sumX, p.x);
    InterlockedAdd(game[0].armies[f].sumY, p.y);
    if ((u.flags & SELECTED) != 0)
        InterlockedAdd(game[0].armies[f].selected, 1u);
    uint q = f * SQUADS + squadOf(i);
    InterlockedAdd(game[0].squads[q].sumX, p.x);
    InterlockedAdd(game[0].squads[q].sumY, p.y);
    InterlockedAdd(game[0].squads[q].count, 1u);
    for (int s = 0; s < SITES; s++)
    {
        float2 d = u.pos - game[0].sites[s].pos;
        if (dot(d, d) < CAPTURE_RADIUS * CAPTURE_RADIUS)
            InterlockedAdd(game[0].sites[s].presence[f], 1u);
    }
}

groupshared uint partial[256];

/// One group of 256: grid counts become run starts; the total sizes `think`.
[shader("compute")]
[numthreads(256, 1, 1)]
void scan(uint3 local : SV_GroupThreadID)
{
    uint t = local.x;
    uint per = (uint(CELLS) + 255) / 256;
    uint begin = t * per;
    uint sum = 0;
    for (uint k = 0; k < per; k++)
        if (begin + k < uint(CELLS))
            sum += cells[begin + k].count;
    partial[t] = sum;
    GroupMemoryBarrierWithGroupSync();
    for (uint offset = 1; offset < 256; offset <<= 1)
    {
        uint add = t >= offset ? partial[t - offset] : 0;
        GroupMemoryBarrierWithGroupSync();
        partial[t] += add;
        GroupMemoryBarrierWithGroupSync();
    }
    uint run = partial[t] - sum;
    for (uint k = 0; k < per; k++)
    {
        uint c = begin + k;
        if (c < uint(CELLS))
        {
            cells[c].start = run;
            cells[c].cursor = run;
            run += cells[c].count;
        }
    }
    if (t == 255)
    {
        uint total = partial[255];
        game[0].aliveTotal = total;
        args[ARG_THINK + 0] = (total + 63) / 64;
        args[ARG_THINK + 1] = 1;
        args[ARG_THINK + 2] = 1;
    }
}

[shader("compute")]
[numthreads(64, 1, 1)]
void scatter(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    if (i >= frame.count || (units[i].flags & ALIVE) == 0)
        return;
    uint slot;
    InterlockedAdd(cells[cellIndex(cellOf(units[i].pos))].cursor, 1u, slot);
    sorted[slot] = i;
}

/// Threads 0-29 run the sites, 32-91 the squads, 96-100 the armies' caps.
[shader("compute")]
[numthreads(64, 1, 1)]
void strategy(uint3 id : SV_DispatchThreadID)
{
    uint i = id.x;
    float dt = frame.dt;
    if (i < SITES)
    {
        Site s = game[0].sites[i];
        int first = -1;
        uint most = 0, second = 0;
        for (int f = 0; f < FACTIONS; f++)
        {
            uint p = s.presence[f];
            if (p > most) { second = most; most = p; first = f; }
            else if (p > second) second = p;
        }
        bool holds = first >= 0 && most >= 3 && most > second * 2;
        if (holds && first != s.owner)
        {
            if (s.capturer != first)
            {
                s.capturer = first;
                s.progress = 0;
            }
            s.progress += dt / CAPTURE_TIME * min(1.0, float(most) / 10.0);
            if (s.progress >= 1)
            {
                s.owner = first;
                s.capturer = -1;
                s.progress = 0;
                pushEffect(s.pos, CAPTURE_RADIUS * 1.6, uint(first), FX_CAPTURE);
            }
        }
        else if (!holds || first == s.owner)
        {
            s.progress = max(0.0, s.progress - dt / CAPTURE_TIME * 0.5);
            if (s.progress <= 0)
                s.capturer = -1;
        }
        game[0].sites[i] = s;
        return;
    }

    if (i >= 32 && i < 32 + FACTIONS * SQUADS)
    {
        uint q = i - 32;
        int f = int(q / SQUADS);
        Squad squad = game[0].squads[q];
        squad.timer -= dt;
        bool lost = squad.objective < 0;
        if (!lost)
        {
            Site target = game[0].sites[squad.objective];
            uint enemies = 0;
            for (int g = 0; g < FACTIONS; g++)
                if (g != f) enemies += target.presence[g];
            // Taken and quiet: time to move on.
            lost = target.owner == f && enemies < 3 && squad.timer < 4;
        }
        if (squad.timer <= 0 || lost)
        {
            float2 centre = squad.count > 0
                ? float2(squad.sumX, squad.sumY) * 8.0 / float(squad.count)
                : game[0].sites[f].pos;
            float bestScore = -INF;
            int best = f;
            for (int s = 0; s < SITES; s++)
            {
                Site site = game[0].sites[s];
                uint enemies = 0;
                for (int g = 0; g < FACTIONS; g++)
                    if (g != f) enemies += site.presence[g];
                float far = distance(centre, site.pos) / WORLD.x;
                float score;
                if (site.owner == f)
                    score = enemies >= 3 ? 2.6 - far * 3 : -INF;     // defend it, or leave it be
                else
                    score = (site.owner == NEUTRAL ? 2.0 : 1.7) - far * 4 + (s < FACTIONS ? 0.3 : 0);
                if (s == squad.objective)
                    score += 0.6;
                // Spread out: each other squad already headed there costs some.
                for (uint other = uint(f) * SQUADS; other < uint(f + 1) * SQUADS; other++)
                    if (other != q && game[0].squads[other].objective == s)
                        score -= 0.45;
                score += random(q * 64 + uint(s), uint(frame.time * 0.2)) * 0.5;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            squad.objective = best;
            squad.timer = 6 + random(q, uint(frame.time)) * 6;
        }
        game[0].squads[q] = squad;
        return;
    }

    if (i >= 96 && i < 96 + FACTIONS)
    {
        uint f = i - 96;
        uint owned = 0;
        for (int s = 0; s < SITES; s++)
            if (game[0].sites[s].owner == int(f))
                owned++;
        uint slots = frame.count / FACTIONS;
        game[0].armies[f].owned = owned;
        game[0].armies[f].cap = uint(float(slots) * saturate(0.3 + 0.07 * float(owned)));
    }
}

/// Artillery's shell: every enemy of `f` near `centre` takes some, the most at the middle.
void splash(float2 centre, uint f, float damage)
{
    int2 c0 = cellOf(centre);
    uint budget = 128;
    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 c = c0 + int2(dx, dy);
            if (!inGrid(c))
                continue;
            uint start = cells[cellIndex(c)].start, n = cells[cellIndex(c)].count;
            for (uint k = 0; k < n && budget > 0; k++, budget--)
            {
                uint j = sorted[start + k];
                if (factionOf(j) == f)
                    continue;
                float d = distance(units[j].pos, centre);
                if (d > SPLASH)
                    continue;
                float dealt = max(1.0, damage * (1 - d / SPLASH * SPLASH_SHARE) - ARMOR[typeOf(j)]);
                InterlockedAdd(hits[j].damage, int(dealt * 16));
                hits[j].by = f + 1;
            }
        }
    }
}

/// A thread per living unit, in grid order (dispatched indirectly from the scan's total).
[shader("compute")]
[numthreads(64, 1, 1)]
void think(uint3 tid : SV_DispatchThreadID)
{
    if (tid.x >= game[0].aliveTotal)
        return;
    uint id = sorted[tid.x];
    Unit u = units[id];
    uint f = factionOf(id), t = typeOf(id);
    float dt = frame.dt;
    float range = RANGE[t];
    float reach = max(SIGHT, range + 40);
    int r = int(ceil(reach / CELL));
    int2 c0 = cellOf(u.pos);

    float bestScore = INF;
    int best = -1;
    uint bestType = 0;
    float bestDist = 0;
    float2 bestPos = 0;
    int heal = -1;
    float healShare = 0.95;
    float2 healPos = 0;
    float nearestEnemy = INF;
    float2 nearestEnemyPos = 0;
    float2 push = 0;
    bool guarded = false, buffed = false;
    uint budget = SCAN_BUDGET;

    for (int ring = 0; ring <= r && budget > 0; ring++)
    {
        // Nearest rings first, so a spent budget leaves out the far ones.
        for (int dy = -ring; dy <= ring && budget > 0; dy++)
        {
            for (int dx = -ring; dx <= ring && budget > 0; dx++)
            {
                if (max(abs(dx), abs(dy)) != ring)
                    continue;
                int2 c = c0 + int2(dx, dy);
                if (!inGrid(c))
                    continue;
                uint ci = cellIndex(c);
                uint start = cells[ci].start, n = cells[ci].count;
                for (uint k = 0; k < n && budget > 0; k++)
                {
                    uint j = sorted[start + k];
                    if (j == id)
                        continue;
                    budget--;
                    float2 v = units[j].pos;
                    float2 delta = v - u.pos;
                    float d2 = dot(delta, delta);
                    if (d2 > reach * reach)
                        continue;
                    float d = sqrt(d2);
                    uint fj = factionOf(j), tj = typeOf(j);
                    float room = (SIZE[t] + SIZE[tj]) * 0.9 + 3;
                    if (d < room)
                        push -= (d > 0.01 ? delta / d : randomDisc(id, j)) * (room - d) / room;
                    if (fj == f)
                    {
                        if (tj == GUARDIAN && d < AURA) guarded = true;
                        if (tj == COMMANDER && d < BUFF) buffed = true;
                        if (t == MEDIC)
                        {
                            float share = units[j].hp / HP[tj];
                            if (share < healShare)
                            {
                                healShare = share;
                                heal = int(j);
                                healPos = v;
                            }
                        }
                        continue;
                    }
                    if (d < nearestEnemy)
                    {
                        nearestEnemy = d;
                        nearestEnemyPos = v;
                    }
                    if (t == MEDIC)
                        continue;
                    float score = d / bonus(t, tj);
                    if (t == ARTILLERY && d < ARTILLERY_MIN)
                        score += 1e5;
                    if (j == u.target)
                        score *= 0.85;   // keep on the same one
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = int(j);
                        bestType = tj;
                        bestDist = d;
                        bestPos = v;
                    }
                }
            }
        }
    }

    // Wounded units fall back to the nearest site their army holds, and heal there.
    float hpShare = u.hp / HP[t];
    bool retreating = (u.flags & RETREATING) != 0;
    if (hpShare < RETREAT_AT[t])
        retreating = true;
    if (hpShare > 0.9)
        retreating = false;
    int home = -1;
    if (retreating)
    {
        float closest = INF;
        for (int s = 0; s < SITES; s++)
        {
            if (game[0].sites[s].owner != int(f))
                continue;
            float d = distance(u.pos, game[0].sites[s].pos);
            if (d < closest) { closest = d; home = s; }
        }
        retreating = home >= 0 && closest > CAPTURE_RADIUS * 0.5;
    }

    uint slot;
    bool ordered = orderLive(u.order, slot);
    float2 dest = ordered ? game[0].orders[slot].dest + randomDisc(id, 5u) * 260 : 0;
    bool marching = ordered && distance(u.pos, dest) > ORDER_ARRIVE;

    float2 desired = 0;
    if (retreating)
        desired = flowDirection(home, u.pos, game[0].sites[home].pos);
    else if (marching)
        desired = flowDirection(SITES + int(slot), u.pos, dest);   // a move order: shoot, but don't stop to chase
    else if (t == MEDIC && heal >= 0)
        desired = distance(healPos, u.pos) > range * 0.7 ? normalize(healPos - u.pos) : 0;
    else if (best >= 0 && !(ordered && distance(bestPos, dest) > 500))
    {
        float hold = HOLD_AT[t] * range;
        if (KITE[t] > 0 && nearestEnemy < KITE[t])
            desired = normalize(u.pos - nearestEnemyPos);
        else if (bestDist > hold)
            desired = normalize(bestPos - u.pos);
        else
            desired = float2(-(bestPos - u.pos).y, (bestPos - u.pos).x) / max(bestDist, 1) * (random(id, uint(frame.time)) - 0.5) * 0.6;
    }
    else if (ordered)
    {
        float2 to = dest - u.pos;
        desired = to / max(length(to), 80);
    }
    else
    {
        int objective = game[0].squads[f * SQUADS + squadOf(id)].objective;
        if (objective < 0)
            objective = int(f);
        float2 site = game[0].sites[objective].pos;
        if (distance(u.pos, site) > CAPTURE_RADIUS * 0.75)
            desired = flowDirection(objective, u.pos, site);
        else
        {
            float2 to = site + randomDisc(id, 9u) * CAPTURE_RADIUS * 0.7 - u.pos;
            desired = to / max(length(to), 120);
        }
    }

    desired += push * 1.6;
    float len = length(desired);
    if (len > 1)
        desired /= len;
    float speed = SPEED[t] * (retreating ? 1.15 : 1.0);
    u.vel = lerp(u.vel, desired * speed, 1 - exp(-8 * dt));
    if (best >= 0 && !retreating && bestDist <= range * 1.2)
        u.heading = atan2(bestPos.y - u.pos.y, bestPos.x - u.pos.x);
    else if (dot(u.vel, u.vel) > 25)
        u.heading = atan2(u.vel.y, u.vel.x);

    // Fire.
    u.cooldown -= dt;
    u.shot = max(0.0, u.shot - dt);
    uint flags = u.flags & (ALIVE | SELECTED);
    if (!retreating && u.cooldown <= 0)
    {
        if (t == MEDIC)
        {
            if (heal >= 0 && distance(healPos, u.pos) <= range)
            {
                InterlockedAdd(hits[uint(heal)].damage, -int(DAMAGE[t] * 16));
                u.cooldown = COOLDOWN[t];
                u.shot = TRACER;
                u.shotTo = healPos;
                u.target = uint(heal);
            }
        }
        else if (best >= 0 && bestDist <= range
                 && (t == ARTILLERY ? bestDist >= ARTILLERY_MIN : lineOfSight(u.pos, bestPos)))
        {
            float damage = DAMAGE[t] * bonus(t, bestType) * (buffed ? BUFF_SHARE : 1.0);
            if (t == BERSERKER)
                damage *= 1 + RAGE * (1 - hpShare);
            if (t == ARTILLERY)
            {
                float2 landing = bestPos + randomDisc(id, uint(frame.time * 60)) * 40;
                splash(landing, f, damage);
                pushEffect(landing, SPLASH * 1.4, f, FX_BLAST);
            }
            else
            {
                InterlockedAdd(hits[best].damage, int(max(1.0, damage - ARMOR[bestType]) * 16));
                hits[best].by = f + 1;
            }
            u.cooldown = COOLDOWN[t] * (0.85 + 0.3 * random(id, uint(frame.time * 60)));
            u.shot = t == ARTILLERY ? 0.3 : TRACER;
            u.shotTo = bestPos;
            u.target = uint(best);
            flags |= FIRED;
        }
    }
    if (retreating) flags |= RETREATING;
    if (guarded) flags |= GUARDED;
    if (buffed) flags |= BUFFED;
    u.flags = flags;
    units[id] = u;
}

/// Damage lands; the dead credit their killer and wait; the waiting rise; the living move.
[shader("compute")]
[numthreads(64, 1, 1)]
void resolve(uint3 tid : SV_DispatchThreadID)
{
    uint id = tid.x;
    if (id >= frame.count)
        return;
    Unit u = units[id];
    Hit h = hits[id];
    hits[id] = (Hit)0;
    uint f = factionOf(id), t = typeOf(id);
    float dt = frame.dt;

    if ((u.flags & ALIVE) != 0)
    {
        float damage = float(h.damage) / 16;
        if (damage > 0 && (u.flags & GUARDED) != 0)
            damage *= AURA_SHARE;
        u.hp = min(HP[t], u.hp - damage);
        if (u.hp < HP[t])
        {
            for (int s = 0; s < SITES; s++)
            {
                if (game[0].sites[s].owner == int(f) && distance(u.pos, game[0].sites[s].pos) < CAPTURE_RADIUS)
                {
                    u.hp = min(HP[t], u.hp + REGEN * dt);
                    break;
                }
            }
        }
        if (u.hp <= 0)
        {
            if (h.by > 0)
                InterlockedAdd(game[0].armies[h.by - 1].kills, 1u);
            InterlockedAdd(game[0].armies[f].deaths, 1u);
            pushEffect(u.pos, SIZE[t] * 3.5, f, FX_DEATH);
            u.flags = 0;
            u.respawn = RESPAWN[t] * (0.8 + 0.4 * random(id, uint(frame.time * 60)));
            u.shot = 0;
            u.order = 0;
            u.vel = 0;
        }
        else
        {
            float2 next = clamp(u.pos + u.vel * dt, float2(1, 1), WORLD - 1);
            if (rockAt(next) && !rockAt(u.pos))
            {
                float2 alongX = float2(next.x, u.pos.y), alongY = float2(u.pos.x, next.y);
                if (!rockAt(alongX)) { next = alongX; u.vel.y = 0; }
                else if (!rockAt(alongY)) { next = alongY; u.vel.x = 0; }
                else { next = u.pos; u.vel = 0; }
            }
            u.pos = next;
        }
        units[id] = u;
        return;
    }

    u.respawn -= dt;
    if (u.respawn > 0)
    {
        units[id] = u;
        return;
    }

    // Rise at the site nearest the squad's objective that the army holds and that raises this type.
    int objective = game[0].squads[f * SQUADS + squadOf(id)].objective;
    float2 aim = game[0].sites[objective >= 0 ? objective : int(f)].pos;
    int at = -1;
    float closest = INF;
    for (int s = 0; s < SITES; s++)
    {
        Site site = game[0].sites[s];
        if (site.owner != int(f) || !siteRaises(site.kind, t))
            continue;
        float d = distance(site.pos, aim);
        if (d < closest) { closest = d; at = s; }
    }
    uint before;
    if (at < 0)
    {
        u.respawn = 1;
    }
    else
    {
        InterlockedAdd(game[0].armies[f].alive, 1u, before);
        if (before >= game[0].armies[f].cap)
        {
            InterlockedAdd(game[0].armies[f].alive, 0xFFFFFFFFu);
            u.respawn = 0.5 + random(id, uint(frame.time * 60));
        }
        else
        {
            float2 site = game[0].sites[at].pos;
            float2 pos = site + randomDisc(id, uint(frame.time * 60)) * 260;
            u.pos = rockAt(pos) ? site : pos;
            u.vel = 0;
            u.hp = HP[t];
            u.flags = ALIVE;
            u.cooldown = random(id, 31u) * COOLDOWN[t];
            u.heading = random(id, 32u) * 6.2831853;
            u.order = 0;
            u.shot = 0;
            u.target = 0xFFFFFFFFu;
        }
    }
    units[id] = u;
}

/// Selection and orders land; visible units, shots and effects are packed for drawing.
[shader("compute")]
[numthreads(64, 1, 1)]
void cull(uint3 tid : SV_DispatchThreadID)
{
    uint i = tid.x;
    float4 camera = game[0].camera;
    float2 size = float2(frame.width, frame.height);

    if (i < FIELDS)
        game[0].fieldAge[i] = min(game[0].fieldAge[i] + 1, 1000000u);

    if (i < FX)
    {
        Effect e = game[0].fx[i];
        float age = (frame.time - e.birth) / FX_LIFE[e.kind];
        float2 s = toScreen(e.pos, camera, size);
        float reach = e.radius * camera.z + 4;
        if (age >= 0 && age < 1 && all(s > -reach) && all(s < size + reach))
        {
            uint slot;
            InterlockedAdd(args[ARG_FX + 1], 1u, slot);
            lists[2 * MAX_UNITS + slot] = i;
        }
    }

    if (i >= frame.count)
        return;
    Unit u = units[i];
    uint f = factionOf(i);
    bool alive = (u.flags & ALIVE) != 0;
    uint flags = u.flags;
    uint order = u.order;
    uint apply = game[0].selectApply;
    if (apply != 0)
    {
        float4 box = game[0].selection;
        bool inside = alive && f == PLAYER && all(u.pos >= box.xy) && all(u.pos <= box.zw);
        if (inside)
            flags |= SELECTED;
        else if (apply == 1)
            flags &= ~SELECTED;
    }
    if (!alive)
        flags &= ~SELECTED;
    uint issued = game[0].orderApply;
    if (issued != 0 && (flags & SELECTED) != 0)
        order = issued | (game[0].orders[issued - 1].serial << 4);
    if (flags != u.flags || order != u.order)
    {
        units[i].flags = flags;
        units[i].order = order;
    }
    if (!alive)
        return;

    float2 s = toScreen(u.pos, camera, size);
    float reach = max(SIZE[typeOf(i)] * camera.z, 1.5) * 2.4 + 2;
    if (all(s > -reach) && all(s < size + reach))
    {
        uint slot;
        InterlockedAdd(args[ARG_UNITS + 1], 1u, slot);
        lists[slot] = i;
    }
    if (u.shot > 0)
    {
        float2 e = toScreen(u.shotTo, camera, size);
        if (all(max(s, e) > -4) && all(min(s, e) < size + 4))
        {
            uint slot;
            InterlockedAdd(args[ARG_TRACERS + 1], 1u, slot);
            lists[MAX_UNITS + slot] = i;
        }
    }
}

// ---- drawing -------------------------------------------------------------------------

static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

float4 toClip(float2 s, float2 size)
{
    float2 ndc = s / size * 2 - 1;
    return float4(ndc.x, -ndc.y, 0, 1);
}

struct UnitVaryings
{
    float4 position : SV_Position;
    float2 local : TEXCOORD0;                     // px from the centre, turned to the heading
    nointerpolation float4 colour : TEXCOORD1;    // rgb, hp share
    nointerpolation float4 style : TEXCOORD2;     // radius px, type, flags, unused
    float2 flat : TEXCOORD3;                      // px from the centre, unturned (the health bar)
};

[shader("vertex")]
UnitVaryings unitVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    uint id = listsV[instance];
    Unit u = unitsV[id];
    uint t = typeOf(id);
    float4 camera = gameV[0].camera;
    float2 size = float2(frameV.width, frameV.height);
    float radius = max(SIZE[t] * camera.z, 1.4);
    float extent = radius * 2.2 + 1.5;
    float2 corner = Corners[vertex];
    UnitVaryings o;
    o.position = toClip(toScreen(u.pos, camera, size) + corner * extent, size);
    float c = cos(u.heading), s = sin(u.heading);
    float2 local = corner * extent;
    o.local = float2(local.x * c + local.y * s, -local.x * s + local.y * c);
    o.flat = local;
    float3 colour = COLOURS[factionOf(id)];
    if ((u.flags & RETREATING) != 0)
        colour *= 0.6;
    if (u.shot > TRACER * 0.5 && t != MEDIC)
        colour = lerp(colour, 1, 0.55);
    o.colour = float4(colour, u.hp / HP[t]);
    o.style = float4(radius, float(t), float(u.flags), 0);
    return o;
}

float shapeDistance(uint t, float2 q)
{
    // q is in units of the radius, x along the heading.
    switch (t)
    {
    case SCOUT:     return max(max(-q.x - 0.6, abs(q.y) * 1.7 + q.x * 0.9 - 0.9), 0);
    case HEAVY:     { float2 d = abs(q) - 0.78; return length(max(d, 0)) + min(max(d.x, d.y), 0) - 0.12; }
    case SNIPER:    return abs(q.x) * 0.45 + abs(q.y) * 1.3 - 0.75;
    case ARTILLERY: { float2 p = abs(q); return max(p.x * 0.866 + p.y * 0.5, p.y) - 0.85; }
    case MEDIC:     { float2 p = abs(q); return min(max(p.x - 0.95, p.y - 0.32), max(p.x - 0.32, p.y - 0.95)); }
    case BERSERKER: { float2 p = abs(q); return (p.x + p.y) * 0.7071 - 0.72; }
    case GUARDIAN:  return min(length(q) - 0.55, abs(length(q) - 0.88) - 0.12);
    case COMMANDER: { float a = atan2(q.y, q.x); return length(q) - (0.78 + 0.22 * cos(a * 5)); }
    default:        return length(q) - 0.9;
    }
}

[shader("fragment")]
float4 unitShade(UnitVaryings v) : SV_Target
{
    float radius = v.style.x;
    uint t = uint(v.style.y);
    uint flags = uint(v.style.z);
    float2 q = v.local / radius;
    float pixel = 1 / radius;
    float4 result = 0;

    float d = radius < 2.5 ? length(q) - 0.9 : shapeDistance(t, q);
    float body = saturate(0.5 - d / pixel);
    float rim = saturate(0.5 - (d + 1.6 * pixel) / pixel);
    float3 colour = lerp(v.colour.rgb * 0.35, v.colour.rgb, rim);
    result = float4(colour, 1) * body;

    if ((flags & SELECTED) != 0)
    {
        float ring = abs(length(q) - 1.55) - max(0.08, pixel);
        float a = saturate(0.5 - ring / pixel);
        result = lerp(result, float4(0.55, 1, 0.55, 1), a * (1 - result.a * 0.5));
    }

    // Hurt units carry a bar above them, once they are big enough to read.
    float share = v.colour.a;
    if (share < 0.999 && radius >= 4)
    {
        float2 bar = (v.flat - float2(0, -radius * 1.9)) / radius;
        if (abs(bar.x) < 1.1 && abs(bar.y) < 0.16 + pixel)
        {
            bool full = (bar.x + 1.1) / 2.2 < share;
            result = full ? float4(lerp(float3(1, 0.25, 0.2), float3(0.4, 1, 0.4), share), 1) : float4(0.05, 0.05, 0.07, 0.9);
        }
    }
    return result;
}

struct LineVaryings
{
    float4 position : SV_Position;
    float2 along : TEXCOORD0;                     // 0..1 along, -1..1 across
    nointerpolation float4 colour : TEXCOORD1;
};

[shader("vertex")]
LineVaryings tracerVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    uint id = listsV[MAX_UNITS + instance];
    Unit u = unitsV[id];
    uint t = typeOf(id);
    float4 camera = gameV[0].camera;
    float2 size = float2(frameV.width, frameV.height);
    float2 a = toScreen(u.pos, camera, size);
    float2 b = toScreen(u.shotTo, camera, size);
    float life = t == ARTILLERY ? 0.3 : TRACER;
    float left = u.shot / life;
    if (t == ARTILLERY)
    {
        // A shell: a short streak travelling from gun to landing.
        float head = 1 - left;
        float2 from = lerp(a, b, max(head - 0.15, 0));
        b = lerp(a, b, head);
        a = from;
    }
    float2 dir = b - a;
    float len = max(length(dir), 0.01);
    float2 n = float2(-dir.y, dir.x) / len;
    float width = t == ARTILLERY ? max(2.5, 3 * camera.z) : max(0.9, 1.4 * camera.z);
    float2 corner = Corners[vertex];
    float2 p = lerp(a, b, corner.x * 0.5 + 0.5) + n * corner.y * (width + 1);
    LineVaryings o;
    o.position = toClip(p, size);
    o.along = float2(corner.x * 0.5 + 0.5, corner.y * (width + 1) / width);
    float3 colour = t == MEDIC ? float3(0.5, 1, 0.6) : (t == ARTILLERY ? float3(1, 0.7, 0.3) : lerp(COLOURS[factionOf(id)], 1, 0.5));
    o.colour = float4(colour, saturate(left) * (t == SNIPER ? 1.0 : 0.75));
    return o;
}

[shader("fragment")]
float4 tracerShade(LineVaryings v) : SV_Target
{
    float edge = saturate(1.5 - abs(v.along.y) * 1.5);
    float a = v.colour.a * edge * (0.35 + 0.65 * v.along.x);
    return float4(v.colour.rgb * a, a);
}

struct EffectVaryings
{
    float4 position : SV_Position;
    float2 local : TEXCOORD0;                      // -1..1 across the effect
    nointerpolation float4 colour : TEXCOORD1;     // rgb, age 0..1
    nointerpolation float4 style : TEXCOORD2;      // kind, radius px
};

[shader("vertex")]
EffectVaryings effectVertex(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
{
    uint i = listsV[2 * MAX_UNITS + instance];
    Effect e = gameV[0].fx[i];
    float4 camera = gameV[0].camera;
    float2 size = float2(frameV.width, frameV.height);
    float radius = max(e.radius * camera.z, 3);
    float2 corner = Corners[vertex];
    EffectVaryings o;
    o.position = toClip(toScreen(e.pos, camera, size) + corner * radius, size);
    o.local = corner;
    float age = saturate((frameV.time - e.birth) / FX_LIFE[e.kind]);
    float3 colour = e.kind == FX_BLAST ? float3(1, 0.62, 0.25) : (e.kind == FX_ORDER ? float3(0.5, 1, 0.5) : COLOURS[e.faction]);
    o.colour = float4(colour, age);
    o.style = float4(float(e.kind), radius, 0, 0);
    return o;
}

[shader("fragment")]
float4 effectShade(EffectVaryings v) : SV_Target
{
    float age = v.colour.a;
    uint kind = uint(v.style.x);
    float px = 1 / v.style.y;
    float r = length(v.local);
    float a;
    if (kind == FX_ORDER)
    {
        float ring = 1 - age * 0.8;
        a = saturate(1 - abs(r - ring) / max(px * 1.5, 0.04)) * (1 - age);
    }
    else if (kind == FX_CAPTURE)
    {
        float ring = 0.3 + age * 0.7;
        a = saturate(1 - abs(r - ring) / max(px * 2, 0.03)) * (1 - age) * 0.9;
    }
    else
    {
        float ring = age;
        float flash = saturate(1 - r / max(0.6 * (1 - age), 0.05)) * (1 - age) * (kind == FX_BLAST ? 0.9 : 0.6);
        a = saturate(1 - abs(r - ring) / max(px * 1.5, 0.08)) * (1 - age) * 0.7 + flash;
    }
    return float4(v.colour.rgb * a, a);
}

// ---- full-stage passes: ground and HUD -----------------------------------------------

struct FullVaryings
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

[shader("vertex")]
FullVaryings full(uint vertex : SV_VertexID)
{
    float2 uv = float2((vertex << 1) & 2, vertex & 2);
    FullVaryings o;
    o.position = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
    o.uv = uv;
    return o;
}

float rockinessF(float2 p)
{
    // Bilinear over the cell values, so rock has round edges near the cells' own.
    float2 g = p / CELL - 0.5;
    int2 i = int2(floor(g));
    float2 f = frac(g);
    float a = worldF[cellIndex(clamp(i, int2(0, 0), int2(GX - 1, GY - 1)))];
    float b = worldF[cellIndex(clamp(i + int2(1, 0), int2(0, 0), int2(GX - 1, GY - 1)))];
    float c = worldF[cellIndex(clamp(i + int2(0, 1), int2(0, 0), int2(GX - 1, GY - 1)))];
    float d = worldF[cellIndex(clamp(i + int2(1, 1), int2(0, 0), int2(GX - 1, GY - 1)))];
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float siteShape(uint kind, float2 q)
{
    switch (kind)
    {
    case SITE_BASE:     { float2 p = abs(q); return max(p.x * 0.866 + p.y * 0.5, p.y) - 1; }
    case SITE_FACTORY:  { float2 d = abs(q) - 0.85; return length(max(d, 0)) + min(max(d.x, d.y), 0); }
    case SITE_FIREBASE: return max(abs(q.x) * 0.866 + q.y * 0.5, -q.y) - 0.6;
    case SITE_BARRACKS: return (abs(q.x) + abs(q.y)) * 0.7071 - 0.8;
    default:            return length(q) - 0.9;
    }
}

[shader("fragment")]
float4 ground(FullVaryings v) : SV_Target
{
    float2 size = float2(frameF.width, frameF.height);
    float2 s = v.uv * size;
    float4 camera = gameF[0].camera;
    float zoom = camera.z;
    float2 p = toWorld(s, camera, size);
    if (any(p < 0) || any(p > WORLD))
        return float4(0.02, 0.022, 0.03, 1);

    float px = 1 / zoom;   // world px per stage px
    float speckle = valueNoise(p / 90, 7u) * 0.6 + valueNoise(p / 23, 8u) * 0.4;
    float3 colour = lerp(float3(0.085, 0.10, 0.085), float3(0.12, 0.135, 0.11), speckle);

    // Faint cell lines once close enough to matter.
    float2 grid = abs(frac(p / CELL + 0.5) - 0.5) * CELL;
    float gridLine = saturate(1 - min(grid.x, grid.y) / px);
    colour += gridLine * 0.025 * saturate(zoom * 4 - 0.6);

    float rock = rockinessF(p);
    float solid = saturate((rock - ROCK) / (fwidth(rock) + 1e-4) + 0.5);
    float3 stone = lerp(float3(0.27, 0.25, 0.23), float3(0.36, 0.34, 0.31), saturate((rock - ROCK) * 6)) * (0.85 + speckle * 0.3);
    colour = lerp(colour * (1 - saturate((rock - ROCK + 0.06) * 10) * 0.35), stone, solid);

    // Flow field view: the field toward the site nearest the pointer.
    if (frameF.view == 1)
    {
        float2 mouse = toWorld(float2(frameF.mouseX, frameF.mouseY), camera, size);
        int field = 0;
        float closest = INF;
        for (int k = 0; k < SITES; k++)
        {
            float d = distance(mouse, gameF[0].sites[k].pos);
            if (d < closest) { closest = d; field = k; }
        }
        float dist = worldF[uint(CELLS) * uint(1 + field) + cellIndex(cellOf(p))];
        if (solid < 0.5)
        {
            if (dist >= INF * 0.5)
                colour = lerp(colour, float3(0.5, 0.1, 0.1), 0.5);
            else
            {
                float band = frac(dist / 6);
                colour = lerp(colour, lerp(float3(0.2, 0.9, 0.9), float3(0.1, 0.2, 0.6), saturate(dist / 120)), 0.35 + 0.15 * step(band, 0.5));
            }
        }
    }

    // Sites: a ring in the owner's colour, the core's shape says what it raises,
    // and an arc shows a capture under way.
    for (int k = 0; k < SITES; k++)
    {
        Site site = gameF[0].sites[k];
        float2 d = p - site.pos;
        float r = length(d);
        if (r > CAPTURE_RADIUS + 40 * px + 60)
            continue;
        float3 own = COLOURS[site.owner];
        colour = lerp(colour, own, 0.06 * saturate((CAPTURE_RADIUS - r) / px));
        float ring = abs(r - CAPTURE_RADIUS) - max(2.5, 1.2 * px);
        colour = lerp(colour, own, 0.75 * saturate(0.5 - ring / px));
        float core = siteShape(site.kind, d / 70);
        float coreA = saturate(0.5 - core * 70 / max(px, 1));
        colour = lerp(colour, lerp(own * 0.4, own, saturate(0.5 - (core * 70 + 6) / max(px, 1))), coreA);
        if (site.capturer >= 0)
        {
            float angle = (atan2(d.x, -d.y) / 6.2831853) + 0.5;
            float arc = abs(r - (CAPTURE_RADIUS + max(10, 6 * px))) - max(4, 2.5 * px);
            if (angle < site.progress)
                colour = lerp(colour, COLOURS[site.capturer], saturate(0.5 - arc / px));
        }
    }
    return float4(colour, 1);
}

/// 3×5 pixel digits, top row in the high bits.
static const uint Digits[10] = { 0x7B6F, 0x2C97, 0x73E7, 0x73CF, 0x5BC9, 0x79CF, 0x79EF, 0x7249, 0x7BEF, 0x7BCF };

/// Whether p is on a lit cell of `value` printed right-aligned to `right`, cells `cell` px square.
bool number(float2 p, float2 right, float cell, uint value)
{
    float2 q = (p - right) / cell;
    if (q.y < 0 || q.y >= 5 || q.x >= 0)
        return false;
    int column = int(floor(-q.x));
    int position = column / 4;
    int x = 2 - (column % 4);
    if (x < 0 || position > 8)
        return false;
    uint power = 1;
    for (int i = 0; i < position; i++)
        power *= 10;
    if (position > 0 && value < power)
        return false;
    uint digit = (value / power) % 10;
    uint bit = 14 - (uint(floor(q.y)) * 3 + uint(x));
    return ((Digits[digit] >> bit) & 1) != 0;
}

bool inRect(float2 p, float2 origin, float2 size) { return all(p >= origin) && all(p < origin + size); }

/// The selection box, the scoreboard and the minimap.
[shader("fragment")]
float4 overlay(FullVaryings v) : SV_Target
{
    float2 size = float2(frameF.width, frameF.height);
    float2 p = v.uv * size;
    float4 camera = gameF[0].camera;
    float4 result = 0;

    if (gameF[0].boxActive != 0)
    {
        float4 box = gameF[0].box;
        float2 a = min(box.xy, box.zw), b = max(box.xy, box.zw);
        if (inRect(p, a, b - a))
        {
            float2 e = min(p - a, b - p);
            result = min(e.x, e.y) < 1.5 ? float4(0.55, 1, 0.55, 0.9) : float4(0.3, 0.8, 0.3, 0.12);
        }
    }

    // Scoreboard: per army, a swatch, units up against everything it has, then
    // kills and sites held. Under it, how many of yours are selected.
    float2 board = float2(16, 16);
    float2 boardSize = float2(392, 14 + FACTIONS * 26 + 28);
    if (inRect(p, board, boardSize))
    {
        result = float4(0.02, 0.025, 0.035, 0.78);
        float2 q = p - board - float2(12, 12);
        int row = int(floor(q.y / 26));
        float y = q.y - float(row) * 26;
        uint slots = frameF.count / FACTIONS;
        if (row >= 0 && row < FACTIONS)
        {
            Army army = gameF[0].armies[row];
            float3 colour = COLOURS[row];
            if (q.x >= 0 && q.x < 14 && y >= 2 && y < 16)
                result = float4(colour, 1);
            float barX = q.x - 24;
            if (barX >= 0 && barX < 130 && y >= 5 && y < 13)
            {
                float alive = float(army.alive) / max(float(slots), 1);
                float cap = float(army.cap) / max(float(slots), 1);
                result = barX < alive * 130 ? float4(colour, 1)
                    : (barX < cap * 130 ? float4(colour * 0.3, 1) : float4(0.12, 0.13, 0.16, 1));
            }
            float2 cellP = float2(q.x, y);
            if (number(cellP, float2(240, 3), 2.4, army.alive)) result = float4(1, 1, 1, 1);
            if (number(cellP, float2(310, 3), 2.4, army.kills)) result = float4(1, 0.8, 0.5, 1);
            if (number(cellP, float2(366, 3), 2.4, army.owned)) result = float4(colour, 1);
        }
        if (row == FACTIONS)
        {
            uint selected = gameF[0].armies[PLAYER].selected;
            if (q.x >= 0 && q.x < 14 && y >= 6 && y < 20 && (abs(q.x - 7) > 4.5 || abs(y - 13) > 4.5))
                result = float4(0.55, 1, 0.55, 1);
            if (number(float2(q.x, y), float2(90, 7), 2.4, selected)) result = float4(0.55, 1, 0.55, 1);
        }
    }

    // Minimap: rock, who stands where, the sites, and what the camera sees.
    float2 origin = minimapOrigin(size);
    if (inRect(p, origin - 2, MINIMAP_SIZE + 4))
    {
        if (!inRect(p, origin, MINIMAP_SIZE))
            return float4(0.35, 0.38, 0.45, 1);
        float2 w = (p - origin) / MINIMAP_SIZE * WORLD;
        uint c = cellIndex(cellOf(w));
        float3 colour = worldF[c] > ROCK ? float3(0.2, 0.19, 0.18) : float3(0.05, 0.06, 0.055);
        uint most = 0;
        uint total = 0;
        int owner = -1;
        for (int f = 0; f < FACTIONS; f++)
        {
            uint n = cellsF[c].density[f];
            total += n;
            if (n > most) { most = n; owner = f; }
        }
        if (owner >= 0)
            colour = lerp(colour, COLOURS[owner], saturate(0.35 + sqrt(float(total)) * 0.15));
        for (int k = 0; k < SITES; k++)
        {
            Site site = gameF[0].sites[k];
            float2 sp = origin + site.pos / WORLD * MINIMAP_SIZE;
            float d = max(abs(p.x - sp.x), abs(p.y - sp.y));
            if (d < 4.5)
                colour = d < 3 ? COLOURS[site.owner] : float3(0, 0, 0);
        }
        float2 a = origin + toWorld(float2(0, 0), camera, size) / WORLD * MINIMAP_SIZE;
        float2 b = origin + toWorld(size, camera, size) / WORLD * MINIMAP_SIZE;
        float2 e = min(abs(p - a), abs(p - b));
        bool between = all(p >= a - 1) && all(p <= b + 1);
        if (between && min(e.x, e.y) < 1)
            colour = float3(1, 1, 1);
        result = float4(colour, 1);
    }
    return result;
}
