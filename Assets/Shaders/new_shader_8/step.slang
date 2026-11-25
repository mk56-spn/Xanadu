// STEP: items the GPU keeps from frame to frame, as many as [Pool(n)] on step says.
// start(i) makes item i after every compile; step moves it once a frame.
// The DEFORM brick draws each item once: it reads the one it is drawing as `item`.
import xanadu;

struct Item { float2 position; float2 velocity; };

Item start(uint i)
{
    Item t;
    t.position = (random2(float2(i, 1)) - 0.5) * 800;
    t.velocity = (random2(float2(i, 2)) - 0.5) * 200;
    return t;
}

[Pool(1000)]
void step(inout Item t, uint i, float dt)
{
    t.position += t.velocity * dt;
}
