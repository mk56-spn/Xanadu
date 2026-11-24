// STEP: 10,000 dots the GPU keeps from frame to frame.
// start(i) scatters them after every compile; step pulls each one towards the
// attractor once a frame. The DEFORM brick below draws every dot once.
import xanadu;

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

static const uint count = 10000;

Dot start(uint i)
{
    Dot d;
    d.position = (random2(float2(i, 1)) - 0.5) * float2(1600, 900);
    d.velocity = float2(0, 0);
    d.size = lerp(3, 8, random(float2(i, 2)));
    d.hue = random(float2(i, 3));
    return d;
}

void step(inout Dot d, uint i, float dt)
{
    float2 towards = attractor - d.position;
    d.velocity += normalize(towards + 0.001) * pull * dt;
    // A little drag, so they swirl round the attractor instead of flying off.
    d.velocity *= exp(-dt * 0.8);
    d.position += d.velocity * dt;
}
