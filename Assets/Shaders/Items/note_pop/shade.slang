// A hit note's pop, in screen pixels round its middle: the note's body (a ring
// in its colour round a dark core, from the note designs, drawn at a 36-unit
// radius and scaled), swelling and fading, a shockwave racing out from its edge
// and thinning, and a white flash over it at the start. The drawer sizes the
// quad to hold the shockwave and sets the numbers each frame.
import xanadu;

[Default(1, 1, 1, 1)] uniform colour pop_tint;
// The body's radius, the shockwave's radius and width, the flash's alpha (pixels).
[Default(36, 50, 4, 0)] uniform float4 pop_shape;
// The flash's radius.
[Default(36, 0, 0, 0)] uniform float4 pop_flash;

static const colour Rim = colour(0.973, 0.98, 0.988);
static const colour Core = colour(0.118, 0.161, 0.231);

float4 shade(ItemInput i)
{
    float radius = pop_shape.x;
    // The designs are drawn at a 36-unit radius; everything in the body scales with it.
    float k = radius / 36;
    float a = pop_tint.a;

    let body = Circle(radius).measured();
    let core = Circle(20 * k).measured();

    colour c = colours.transparent;
    c.paint(body, pop_tint);
    c.paint(body.stroked(4 * k), Rim.withAlpha(a));
    c.paint(core, Core.withAlpha(a));
    c.paint(core.stroked(5 * k), Rim.withAlpha(a));

    // The shockwave.
    c.paint(Circle(pop_shape.y).stroked(pop_shape.z), pop_tint.withAlpha(a * 0.8));

    // The flash.
    if (pop_shape.w > 0)
        c.paint(Circle(pop_flash.x), colours.white.withAlpha(pop_shape.w));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
