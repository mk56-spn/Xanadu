// A dash note: a thin bar through every line of the chord, in its colour, with
// a small diamond outlined in white at its middle; it names no way, the player
// picks one. The drawer sets dash_tint and the bar's size (dash_shape.xy, item
// units) and the diamond's half diagonal (dash_shape.z). Sizes are in item units
// (playfield units), made screen pixels with xif_unit.
import xanadu;

[Default(1)] uniform float xif_unit;
[Default(1, 1, 1, 1)] uniform float4 dash_tint;
[Default(150, 10, 14, 0)] uniform float4 dash_shape;

float4 shade(ItemInput i)
{
    float unit = max(xif_unit, 1e-4);
    float px = 1.0 / unit;
    float2 p = i.canvas.centred / unit;
    colour c = colours.transparent;
    c.paint(sdf.filled(Box(dash_shape.xy).local(p), px), dash_tint);
    // The diamond: |x| + |y| = r, its distance measured square to its sides; 2 pixels wide.
    float r = max(dash_shape.z, 4.0 * px);
    float diamond = (abs(p.x) + abs(p.y) - r) * 0.70710678;
    c.paint(sdf.stroked(diamond, 2.0 * px, px), float4(1.0, 1.0, 1.0, dash_tint.a));
    return float4(c.rgb * i.colour.rgb, c.a * i.colour.a);
}
