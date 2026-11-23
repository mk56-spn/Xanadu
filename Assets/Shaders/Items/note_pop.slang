// A hit note's pop, in screen pixels round its middle: the note's body (a ring
// in its colour round a dark core, from the note designs, drawn at a 36-unit
// radius and scaled), swelling and fading, a shockwave racing out from its edge
// and thinning, and a white flash over it at the start. The drawer sizes the
// quad to hold the shockwave and sets the numbers each frame.
import xanadu;

// The pop's colour, faded.
[Default(1, 1, 1, 1)] uniform float4 pop_tint;
// The body's radius, the shockwave's radius and width, the flash's alpha (pixels).
[Default(36, 50, 4, 0)] uniform float4 pop_shape;
// The flash's radius.
[Default(36, 0, 0, 0)] uniform float4 pop_flash;

static const float4 Edge = float4(0.973, 0.98, 0.988, 1.0);
static const float4 Core = float4(0.118, 0.161, 0.231, 1.0);

float4 shade(ItemInput i)
{
    float r = length(i.canvas.centred);
    float radius = pop_shape.x;
    float k = radius / 36.0;
    float a = pop_tint.a;
    colour c = colours.transparent;

    // The body: a filled ring with a bright rim, a dark core with a bright rim.
    c.paint(sdf.filled(r - radius), pop_tint);
    c.paint(sdf.stroked(r - radius, 4.0 * k), float4(Edge.rgb, a));
    c.paint(sdf.filled(r - 20.0 * k), float4(Core.rgb, a));
    c.paint(sdf.stroked(r - 20.0 * k, 5.0 * k), float4(Edge.rgb, a));

    // The shockwave.
    c.paint(sdf.stroked(r - pop_shape.y, pop_shape.z), float4(pop_tint.rgb, a * 0.8));

    // The flash.
    if (pop_shape.w > 0.0)
        c.paint(sdf.filled(r - pop_flash.x), float4(1.0, 1.0, 1.0, pop_shape.w));

    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
