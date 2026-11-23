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
    float2 p = i.canvas.centred;
    float fade = saturate(dashpop_state.x);
    colour c = colours.transparent;
    c.paint(sdf.filled(Box(dashpop_shape.xy).local(p)), float4(1.0, 1.0, 1.0, 0.85 * fade * fade));
    float2 spark = float2(dashpop_shape.z, 0.0);
    float d = min(length(p - spark), length(p + spark)) - dashpop_shape.w;
    c.paint(sdf.filled(d), float4(1.0, 1.0, 1.0, fade));
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
