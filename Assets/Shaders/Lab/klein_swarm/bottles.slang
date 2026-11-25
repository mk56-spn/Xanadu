// 1000 Klein bottles drifting through a box and tumbling as they go. `move` scatters them after
// every compile (`made` says whether they exist yet), then moves and turns each one once a frame;
// `bottle` bends a grid into each one and lights it (look.slang).
import xanadu;

// How fast they tumble, in turns a second at most.
[Default(0.25)] uniform float spin;
// How fast they drift, in pixels a second at most.
[Default(60)] uniform float drift;

struct Bottle
{
    float3 position;    // pixels from the middle of the box (y down, z away)
    float3 velocity;    // a unit direction; drift sets the speed
    float3 angles;      // how it's turned, in radians
    float3 turning;     // turns a second, each from -1 to 1
    float3x3 turn;      // the same turn as a matrix, made once a frame here so no corner works out its sines
    float size;         // the ring's radius, in pixels
    float hue;
};

static const uint BOTTLES = 1000;

// The box they drift in, in pixels: they wrap round from one side to the other.
static const float3 Box = float3(1500, 860, 900);

/// A turn by x, then y, then z, in radians, as a matrix.
float3x3 turnOf(float3 a)
{
    float3 c = cos(a), s = sin(a);
    float3x3 x = float3x3(1, 0, 0, 0, c.x, -s.x, 0, s.x, c.x);
    float3x3 y = float3x3(c.y, 0, s.y, 0, 1, 0, -s.y, 0, c.y);
    float3x3 z = float3x3(c.z, -s.z, 0, s.z, c.z, 0, 0, 0, 1);
    return mul(z, mul(y, x));
}

Bottle start(uint i)
{
    Bottle b;
    b.position = (random3(float3(i, 1, 0)) - 0.5) * Box;
    b.velocity = normalize(random3(float3(i, 2, 0)) - 0.5 + 0.001);
    b.angles = random3(float3(i, 3, 0)) * TAU;
    b.turn = turnOf(b.angles);
    b.turning = random3(float3(i, 4, 0)) * 2 - 1;
    b.size = lerp(12, 26, random(float2(i, 5)));
    b.hue = random(float2(i, 6));
    return b;
}

[Buffer(BOTTLES)] RWStructuredBuffer<Bottle> bottles;
[Buffer(1)] RWStructuredBuffer<uint> made;

[Pass(64, over: BOTTLES)]
void move(uint i)
{
    Bottle b = bottles[i];
    if (made[0] == 0)
        b = start(i);
    b.angles += b.turning * spin * TAU * dt;
    b.turn = turnOf(b.angles);
    b.position += b.velocity * drift * dt;
    // Out of one side, in at the other.
    b.position = (frac(b.position / Box + 0.5) - 0.5) * Box;
    bottles[i] = b;
}

[Pass(1, over: 1)]
void ready(uint i) { made[0] = 1; }

void graph()
{
    move();
    ready();
    bottle(frame);
}
