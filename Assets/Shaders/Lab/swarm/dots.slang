// 10,000 dots the GPU keeps from frame to frame. `move` makes them after every compile (the
// buffers start zeroed, so `made` says whether they exist yet) and then pulls each one towards
// the attractor once a frame; `dot` draws each one, a copy of a quad apiece.
import xanadu;

static const uint DOTS = 10000;

// Where the dots are pulled to, in pixels from the centre: drag its dot on the preview.
[Default(0, 0)] uniform float2 attractor;
[Default(600)] uniform float pull;

struct Dot
{
    float2 position;    // pixels from the centre
    float2 velocity;    // pixels a second
    float size;         // width in pixels
    float hue;
};

[Buffer(DOTS)] RWStructuredBuffer<Dot> dots;
[Buffer(1)] RWStructuredBuffer<uint> made;

Dot start(uint i)
{
    Dot d;
    d.position = (random2(float2(i, 1)) - 0.5) * float2(1600, 900);
    d.velocity = float2(0, 0);
    d.size = lerp(3, 8, random(float2(i, 2)));
    d.hue = random(float2(i, 3));
    return d;
}

[Pass(64, over: DOTS)]
void move(uint i)
{
    Dot d = made[0] == 0 ? start(i) : dots[i];
    float2 towards = attractor - d.position;
    d.velocity += normalize(towards + 0.001) * pull * dt;
    // A little drag, so they swirl round the attractor instead of flying off.
    d.velocity *= exp(-dt * 0.8);
    d.position += d.velocity * dt;
    dots[i] = d;
}

[Pass(1, over: 1)]
void ready(uint i) { made[0] = 1; }

void graph()
{
    move();
    ready();
    dot(frame);
}
