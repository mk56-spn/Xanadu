// A hit dash's pop, in screen pixels round its middle: its bar flashing white,
// stretching out and thinning, with two sparks racing out along it. The drawer
// sizes the quad to hold them and sets the numbers each frame.
import xanadu;

// The bar's width and height, how far out the sparks are, their radius (pixels).
[Default(150, 10, 80, 10)] uniform float4 dashpop_shape;
// How faded it is (1 fresh, 0 gone).
[Default(1, 0, 0, 0)] uniform float4 dashpop_state;

float4 shade(ItemInput i)
{
    float fade = saturate(dashpop_state.x);

    colour c = colours.transparent;
    c.paint(Box(dashpop_shape.xy), colours.white.withAlpha(0.85 * fade * fade));
    // One spark out to the right, mirrored to the left.
    c.paint(Circle(dashpop_shape.w).moved(float2(dashpop_shape.z, 0)).mirrored(), colours.white.withAlpha(fade));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
