// Flashlight: darkness everywhere but a soft circle that trails the pointer.
// The light's place is kept state: update() eases it toward the mouse once a
// frame, and shade() reads where it got to as `state`.
import xanadu;

[Default(140)] uniform float radius;
[Default(8)] uniform float follow;

struct State
{
    float2 light;
};

void update(inout State s, float dt)
{
    // Frame-rate independent easing: the same feel at 60 or 240 fps.
    s.light = lerp(s.light, mouse, 1 - exp(-dt * follow));
}

float4 shade(Canvas canvas)
{
    float d = distance(canvas.centred, state.light);
    return colour(0, 0, 0, smoothstep(radius * 0.7, radius, d));
}
