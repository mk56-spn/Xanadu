// A dash note: a thin bar through every line of the chord, in its colour, with
// a small diamond outlined in white at its middle; it names no way, the player
// picks one. The drawer sets dash_tint, the bar's size (dash_shape.xy) and the
// diamond's half diagonal (dash_shape.z). Sizes are item units, made screen
// pixels with `unit`.
import xanadu;

[Default(1, 1, 1, 1)] uniform colour dash_tint;
[Default(150, 10, 14, 0)] uniform float4 dash_shape;

float4 shade(ItemInput i)
{
    // At least four pixels to its corners, so it stays a diamond when small.
    float corner = i.pixels(dash_shape.z, 4);

    colour c = colours.transparent;
    c.paint(Box(dash_shape.xy * unit), dash_tint);
    c.paint(Rhombus(float2(corner * 2)).stroked(2), colours.white.withAlpha(dash_tint.a));

    // The item's colour carries its self modulate and fades.
    return c * i.colour;
}
